namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record ConstructorInitializerNode(
    SyntaxToken Colon,
    SyntaxToken BaseOrThisKeyword,
    SyntaxToken OpenParen,
    SyntaxToken CloseParen) : SyntaxNode;