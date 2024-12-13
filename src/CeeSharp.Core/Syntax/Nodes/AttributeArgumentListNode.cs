namespace CeeSharp.Core.Syntax.Nodes;

public record AttributeArgumentListNode(
    SyntaxToken OpenParen,
    SeparatedSyntaxList<AttributeArgumentNode> Arguments,
    SyntaxToken CloseParen) : SyntaxNode;