namespace CeeSharp.Core.Syntax.Nodes;

public sealed record BlockNode(SyntaxToken OpenBrace, SyntaxToken CloseBrace) : SyntaxNode;