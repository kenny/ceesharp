namespace CeeSharp.Core.Syntax.Nodes;

public sealed record UsingDirectiveNode(
    SyntaxToken UsingKeyword,
    OptionalSyntax<UsingAliasNode> Alias,
    MemberNameNode Name,
    SyntaxToken Semicolon) : SyntaxNode;