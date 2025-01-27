using SortingFile.Models;
using System.Text;

namespace SortingFile.Services;

internal class ExternalSortService : IExternalSortService
{
    // Approximate max chunk size in bytes to load in memory
    private const long MaxChunkSizeBytes = 200_000_000; // ~200MB

    /// <summary>
    /// Reads the input file in chunks of up to MaxChunkSizeBytes,
    /// parses each line into NumberStringLine,
    /// sorts in memory using List.Sort(...),
    /// writes each sorted chunk to a temp file,
    /// and returns the list of temp file paths.
    /// </summary>
    public async Task<List<string>> SplitAndSortChunks(string inputFile)
    {
        var tempFiles = new List<string>();
        var comparer = new NumberStringLineComparer();

        using var fs = new FileStream(
            inputFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            FileOptions.SequentialScan
        );
        using var reader = new StreamReader(fs, Encoding.UTF8);

        bool endOfFile = false;
        while (!endOfFile)
        {
            long currentChunkSize = 0;
            var chunkData = new List<NumberStringLine>(capacity: 100_000);

            while (currentChunkSize < MaxChunkSizeBytes)
            {
                string? line = await reader.ReadLineAsync();
                if (line == null)
                {
                    endOfFile = true;
                    break;
                }

                var (ok, parsed) = ParseLine(line);
                if (!ok)
                    continue;

                chunkData.Add(parsed);
                currentChunkSize += line.Length * 2;
            }

            if (chunkData.Count > 0)
            {
                chunkData.Sort(comparer);

                string tempFile = Path.GetTempFileName();
                using var outFs = new FileStream(
                    tempFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 65536,
                    FileOptions.SequentialScan
                );
                using var writer = new StreamWriter(outFs, Encoding.UTF8);

                foreach (var item in chunkData)
                {
                    await writer.WriteLineAsync($"{item.Number}. {item.Text}");
                }

                tempFiles.Add(tempFile);
                Console.WriteLine($"Created sorted chunk: {tempFile}");
            }
        }

        return tempFiles;
    }

    /// <summary>
    /// Parses a line of the form "number. text" into a NumberStringLine.
    /// Returns (true, result) if successful, or (false, default) if it fails.
    /// </summary>
    private (bool, NumberStringLine) ParseLine(string line)
    {
        var parts = line.Split(new char[] { '.' }, 2, StringSplitOptions.None);
        if (parts.Length < 2)
            return (false, default);

        if (!long.TryParse(parts[0], out long number))
            return (false, default);

        string text = parts[1].Trim();

        return (true, new NumberStringLine(number, text));
    }

    /// <summary>
    /// Merges the sorted chunk files into one final output file (k-way merge)
    /// using a PriorityQueue of (NumberStringLine line, int readerIndex).
    /// </summary>
    public async Task MergeSortedChunks(List<string> chunkFiles, string outputFile)
    {
        var readers = new List<StreamReader>(chunkFiles.Count);
        for (int i = 0; i < chunkFiles.Count; i++)
        {
            var fs = new FileStream(
                chunkFiles[i],
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                FileOptions.SequentialScan
            );
            readers.Add(new StreamReader(fs, Encoding.UTF8));
        }

        var comparer = new NumberStringLineComparer();

        var pq = new PriorityQueue<(NumberStringLine line, int readerIndex), NumberStringLine>(
            new PriorityLineComparerAdapter(comparer)
        );

        for (int i = 0; i < readers.Count; i++)
        {
            var lineObj = await ReadOneLine(readers[i]);
            if (lineObj != null)
            {
                pq.Enqueue((lineObj.Value, i), lineObj.Value);
            }
        }

        using var outFs = new FileStream(
            outputFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            FileOptions.SequentialScan
        );
        using var outWriter = new StreamWriter(outFs, Encoding.UTF8);

        while (pq.Count > 0)
        {
            var dqResult = pq.Dequeue();
            var currentElement = dqResult; 

            NumberStringLine lineVal = currentElement.line; 
            int idx = currentElement.readerIndex;

            await outWriter.WriteLineAsync($"{lineVal.Number}. {lineVal.Text}");

            var nextLineObj = await ReadOneLine(readers[idx]);
            if (nextLineObj != null)
            {
                pq.Enqueue((nextLineObj.Value, idx), nextLineObj.Value);
            }
        }

        foreach (var rdr in readers)
        {
            rdr.Dispose();
        }
    }

    /// <summary>
    /// Reads one line from the given StreamReader, parses it into NumberStringLine,
    /// or returns null if EOF or parse fails.
    /// </summary>
    private async Task<NumberStringLine?> ReadOneLine(StreamReader reader)
    {
        string? line = await reader.ReadLineAsync();
        if (line == null)
            return null;

        var (ok, parsed) = ParseLine(line);
        if (!ok) return null;

        return parsed;
    }
}
