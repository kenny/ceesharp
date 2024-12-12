namespace CeeSharp.Core.Syntax.Nodes;

public record BlockNode(SyntaxToken OpenBrace, SyntaxToken CloseBrace) : SyntaxNode;