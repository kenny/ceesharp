namespace CeeSharp.Core.Syntax.Nodes;

public record QualifiedNameNode(MemberNameNode Left, SyntaxToken Dot, MemberNameNode Right) : MemberNameNode;