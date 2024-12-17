using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record IncompleteMemberDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxElement> Elements) : MemberDeclarationNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.Incomplete;

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;
    }
}