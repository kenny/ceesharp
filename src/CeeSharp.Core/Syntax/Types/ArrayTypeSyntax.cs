using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes;

namespace CeeSharp.Core.Syntax.Types;

public sealed record ArrayTypeSyntax(
    TypeSyntax ElementType,
    ImmutableArray<ArrayRankSpecifierNode> RankSpecifiers) : TypeSyntax;