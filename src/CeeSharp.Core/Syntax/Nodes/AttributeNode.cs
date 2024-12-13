using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes;

public record AttributeNode(
    TypeSyntax Name,
    OptionalSyntax<AttributeArgumentListNode> Arguments) : SyntaxNode;