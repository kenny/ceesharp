namespace CeeSharp.Core.Syntax.Nodes;

public sealed record ExplicitInterfaceNode(MemberNameNode Name, OptionalSyntax<SyntaxToken> Dot) : SyntaxNode;