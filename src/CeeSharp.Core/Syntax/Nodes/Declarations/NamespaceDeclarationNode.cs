using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record NamespaceDeclarationNode(
    SyntaxToken NamespaceKeyword,
    TypeSyntax QualifiedName,
    SyntaxToken OpenBrace,
    ImmutableArray<UsingDirectiveNode> Usings,
    ImmutableArray<DeclarationNode> Declarations,
    SyntaxToken CloseBrace) : DeclarationNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Usings)
            yield return child;

        foreach (var child in Declarations)
            yield return child;
    }
}