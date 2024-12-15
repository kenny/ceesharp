namespace CeeSharp.Core.Syntax.Nodes;

public sealed record UsingDirectiveNode(
    SyntaxToken UsingKeyword,
    OptionalSyntax<UsingAliasNode> Alias,
    SyntaxToken Identifier,
    SyntaxToken Semicolon) : SyntaxNode;