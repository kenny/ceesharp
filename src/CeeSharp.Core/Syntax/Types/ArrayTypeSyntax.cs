using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes;
using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Types;

public sealed record ArrayTypeSyntax(
    TypeSyntax ElementType,
    ImmutableArray<ArrayRankSpecifierNode> RankSpecifiers) : TypeSyntax
{
    public bool IsValidType
    {
        get;
        init;
    }
}