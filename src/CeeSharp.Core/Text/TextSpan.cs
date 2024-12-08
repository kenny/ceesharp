namespace CeeSharp.Core.Text;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool OverlapsWith(TextSpan other)
    {
        var start = Math.Max(Start, other.Start);
        var end = Math.Min(End, other.End);

        return start < end;
    }

    public TextSpan? Intersection(TextSpan other)
    {
        var start = Math.Max(Start, other.Start);
        var end = Math.Min(End, other.End);
        return start <= end ? new TextSpan(start, end - start) : null;
    }
}