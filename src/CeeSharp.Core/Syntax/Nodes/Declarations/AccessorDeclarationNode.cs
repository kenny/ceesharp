using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes.Statements;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record AccessorDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    OptionalSyntax<SyntaxToken> Keyword,
    OptionalSyntax<BlockStatementNode> Body,
    OptionalSyntax<SyntaxToken> Semicolon) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        if (Body.HasValue)
            yield return Body.Element;
    }
}