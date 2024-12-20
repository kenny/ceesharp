using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record ArrayCreationExpressionNode(
    SyntaxToken NewKeyword,
    TypeSyntax ElementType,
    ImmutableArray<ArrayRankSpecifierNode> RankSpecifiers,
    OptionalSyntax<ArrayInitializerExpressionNode> Initializer) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in RankSpecifiers)
            yield return child;

        if (Initializer.HasValue)
            yield return Initializer.Element;
    }
}