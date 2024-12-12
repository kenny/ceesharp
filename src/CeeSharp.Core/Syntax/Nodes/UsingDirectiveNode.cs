namespace CeeSharp.Core.Syntax.Nodes;

public record UsingDirectiveNode(SyntaxToken UsingKeyword, OptionalSyntax<UsingAliasNode> Alias, SyntaxToken Identifier, SyntaxToken Semicolon) : SyntaxNode;