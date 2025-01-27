using System.Diagnostics;
using SortingFile.Services;

namespace SortingFile;

internal static class Program
{
    private static IExternalSortService _externalSortService = new ExternalSortService();
 
    // Usage: dotnet run -- <inputFile> <outputFile>

    public static async Task Main(string[] args)
    {       
        string inputFile = args.Length > 0 ? args[0] : "big_parallel.txt";
        string outputFile = args.Length > 1 ? args[1] : "big_parallel_sort.txt";

        Console.WriteLine("Starting external sort...");

        var stopwatch = Stopwatch.StartNew();
        List<string> sortedChunkFiles = await _externalSortService.SplitAndSortChunks(inputFile);

        Console.WriteLine($"Chunks created. Elapsed: {stopwatch.Elapsed}");
        Console.WriteLine("Starting k-way merge...");

        await _externalSortService.MergeSortedChunks(sortedChunkFiles, outputFile);

        foreach (var file in sortedChunkFiles)
        {
            File.Delete(file);
        }

        stopwatch.Stop();
        Console.WriteLine($"External sort completed in {stopwatch.Elapsed}.");
    }
}
