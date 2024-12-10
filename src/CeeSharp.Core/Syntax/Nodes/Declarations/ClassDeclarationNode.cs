using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record ClassDeclarationNode(
    ImmutableArray<SyntaxToken> Modifiers,
    SyntaxToken ClassKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenBrace,
    ImmutableArray<DeclarationNode> Declarations,
    SyntaxToken CloseBrace) : DeclarationNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Declarations)
            yield return child;
    }
}