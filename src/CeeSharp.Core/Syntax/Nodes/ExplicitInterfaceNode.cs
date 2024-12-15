namespace CeeSharp.Core.Syntax.Nodes;

public record ExplicitInterfaceNode(MemberNameNode Name, OptionalSyntax<SyntaxToken> Dot) : SyntaxNode;