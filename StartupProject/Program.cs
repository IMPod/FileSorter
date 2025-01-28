using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using FileGenerator.Models;
using FileGenerator.Services;
using SortingFile.Services;


bool validChoice = false;
string userInput = "";

var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

IConfigurationRoot configuration = builder.Build();

var GENERATED_FILE = configuration.GetSection("Settings")["GENERATED_FILE"];
var ORDERED_FILE = configuration.GetSection("Settings")["ORDERED_FILE"];
var TOTALLINES = configuration.GetSection("Settings")["TOTALLINES"];
var CHUNKCOUNT = configuration.GetSection("Settings")["CHUNKCOUNT"];
var MAX_CHUNK_SIZE_BYTES = configuration.GetSection("Settings")["MAX_CHUNK_SIZE_BYTES"];
var MAX_PARALLELSORTERS = configuration.GetSection("Settings")["MAX_PARALLELSORTERS"];


while (!validChoice)
{
    Console.Clear();
    Console.WriteLine("What you chousing?");
    Console.WriteLine("1 Generate File");
    Console.WriteLine("2 Sorting File");

    Console.Write("Your chousing: ");
    userInput = Console.ReadLine();

    switch (userInput)
    {
        case "1":
            validChoice = true;
            await GenerateFileAsync();
            break;
        case "2":
            validChoice = true;
            await SortingFileAsync();
            break;
        default:
            Console.WriteLine("Invalid entry. Please enter 1 or 2.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(); 
            break;
    }

}


async Task GenerateFileAsync()
{
    IGeneraterService generaterService = new GeneraterService();

    var outputPath = GENERATED_FILE;
    bool isOptionIntParsed = int.TryParse(CHUNKCOUNT, out int optionInt);
    bool isOptionLongParsed = long.TryParse(TOTALLINES, out long optionLong);

    if (isOptionIntParsed || isOptionLongParsed)
    {
        Console.WriteLine("Invalid CHUNKCOUNT OR TOTALLINES");
        return;
    }
    var totalLines = optionLong;
    var chunkCount = optionInt;

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

async Task SortingFileAsync()
{

    bool isOptionIntParsed = int.TryParse(CHUNKCOUNT, out int optionInt);
    bool isOptionLongParsed = long.TryParse(TOTALLINES, out long optionLong);

    if (isOptionIntParsed || isOptionLongParsed)
    {
        Console.WriteLine("Invalid CHUNKCOUNT OR TOTALLINES");
        return;
    }
    var maxChunkSizeBytes = optionLong;
    var maxParallelSorters = optionInt;

    var inputFile =  GENERATED_FILE;
    var outputFile = ORDERED_FILE;

    IExternalSortService _externalSortService = new ExternalSortService(maxChunkSizeBytes, maxParallelSorters);

    Console.WriteLine("[Sorting] Starting external sort...");

    var stopwatch = Stopwatch.StartNew();
    List<string> sortedChunkFiles = await _externalSortService.SplitAndSortChunks(inputFile);

    Console.WriteLine($"[Sorting] Chunks created. Elapsed: {stopwatch.Elapsed}");
    Console.WriteLine("[Sorting] Starting k-way merge...");

    await _externalSortService.MergeSortedChunks(sortedChunkFiles, outputFile);

    foreach (var file in sortedChunkFiles)
    {
        File.Delete(file);
    }

    stopwatch.Stop();
    Console.WriteLine($"[Sorting] External sort completed in {stopwatch.Elapsed}.");
}