namespace CeeSharp.Core.Text;

public class SourceText
{
    private readonly List<int> lineStarts;
    private readonly string text;

    public SourceText(string text)
    {
        this.text = text;
        lineStarts = [0];

        CalculateLineStarts();
    }

    public int Length => text.Length;

    public char this[int index] => text[index];

    private void CalculateLineStarts()
    {
        for (var i = 0; i < text.Length; i++)
            if (text[i] == '\n')
                lineStarts.Add(i + 1);
    }

    public string GetText(int start, int length)
    {
        return text.Substring(start, length);
    }

    public string GetText(TextSpan span)
    {
        return GetText(span.Start, span.Length);
    }

    public (int line, int column) GetLinePosition(int position)
    {
        var line = lineStarts.BinarySearch(position);
        if (line < 0) line = ~line - 1;
        return (line + 1, position - lineStarts[line] + 1);
    }

    public override string ToString()
    {
        return text;
    }
}