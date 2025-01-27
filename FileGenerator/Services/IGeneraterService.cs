﻿using FileGenerator.Models;

namespace FileGenerator.Services;

internal interface IGeneraterService
{
    Task<string[]> GenerateChunksInParallel(GeneratorConfig config);
    Task ConcatenateFiles(string[] files, string outputPath);
}
