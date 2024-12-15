namespace CeeSharp.Core.Syntax.Nodes;

public sealed record AttributeSectionNode(
    SyntaxToken OpenBracket,
    OptionalSyntax<AttributeTargetNode> Target,
    SeparatedSyntaxList<AttributeNode> AttributeList,
    SyntaxToken CloseBracket) : SyntaxNode;