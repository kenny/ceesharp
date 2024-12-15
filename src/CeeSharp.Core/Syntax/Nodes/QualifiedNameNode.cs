namespace CeeSharp.Core.Syntax.Nodes;

public sealed record QualifiedNameNode(MemberNameNode Left, SyntaxToken Dot, MemberNameNode Right) : MemberNameNode;