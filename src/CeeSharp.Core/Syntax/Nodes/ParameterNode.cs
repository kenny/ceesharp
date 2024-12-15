using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record ParameterNode(
    ImmutableArray<SyntaxToken> Modifiers,
    TypeSyntax Type,
    SyntaxToken Identifier) : SyntaxNode;