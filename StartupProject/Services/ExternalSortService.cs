using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Utilities.Constants;
using FileSorter.StartupProject.Models;
using SortingFile.Models;

namespace SortingFile.Services;

/// <summary>
/// Provides methods for external file sorting using chunk splitting, parallel sorting, and k-way merge.
/// </summary>
public class ExternalSortService(SortingConfig config) : IExternalSortService
{
    public async Task<bool> SplitAndSortChunks(string inputFile, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var chunkQueue = new ConcurrentQueue<List<NumberStringLine>>();
        var tempFiles = new ConcurrentBag<string>();
        using var semaphore = new SemaphoreSlim(config.MaxParallelSorters);

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

    private List<Task> StartParallelSorters(ConcurrentQueue<List<NumberStringLine>> chunkQueue, ConcurrentBag<string> tempFiles, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        for (var i = 0; i < config.MaxParallelSorters; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var comparer = new NumberStringLineComparer();
                while (!cancellationToken.IsCancellationRequested && chunkQueue.TryDequeue(out var chunk))
                {
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

    private async Task ProduceChunksAsync(string inputFile, ConcurrentQueue<List<NumberStringLine>> chunkQueue, CancellationToken cancellationToken)
    {
        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input file does not exist.", inputFile);

        await using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, Setting.BIGBUFFERSIZE, FileOptions.SequentialScan);
        using var reader = new StreamReader(fs, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            var (chunkData, reachedEnd) = await ReadNextChunk(reader, cancellationToken);
            if (chunkData.Count > 0)
                chunkQueue.Enqueue(chunkData);

            if (reachedEnd)
                break;
        }
    }

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

    private static async Task<string> SaveSortedChunkToFile(List<NumberStringLine> chunkData, CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllLinesAsync(tempFile, chunkData.Select(l => $"{l.Number}. {l.Text}"), cancellationToken);
        return tempFile;
    }

    public async Task MergeSortedChunks(List<string> chunkFiles, string outputFile, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);
        var readers = chunkFiles.Select(file => new StreamReader(file, Encoding.UTF8)).ToList();
        var pq = new PriorityQueue<(NumberStringLine line, int index), NumberStringLine>(new PriorityLineComparerAdapter(new NumberStringLineComparer()));

        for (var i = 0; i < readers.Count; i++)
        {
            var line = await ReadOneLine(readers[i], cancellationToken);
            if (line != null) pq.Enqueue((line.Value, i), line.Value);
        }

        while (pq.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var (line, idx) = pq.Dequeue();
            var sb = new StringBuilder();
            sb.Append(line.Number).Append(". ").Append(line.Text);
            await writer.WriteLineAsync(sb, cancellationToken);

            var nextLine = await ReadOneLine(readers[idx], cancellationToken);
            if (nextLine != null) pq.Enqueue((nextLine.Value, idx), nextLine.Value);
        }

        readers.ForEach(r => r.Dispose());
    }

    private static async Task<NumberStringLine?> ReadOneLine(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        return line != null ? ParseLine(line).Item2 : null;
    }

    private static (bool, NumberStringLine) ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) 
            return (false, default);
        var dotIndex = line.IndexOf('.');
        if (dotIndex == -1 || dotIndex == line.Length - 1) 
            return (false, default);

        return !long.TryParse(line[..dotIndex], out var number) ? (false, default) : (true, new NumberStringLine(number, line[(dotIndex + 1)..].Trim()));
    }
}