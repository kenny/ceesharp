using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record IncompleteMemberDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxElement> Elements) : MemberDeclarationNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.Incomplete;
}