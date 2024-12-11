using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record NamespaceDeclarationNode(
    SyntaxToken NamespaceKeyword,
    SyntaxToken Identifier,
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
    
    public override DeclarationKind DeclarationKind => DeclarationKind.Namespace;
}