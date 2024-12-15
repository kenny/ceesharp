using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record AttributeNode(
    TypeSyntax Name,
    OptionalSyntax<AttributeArgumentListNode> Arguments) : SyntaxNode;