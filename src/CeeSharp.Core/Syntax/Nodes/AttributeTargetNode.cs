namespace CeeSharp.Core.Syntax.Nodes;

public record AttributeTargetNode(SyntaxToken Identifier, SyntaxToken Colon) : SyntaxNode;