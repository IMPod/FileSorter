using FileGenerator.Models;

namespace FileGenerator.Services;

internal class GeneraterService : IGeneraterService
{
    /// <summary>
    /// Splits the total line count into 'ChunkCount' parts, 
    /// generates each part in parallel, and returns the list of temporary files.
    /// </summary>
    public async Task<string[]> GenerateChunksInParallel(GeneratorConfig config)
    {
        // Divide TotalLines by ChunkCount
        long linesPerChunk = config.TotalLines / config.ChunkCount;
        long remainder = config.TotalLines % config.ChunkCount;

        // This array will hold the temporary files
        var tempFiles = new string[config.ChunkCount];
        var tasks = new Task[config.ChunkCount];

        for (int i = 0; i < config.ChunkCount; i++)
        {
            // Distribute the remainder so that some chunks get one extra line
            long linesThisChunk = linesPerChunk;
            if (i < remainder)
                linesThisChunk++;

            // Generate a unique temporary file name
            // Note: Path.GetTempFileName() can be used, but it might be limited in number,
            // so you could also use a custom approach if needed.
            string tmpFile = Path.GetTempFileName();
            tempFiles[i] = tmpFile;

            // Create a task to generate data for this chunk
            tasks[i] = Task.Run(() => GenerateOneChunk(tmpFile, linesThisChunk, config));
        }

        // Wait until all chunks are done
        await Task.WhenAll(tasks);

        return tempFiles;
    }

    /// <summary>
    /// Generates 'countLines' lines and writes them to 'tempFile'.
    /// Each line follows the format: "<number>. <text>"
    /// </summary>
    private void GenerateOneChunk(string tempFile, long countLines, GeneratorConfig config)
    {
        var random = Random.Shared;

        // Open the file with a decent buffer size to reduce I/O overhead
        using var fs = new FileStream(
            tempFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536, // 64KB buffer
            FileOptions.SequentialScan
        );
        using var writer = new StreamWriter(fs, System.Text.Encoding.UTF8);

        var texts = config.SampleTexts; // local reference for faster access
        int sampleCount = texts.Length;

        for (long i = 0; i < countLines; i++)
        {
            long number = random.NextInt64(1, 1_000_000_000);
            string text = texts[random.Next(sampleCount)];
            writer.WriteLine($"{number}. {text}");
        }
    }

    /// <summary>
    /// Concatenates all generated chunk files into a single output file.
    /// </summary>
    public async Task ConcatenateFiles(string[] files, string outputPath)
    {
        // Open the final output file
        using var outStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            FileOptions.SequentialScan
        );
        using var writer = new StreamWriter(outStream, System.Text.Encoding.UTF8);

        // We will reuse a buffer to read each chunk
        var buffer = new char[65536];

        // Loop over each temporary file
        foreach (string file in files)
        {
            using var inStream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                FileOptions.SequentialScan
            );
            using var reader = new StreamReader(inStream, System.Text.Encoding.UTF8);

            int read;
            // Read from the chunk file in a loop, then write to the final file
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await writer.WriteAsync(buffer, 0, read);
            }
        }
    }
}
