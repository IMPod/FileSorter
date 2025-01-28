namespace SortingFile.Models;

/// <summary>
/// Adapter to let PriorityQueue compare NumberStringLine using our custom comparer.
/// </summary>
public class PriorityLineComparerAdapter : IComparer<NumberStringLine>
{
    private readonly NumberStringLineComparer _baseComparer;

    public PriorityLineComparerAdapter(NumberStringLineComparer baseComparer)
    {
        _baseComparer = baseComparer;
    }

    public int Compare(NumberStringLine x, NumberStringLine y)
    {
        return _baseComparer.Compare(x, y);
    }
}
