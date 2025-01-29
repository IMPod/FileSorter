using FileSorter.StartupProject.Models;
using SortingFile.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Utilities.Constants;

namespace SortingFile.Services;

/// <summary>
/// Provides methods for external file sorting using chunk splitting, parallel sorting, and k-way merge.
/// </summary>
public class ExternalSortService(SortingConfig config) : IExternalSortService
{
    /// <summary>
    /// Splits the input file into chunks, sorts them in parallel, and returns the temporary sorted files.
    /// </summary>
    public async Task<bool> SplitAndSortChunks(string inputFile)
    {
        var stopwatch = Stopwatch.StartNew();

        var chunkQueue = new BlockingCollection<List<NumberStringLine>>(config.MaxParallelSorters * 2);
        var tempFiles = new ConcurrentBag<string>();

        // 1) Start parallel sorters FIRST so they can consume chunks immediately
        var sortTasks = StartParallelSorters(chunkQueue, tempFiles);

        Console.WriteLine("[Sorting] Starting chunk production...");
        // 2) Produce chunks (this will enqueue them)
        try
        {
            await ProduceChunksAsync(inputFile, chunkQueue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during chunk production: {ex.Message}");
            throw;
        }
        finally
        {
            chunkQueue.CompleteAdding();
            Console.WriteLine("[Sorting] Chunk production completed.");
        }

        Console.WriteLine("[Sorting] Starting parallel sorting...");
        // 3) Wait for all sorting tasks to complete
        try
        {
            await Task.WhenAll(sortTasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during parallel sorting: {ex.Message}");
            throw;
        }

        Console.WriteLine("[Sorting] Parallel sorting completed.");

        Console.WriteLine("[Sorting] Starting k-way merge...");
        // 4) Merge sorted chunks into a single output file
        await MergeSortedChunks(tempFiles.ToList(), config.OutputPath);

        Console.WriteLine($"[Sorting] Delete temp files. Elapsed: {stopwatch.Elapsed}");
        // 5) Cleanup temporary files
        foreach (var file in tempFiles.ToList())
        {
            File.Delete(file);
        }

        stopwatch.Stop();
        Console.WriteLine($"[Sorting] External sort completed in {stopwatch.Elapsed}.");

        return true;
    }

    /// <summary>
    /// Merges a list of sorted chunk files into one output file using k-way merge.
    /// </summary>
    public async Task MergeSortedChunks(List<string> chunkFiles, string outputFile)
    {
        List<StreamReader> readers = new();
        try
        {
            // Step 1: Open each sorted chunk file
            readers = await OpenChunkReadersAsync(chunkFiles);

            // Step 2: Initialize the priority queue with the first line of each chunk
            var pq = await InitializePriorityQueueAsync(readers);

            // Step 3: Perform k-way merge and write the result to the output file
            await PerformKWayMergeAsync(outputFile, readers, pq);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during merge operation: {ex.Message}");
            throw;
        }
        finally
        {
            // Step 4: Close all readers
            CloseReaders(readers);
        }
    }

    /// <summary>
    /// Opens multiple chunk files for reading.
    /// </summary>
    /// <param name="chunkFiles">List of chunk file paths.</param>
    /// <returns>List of StreamReader objects for each chunk file.</returns>
    private async Task<List<StreamReader>> OpenChunkReadersAsync(List<string> chunkFiles)
    {
        var readers = new List<StreamReader>();
        foreach (var file in chunkFiles)
        {
            var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, Setting.SMALLBUFFERSIZE, FileOptions.SequentialScan);
            readers.Add(new StreamReader(fs, Encoding.UTF8));
        }
        return readers;
    }

    /// <summary>
    /// Initializes the priority queue with the first line of each chunk file.
    /// </summary>
    /// <param name="readers">List of StreamReader objects for each chunk file.</param>
    /// <returns>PriorityQueue initialized with the first lines of each chunk.</returns>
    private async Task<PriorityQueue<(NumberStringLine line, int readerIndex), NumberStringLine>> InitializePriorityQueueAsync(List<StreamReader> readers)
    {
        var comparer = new NumberStringLineComparer();
        var pq = new PriorityQueue<(NumberStringLine line, int readerIndex), NumberStringLine>(
            new PriorityLineComparerAdapter(comparer)
        );

        for (int i = 0; i < readers.Count; i++)
        {
            var item = await ReadOneLine(readers[i]);
            if (item != null)
            {
                pq.Enqueue((item.Value, i), item.Value);
            }
        }

        return pq;
    }

    /// <summary>
    /// Performs k-way merge using the priority queue and writes the merged result to the output file.
    /// </summary>
    /// <param name="outputFile">Path to the output file.</param>
    /// <param name="readers">List of StreamReader objects for each chunk file.</param>
    /// <param name="pq">PriorityQueue containing the lines to be merged.</param>
    private async Task PerformKWayMergeAsync(string outputFile, List<StreamReader> readers, PriorityQueue<(NumberStringLine line, int readerIndex), NumberStringLine> pq)
    {
        using var outFs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None, Setting.SMALLBUFFERSIZE, FileOptions.SequentialScan);
        using var writer = new StreamWriter(outFs, Encoding.UTF8);

        while (pq.Count > 0)
        {
            var (line, idx) = pq.Dequeue();
            await writer.WriteLineAsync($"{line.Number}. {line.Text}");

            var nextLine = await ReadOneLine(readers[idx]);
            if (nextLine != null)
            {
                pq.Enqueue((nextLine.Value, idx), nextLine.Value);
            }
        }
    }

    /// <summary>
    /// Closes all chunk file readers.
    /// </summary>
    /// <param name="readers">List of StreamReader objects to close.</param>
    private void CloseReaders(List<StreamReader> readers)
    {
        foreach (var rdr in readers)
        {
            rdr.Dispose();
        }
    }

    /// <summary>
    /// Starts parallel tasks that take chunks from the queue, sort them, and save them to temporary files.
    /// </summary>
    private List<Task> StartParallelSorters(BlockingCollection<List<NumberStringLine>> chunkQueue, ConcurrentBag<string> tempFiles)
    {
        var tasks = new List<Task>();

        for (int i = 0; i < config.MaxParallelSorters; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var comparer = new NumberStringLineComparer();
                while (!chunkQueue.IsCompleted)
                {
                    if (chunkQueue.TryTake(out var chunk, TimeSpan.FromMilliseconds(50)))
                    {
                        try
                        {
                            chunk.Sort(comparer);

                            var tempFile = await SaveSortedChunkToFile(chunk);
                            tempFiles.Add(tempFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error during chunk sorting: {ex.Message}");
                            throw;
                        }
                    }

                }
            }));
        }

        return tasks;
    }

    /// <summary>
    /// Reads chunks from the input file and adds them to the queue.
    /// </summary>
    private async Task ProduceChunksAsync(string inputFile, BlockingCollection<List<NumberStringLine>> chunkQueue)
    {
        try
        {
            if (!File.Exists(inputFile))
            {
                throw new FileNotFoundException("Input file does not exist.", inputFile);
            }

            using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, Setting.BIGBUFFERSIZE, FileOptions.SequentialScan);
            using var reader = new StreamReader(fs, Encoding.UTF8);

            bool endOfFile = false;

            while (!endOfFile)
            {
                var (chunkData, reachedEnd) = await ReadNextChunk(reader);
                endOfFile = reachedEnd;

                if (chunkData.Count > 0)
                {
                    //Console.WriteLine($"[Chunking] Adding chunk with {chunkData.Count} lines to queue.");
                    chunkQueue.Add(chunkData);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during chunk reading: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Reads a single chunk of data from the reader or detects end of file.
    /// </summary>
    private async Task<(List<NumberStringLine> chunk, bool endOfFile)> ReadNextChunk(StreamReader reader)
    {
        long currentChunkSize = 0;
        var chunkData = new List<NumberStringLine>(100_000);

        while (currentChunkSize < config.MaxChunkSizeBytes)
        {
            string line;
            try
            {
                line = await reader.ReadLineAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading line: {ex.Message}");
                throw;
            }

            if (line == null)
            {
                return (chunkData, true);
            }

            var (ok, parsed) = ParseLine(line);
            if (!ok) continue;

            chunkData.Add(parsed);
            currentChunkSize += line.Length * 2;
        }

        return (chunkData, false);
    }

    /// <summary>
    /// Saves a sorted chunk to a temporary file.
    /// </summary>
    private async Task<string> SaveSortedChunkToFile(List<NumberStringLine> chunkData)
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            using var outFs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, Setting.BIGBUFFERSIZE, FileOptions.SequentialScan);
            using var writer = new StreamWriter(outFs, Encoding.UTF8);

            foreach (var item in chunkData)
            {
                await writer.WriteLineAsync($"{item.Number}. {item.Text}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving sorted chunk: {ex.Message}");
            throw;
        }

        return tempFile;
    }

    /// <summary>
    /// Parses a line of the form 'number. text' into NumberStringLine.
    /// </summary>
    private (bool, NumberStringLine) ParseLine(string line)
    {
        var parts = line.Split(new[] { '.' }, 2, StringSplitOptions.None);
        if (parts.Length < 2) 
            return (false, default);
        if (!long.TryParse(parts[0], out long number)) 
            return (false, default);

        return (true, new NumberStringLine(number, parts[1].Trim()));
    }

    /// <summary>
    /// Reads and parses a single line from a reader.
    /// </summary>
    private async Task<NumberStringLine?> ReadOneLine(StreamReader reader)
    {
        var line = await reader.ReadLineAsync();
        if (line == null) return null;

        var (ok, parsed) = ParseLine(line);
        return ok ? parsed : null;
    }

    public bool IsOutputFileSorted(string outputFile)
    {
        var previousLine = default(NumberStringLine);
        using var reader = new StreamReader(outputFile, Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var (ok, parsed) = ParseLine(line);
            if (!ok || parsed == null)
            {
                throw new InvalidOperationException("Invalid line format in output file.");
            }

            if (previousLine != null && (previousLine.Number > parsed.Number && previousLine.Text == parsed.Text))
            {
                return false;
            }
            previousLine = parsed;
        }
        return true;
    }
}
