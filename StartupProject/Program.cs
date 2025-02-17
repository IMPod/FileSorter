﻿using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using FileGenerator.Models;
using FileGenerator.Services;
using SortingFile.Services;
using FileSorter.StartupProject.Models;


var validChoice = false;

var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var configuration = builder.Build();

var GENERATED_FILE = configuration.GetSection("Settings")["GENERATED_FILE"];
var ORDERED_FILE = configuration.GetSection("Settings")["ORDERED_FILE"];
var TOTALLINES = configuration.GetSection("Settings")["TOTALLINES"];
var CHUNKCOUNT = configuration.GetSection("Settings")["CHUNKCOUNT"];
var MAX_CHUNK_SIZE_BYTES = configuration.GetSection("Settings")["MAX_CHUNK_SIZE_BYTES"];
var MAX_PARALLELSORTERS = configuration.GetSection("Settings")["MAX_PARALLELSORTERS"];

var _cancellationToken = new CancellationToken();
while (!validChoice)
{
    Console.Clear();
    Console.WriteLine("What you chousing?");
    Console.WriteLine("1 Generate File");
    Console.WriteLine("2 Sorting File");

    Console.Write("Your chousing: ");
    var userInput = Console.ReadLine()!;

    switch (userInput)
    {
        case "1":
            validChoice = true;
            await GenerateFileAsync();
            break;
        case "2":
            validChoice = true;
            await SortingFileAsync(_cancellationToken);
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
    var isOptionIntParsed = int.TryParse(CHUNKCOUNT, out var optionInt);
    var isOptionLongParsed = long.TryParse(TOTALLINES, out var optionLong);

    if (!isOptionIntParsed || !isOptionLongParsed)
    {
        Console.WriteLine("Invalid CHUNKCOUNT OR TOTALLINES");
        return;
    }

    var config = new GeneratorConfig(optionLong, outputPath, optionInt);
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

async Task SortingFileAsync(CancellationToken cancellationToken)
{

    int.TryParse(MAX_PARALLELSORTERS, out var maxParallelSorters);
    long.TryParse(MAX_CHUNK_SIZE_BYTES, out var maxChunkSizeBytes);

    var config = new SortingConfig(GENERATED_FILE!, ORDERED_FILE!, maxChunkSizeBytes, maxParallelSorters);
    config.Validate();


    IExternalSortService externalSortService = new ExternalSortService(config);

    Console.WriteLine("[Sorting] Starting external sort...");

    
    var result = await externalSortService.SplitAndSortChunks(config.InputPath, cancellationToken);

    if (result)
        Console.WriteLine("[Sorting] Done!");
}