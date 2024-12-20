using CeeSharp.Core.Syntax.Nodes.Statements;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record VariableDeclarationNode(
    OptionalSyntax<SyntaxToken> ConstKeyword,
    OptionalSyntax<TypeSyntax> Type,
    SeparatedSyntaxList<VariableDeclaratorNode> Declarators) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var declarator in Declarators.Elements)
            yield return declarator;
    }
}