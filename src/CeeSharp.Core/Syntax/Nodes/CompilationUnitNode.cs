using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes.Declarations;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record CompilationUnitNode(
    ImmutableArray<UsingDirectiveNode> Usings,
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<DeclarationNode> Declarations,
    SyntaxToken EndOfFileToken) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Usings)
            yield return child;

        foreach (var child in Declarations)
            yield return child;
    }
}