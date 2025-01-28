namespace SortingFile.Models;

/// <summary>
/// A custom comparer that sorts by Text (lexicographically, Ordinal),
/// and if the Text is equal, then sorts by Number ascending.
/// </summary>
public class NumberStringLineComparer : IComparer<NumberStringLine>
{
    public int Compare(NumberStringLine x, NumberStringLine y)
    {
        int cmp = string.CompareOrdinal(x.Text, y.Text);
        if (cmp != 0)
            return cmp;

        return x.Number.CompareTo(y.Number);
    }
}