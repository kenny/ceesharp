using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record IncompleteMemberDeclarationNode(ImmutableArray<SyntaxElement> Elements) : MemberDeclarationNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.None;
}