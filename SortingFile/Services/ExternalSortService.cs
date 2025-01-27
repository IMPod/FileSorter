using SortingFile.Models;
using System.Collections.Concurrent;
using System.Text;
using Utilities.Constants;

namespace SortingFile.Services;

/// <summary>
/// Provides methods for external file sorting using chunk splitting, parallel sorting, and k-way merge.
/// </summary>
internal class ExternalSortService : IExternalSortService
{
    private const long MaxChunkSizeBytes = Setting.MAX_CHUNK_SIZE_BYTES;
    private readonly int _maxParallelSorters = Setting.MAX_PARALLELSORTERS;

    /// <summary>
    /// Splits the input file into chunks, sorts them in parallel, and returns the temporary sorted files.
    /// </summary>
    public async Task<List<string>> SplitAndSortChunks(string inputFile)
    {
        var chunkQueue = new BlockingCollection<List<NumberStringLine>>(_maxParallelSorters * 2);
        var tempFiles = new ConcurrentBag<string>();

        var sortTasks = StartParallelSorters(chunkQueue, tempFiles);

        await ProduceChunksAsync(inputFile, chunkQueue);
        chunkQueue.CompleteAdding();

        await Task.WhenAll(sortTasks);

        return tempFiles.ToList();
    }

    /// <summary>
    /// Merges a list of sorted chunk files into one output file using k-way merge.
    /// </summary>
    public async Task MergeSortedChunks(List<string> chunkFiles, string outputFile)
    {
        var readers = OpenChunkReaders(chunkFiles);
        var comparer = new NumberStringLineComparer();

        var pq = new PriorityQueue<(NumberStringLine line, int readerIndex), NumberStringLine>(
            new PriorityLineComparerAdapter(comparer)
        );

        await InitializePriorityQueue(readers, pq);

        using var outFs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.SequentialScan);
        using var outWriter = new StreamWriter(outFs, Encoding.UTF8);

        while (pq.Count > 0)
        {
            var (line, idx) = pq.Dequeue();
            await outWriter.WriteLineAsync($"{line.Number}. {line.Text}");

            var nextLine = await ReadOneLine(readers[idx]);
            if (nextLine != null)
            {
                pq.Enqueue((nextLine.Value, idx), nextLine.Value);
            }
        }

        CloseReaders(readers);
    }

    /// <summary>
    /// Starts parallel tasks that take chunks from the queue, sort them, and save them to temporary files.
    /// </summary>
    private List<Task> StartParallelSorters(
        BlockingCollection<List<NumberStringLine>> chunkQueue,
        ConcurrentBag<string> tempFiles)
    {
        var tasks = new List<Task>();

        for (int i = 0; i < _maxParallelSorters; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var comparer = new NumberStringLineComparer();

                while (!chunkQueue.IsCompleted)
                {
                    List<NumberStringLine> chunk;
                    try
                    {
                        chunk = chunkQueue.Take();
                    }
                    catch (InvalidOperationException)
                    {
                        break;
                    }

                    chunk.Sort(comparer);

                    var tempFile = await SaveSortedChunkToFile(chunk);
                    tempFiles.Add(tempFile);
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
        using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1_048_576, FileOptions.SequentialScan);
        using var reader = new StreamReader(fs, Encoding.UTF8);

        bool endOfFile = false;

        while (!endOfFile)
        {
            var (chunkData, reachedEnd) = await ReadNextChunk(reader);
            endOfFile = reachedEnd;

            if (chunkData.Count > 0)
            {
                chunkQueue.Add(chunkData);
            }
        }
    }

    /// <summary>
    /// Reads a single chunk of data from the reader or detects end of file.
    /// </summary>
    private async Task<(List<NumberStringLine> chunk, bool endOfFile)> ReadNextChunk(StreamReader reader)
    {
        long currentChunkSize = 0;
        var chunkData = new List<NumberStringLine>(100_000);

        while (currentChunkSize < MaxChunkSizeBytes)
        {
            var line = await reader.ReadLineAsync();
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

        using var outFs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 1_048_576, FileOptions.SequentialScan);
        using var writer = new StreamWriter(outFs, Encoding.UTF8);

        foreach (var item in chunkData)
        {
            await writer.WriteLineAsync($"{item.Number}. {item.Text}");
        }

        return tempFile;
    }

    /// <summary>
    /// Parses a line of the form 'number. text' into NumberStringLine.
    /// </summary>
    private (bool, NumberStringLine) ParseLine(string line)
    {
        var parts = line.Split(new[] { '.' }, 2, StringSplitOptions.None);
        if (parts.Length < 2) return (false, default);
        if (!long.TryParse(parts[0], out long number)) return (false, default);

        return (true, new NumberStringLine(number, parts[1].Trim()));
    }

    /// <summary>
    /// Opens multiple chunk files for reading.
    /// </summary>
    private List<StreamReader> OpenChunkReaders(List<string> chunkFiles)
    {
        var readers = new List<StreamReader>(chunkFiles.Count);
        foreach (var file in chunkFiles)
        {
            var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            readers.Add(new StreamReader(fs, Encoding.UTF8));
        }
        return readers;
    }

    /// <summary>
    /// Initializes the priority queue with the first line of each chunk file.
    /// </summary>
    private async Task InitializePriorityQueue(
        List<StreamReader> readers,
        PriorityQueue<(NumberStringLine line, int readerIndex), NumberStringLine> pq)
    {
        for (int i = 0; i < readers.Count; i++)
        {
            var lineObj = await ReadOneLine(readers[i]);
            if (lineObj != null)
            {
                pq.Enqueue((lineObj.Value, i), lineObj.Value);
            }
        }
    }

    /// <summary>
    /// Closes all chunk file readers.
    /// </summary>
    private void CloseReaders(List<StreamReader> readers)
    {
        foreach (var rdr in readers) rdr.Dispose();
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
}
