using FileGenerator.Models;
using Utilities.Constants;

namespace FileGenerator.Services;

public class GeneraterService : IGeneraterService
{
    private const int _randomSize = 1_000_000_000;
    /// <summary>
    /// Splits the total line count into 'ChunkCount' parts, 
    /// generates each part in parallel, and returns the list of temporary files.
    /// </summary>
    public async Task<string[]> GenerateChunksInParallel(GeneratorConfig config)
    {
        long linesPerChunk = config.TotalLines / config.ChunkCount;
        long remainder = config.TotalLines % config.ChunkCount;

        var tempFiles = new string[config.ChunkCount];
        var tasks = new Task[config.ChunkCount];

        for (int i = 0; i < config.ChunkCount; i++)
        {
            long linesThisChunk = linesPerChunk;
            if (i < remainder)
                linesThisChunk++;

            string tmpFile = Path.GetTempFileName();
            tempFiles[i] = tmpFile;

            tasks[i] = Task.Run(() =>
            {
                try
                {
                    GenerateOneChunk(tmpFile, linesThisChunk, config);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating chunk {i}: {ex.Message}");
                    throw;
                }
            });
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during parallel chunk generation: {ex.Message}");
            throw;
        }

        return tempFiles;
    }

    /// <summary>
    /// Generates 'countLines' lines and writes them to 'tempFile'.
    /// Each line follows the format: "<number>. <text>"
    /// </summary>
    public void GenerateOneChunk(string tempFile, long countLines, GeneratorConfig config)
    {
        try
        {
            var random = Random.Shared;

            using var fs = new FileStream(
                tempFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: Setting.SMALLBUFFERSIZE,
                FileOptions.SequentialScan
            );
            using var writer = new StreamWriter(fs, System.Text.Encoding.UTF8);

            var texts = config.SampleTexts;
            int sampleCount = texts.Length;

            for (long i = 0; i < countLines; i++)
            {
                long number = random.NextInt64(1, _randomSize);
                string text = texts[random.Next(sampleCount)];
                writer.WriteLine($"{number}. {text}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to chunk file {tempFile}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Concatenates all generated chunk files into a single output file.
    /// </summary>
    public async Task ConcatenateFiles(string[] files, string outputPath)
    {
        try
        {
            using var outStream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: Setting.SMALLBUFFERSIZE,
                FileOptions.SequentialScan
            );
            using var writer = new StreamWriter(outStream, System.Text.Encoding.UTF8);

            var buffer = new char[65536];

            foreach (string file in files)
            {
                try
                {
                    using var inStream = new FileStream(
                        file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: Setting.SMALLBUFFERSIZE,
                        FileOptions.SequentialScan
                    );
                    using var reader = new StreamReader(inStream, System.Text.Encoding.UTF8);

                    int read;
                    while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await writer.WriteAsync(buffer, 0, read);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading or writing chunk file {file}: {ex.Message}");
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error concatenating files: {ex.Message}");
            throw;
        }
    }
}
