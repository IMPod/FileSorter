namespace SortingFile.Models;

/// <summary>
/// Record struct that holds the parsed information from a line like "123. Apple":
/// - Number = 123
/// - Text   = "Apple"
/// </summary>
public readonly record struct NumberStringLine(long Number, string Text);