using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record SeparatedSyntaxList<TNode>(ImmutableArray<TNode> Elements, ImmutableArray<SyntaxToken> Separators)
    : SyntaxElement
    where TNode : SyntaxNode
{
    public static SeparatedSyntaxList<TNode> Empty { get; } = new(
        ImmutableArray<TNode>.Empty,
        ImmutableArray<SyntaxToken>.Empty);

    public IEnumerable<(TNode, SyntaxToken?)> GetSeparatedElements()
    {
        var separatorIndex = 0;

        foreach (var node in Elements)
        {
            yield return (node, separatorIndex < Separators.Length ? Separators[separatorIndex] : null);

            separatorIndex++;
        }
    }
}