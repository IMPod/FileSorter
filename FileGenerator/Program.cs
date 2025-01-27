using FileGenerator.Models;
using FileGenerator.Services;
using System.Diagnostics;

namespace FileGenerator;

internal static class Program
{
    // Samsung SSD, RAM 16,0 GB, Intel(R) Core(TM) i7-8750H CPU @ 2.20GHz   2.20 GHz

    // Command line parameters:
    //   1) Output file path (e.g. "big_parallel.txt")
    //   2) Total number of lines (e.g. "1000000")
    //   3) Optional: number of chunks (default is 8)
    //
    // Example:
    //   dotnet run -- "big_parallel.txt" 1000000 8
    public static async Task Main(string[] args)
    {
        IGeneraterService generaterService = new GeneraterService();

        string outputPath = args.Length > 0 ? args[0] : "big_parallel.txt";
        long totalLines = args.Length > 1 ? long.Parse(args[1]) : 4_000_000_00;
        int chunkCount = args.Length > 2 ? int.Parse(args[2]) : 8;

        var config = new GeneratorConfig(totalLines, outputPath, chunkCount);
        config.Validate();

        Console.WriteLine($"[Generator] Will generate {config.TotalLines} lines in {config.ChunkCount} chunks.");
        Console.WriteLine($"[Generator] Final file will be: {config.OutputPath}");

        var sw = Stopwatch.StartNew();

        var tempFiles = await generaterService.GenerateChunksInParallel(config);

        Console.WriteLine($"[Generator] Chunk generation completed in {sw.Elapsed}.");
        Console.WriteLine("[Generator] Concatenating all chunks into a single file...");

        await generaterService.ConcatenateFiles(tempFiles, config.OutputPath);

        // Remove temporary files
        foreach (var file in tempFiles)
        {
            File.Delete(file);
        }

        sw.Stop();
        Console.WriteLine($"[Generator] Done! Total time: {sw.Elapsed}.");
    }    
}