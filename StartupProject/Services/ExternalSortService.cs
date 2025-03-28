using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Utilities.Constants;
using FileSorter.StartupProject.Models;
using SortingFile.Models;
using System.Linq;

namespace SortingFile.Services;

/// <summary>
/// Provides methods for external file sorting using chunk splitting, parallel sorting, and k-way merge.
/// </summary>
public class ExternalSortService(SortingConfig config) : IExternalSortService
{
    private volatile bool isChunkProductionCompleted = false;

    /// <summary>
    /// Splits a large input file into chunks, sorts them in parallel, and then merges them.
    /// </summary>
    /// <param name="inputFile">Path to the input file to be sorted</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>True if sorting completes successfully</returns>
    public async Task<bool> SplitAndSortChunks(string inputFile, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var chunkQueue = new ConcurrentQueue<List<NumberStringLine>>();
        var tempFiles = new ConcurrentBag<string>();
        using var semaphore = new SemaphoreSlim(config.MaxParallelSorters);

        isChunkProductionCompleted = false;

        var sortTasks = StartParallelSorters(chunkQueue, tempFiles, semaphore, cancellationToken);
        Console.WriteLine("[Sorting] Starting chunk production...");

        try
        {
            await ProduceChunksAsync(inputFile, chunkQueue, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during chunk production: {ex.Message}");
            throw;
        }
        finally
        {
            isChunkProductionCompleted = true;
        }

        await Task.WhenAll(sortTasks);
        Console.WriteLine("[Sorting] Parallel sorting completed.");

        await MergeSortedChunks(tempFiles.ToList(), config.OutputPath, cancellationToken);

        foreach (var file in tempFiles)
        {
            File.Delete(file);
        }

        stopwatch.Stop();
        Console.WriteLine($"[Sorting] External sort completed in {stopwatch.Elapsed}.");
        return true;
    }

    /// <summary>
    /// Starts parallel sorting tasks for processing chunks from the queue
    /// </summary>
    /// <param name="chunkQueue">Thread-safe queue of chunks to be sorted</param>
    /// <param name="tempFiles">Collection to store temporary sorted chunk files</param>
    /// <param name="semaphore">Semaphore to limit concurrent sorting operations</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>List of sorting tasks</returns>
    private List<Task> StartParallelSorters(
        ConcurrentQueue<List<NumberStringLine>> chunkQueue, 
        ConcurrentBag<string> tempFiles, 
        SemaphoreSlim semaphore, 
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        for (var i = 0; i < config.MaxParallelSorters; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var comparer = new NumberStringLineComparer();
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!chunkQueue.TryDequeue(out var chunk))
                    {
                        if (isChunkProductionCompleted) break;
                        await Task.Delay(10, cancellationToken);
                        continue;
                    }

                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        chunk.Sort(comparer);
                        var tempFile = await SaveSortedChunkToFile(chunk, cancellationToken);
                        tempFiles.Add(tempFile);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }, cancellationToken));
        }
        return tasks;
    }

    /// <summary>
    /// Reads the input file and produces chunks for parallel processing
    /// </summary>
    /// <param name="inputFile">Path to the input file</param>
    /// <param name="chunkQueue">Queue to enqueue produced chunks</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    private async Task ProduceChunksAsync(string inputFile, ConcurrentQueue<List<NumberStringLine>> chunkQueue, CancellationToken cancellationToken)
    {
        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input file does not exist.", inputFile);

        await using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, Setting.BIGBUFFERSIZE, FileOptions.SequentialScan);
        using var reader = new StreamReader(fs, Encoding.UTF8);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var (chunkData, reachedEnd) = await ReadNextChunk(reader, cancellationToken);
                if (chunkData.Count > 0)
                    chunkQueue.Enqueue(chunkData);

                if (reachedEnd)
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during chunk production: {ex.Message}");
            throw;
        }
        finally
        {
            isChunkProductionCompleted = true;
        }
    }

    /// <summary>
    /// Reads the next chunk of data from the file reader
    /// </summary>
    /// <param name="reader">StreamReader to read from</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A tuple containing the chunk of data and a flag indicating if end of file was reached</returns>
    private async Task<(List<NumberStringLine> chunk, bool endOfFile)> ReadNextChunk(StreamReader reader, CancellationToken cancellationToken)
    {
        long currentChunkSize = 0;
        var chunkData = new List<NumberStringLine>(100_000);

        while (currentChunkSize < config.MaxChunkSizeBytes && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) return (chunkData, true);

            var (ok, parsed) = ParseLine(line);
            if (!ok) continue;

            chunkData.Add(parsed);
            currentChunkSize += line.Length * 2;
        }

        return (chunkData, false);
    }

    /// <summary>
    /// Saves a sorted chunk to a temporary file
    /// </summary>
    /// <param name="chunkData">Sorted chunk of data</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Path to the temporary file</returns>
    private static async Task<string> SaveSortedChunkToFile(List<NumberStringLine> chunkData, CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllLinesAsync(tempFile, chunkData.Select(l => $"{l.Number}. {l.Text}"), cancellationToken);
        return tempFile;
    }

    /// <summary>
    /// Merges multiple sorted chunk files into a single output file using a k-way merge algorithm
    /// </summary>
    /// <param name="chunkFiles">List of sorted chunk files to merge</param>
    /// <param name="outputFile">Path to the final sorted output file</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    public async Task MergeSortedChunks(List<string> chunkFiles, string outputFile, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(outputFile, false, Encoding.UTF8, bufferSize: Setting.BIGBUFFERSIZE);
        var readers = chunkFiles
            .AsParallel()
            .Select(file => new StreamReader(file, Encoding.UTF8,false, bufferSize: Setting.BIGBUFFERSIZE))
            .ToList();

        var pq = new PriorityQueue<(NumberStringLine line, int index), NumberStringLine>(new PriorityLineComparerAdapter(new NumberStringLineComparer()));

        for (var i = 0; i < readers.Count; i++)
        {
            var line = await ReadOneLine(readers[i], cancellationToken);
            if (line != null) pq.Enqueue((line.Value, i), line.Value);
        }

        var batchSize = 1000;
        var batch = new List<StringBuilder>(batchSize);

        while (pq.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var (line, idx) = pq.Dequeue();
            var sb = new StringBuilder();
            sb.Append(line.Number).Append(". ").Append(line.Text);
            batch.Add(sb);

            if (batch.Count >= batchSize)
            {
                await WriteBatchAsync(writer, batch, cancellationToken);
                batch.Clear();
            }

            var nextLine = await ReadOneLine(readers[idx], cancellationToken);
            if (nextLine != null) pq.Enqueue((nextLine.Value, idx), nextLine.Value);
        }

        readers.ForEach(r => r.Dispose());
    }

    /// <summary>
    /// Writes a batch of lines to the output file
    /// </summary>
    /// <param name="writer">StreamWriter for output file</param>
    /// <param name="batch">Batch of lines to write</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    private async Task WriteBatchAsync(StreamWriter writer, List<StringBuilder> batch, CancellationToken cancellationToken)
    {
        foreach (var sb in batch)
        {
            await writer.WriteLineAsync(sb, cancellationToken);
        }
    }

    /// <summary>
    /// Reads a single line from a StreamReader and converts it to a NumberStringLine
    /// </summary>
    /// <param name="reader">StreamReader to read from</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Parsed NumberStringLine or null if end of file</returns>
    private static async Task<NumberStringLine?> ReadOneLine(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        return line != null ? ParseLine(line).Item2 : null;
    }

    /// <summary>
    /// Parses a line into a NumberStringLine object
    /// </summary>
    /// <param name="line">Input line to parse</param>
    /// <returns>A tuple indicating parsing success and the parsed NumberStringLine</returns>
    private static (bool, NumberStringLine) ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) 
            return (false, default);
        var dotIndex = line.IndexOf('.');
        if (dotIndex == -1 || dotIndex == line.Length - 1) 
            return (false, default);

        return !long.TryParse(line[..dotIndex], out var number) 
            ? (false, default) 
            : (true, new NumberStringLine(number, line[(dotIndex + 1)..].Trim()));
    }
}