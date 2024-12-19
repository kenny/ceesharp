using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record EventFieldDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken EventKeyword,
    TypeSyntax Type,
    SeparatedSyntaxList<VariableDeclaratorNode> Declarators,
    SyntaxToken Semicolon) : MemberDeclarationNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        foreach (var child in Declarators.Elements)
            yield return child;
    }
}