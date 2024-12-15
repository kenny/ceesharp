namespace CeeSharp.Core.Syntax.Nodes;

public record AttributeNamedArgumentNode(SyntaxToken Name, SyntaxToken Assign) : SyntaxNode;