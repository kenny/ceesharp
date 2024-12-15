namespace CeeSharp.Core.Syntax.Nodes;

public sealed record AttributeArgumentListNode(
    SyntaxToken OpenParen,
    SeparatedSyntaxList<AttributeArgumentNode> Arguments,
    SyntaxToken CloseParen) : SyntaxNode;