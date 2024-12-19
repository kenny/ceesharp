using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record SeparatedSyntaxList<TElement>(ImmutableArray<TElement> Elements, ImmutableArray<SyntaxToken> Separators)
    : SyntaxElement
    where TElement : SyntaxElement
{
    public static SeparatedSyntaxList<TElement> Empty { get; } = new(
        ImmutableArray<TElement>.Empty,
        ImmutableArray<SyntaxToken>.Empty);

    public IEnumerable<(TElement, SyntaxToken?)> GetSeparatedElements()
    {
        var separatorIndex = 0;

        foreach (var node in Elements)
        {
            yield return (node, separatorIndex < Separators.Length ? Separators[separatorIndex] : null);

            separatorIndex++;
        }
    }
}