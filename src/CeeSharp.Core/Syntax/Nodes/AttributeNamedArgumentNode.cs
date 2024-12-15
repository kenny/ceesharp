namespace CeeSharp.Core.Syntax.Nodes;

public sealed record AttributeNamedArgumentNode(SyntaxToken Name, SyntaxToken Assign) : SyntaxNode;