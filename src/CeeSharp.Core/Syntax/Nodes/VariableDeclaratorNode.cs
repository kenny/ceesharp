using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record VariableDeclaratorNode(
    SyntaxToken Identifier,
    OptionalSyntax<SyntaxToken> Assign,
    OptionalSyntax<ExpressionNode> Initializer) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        if (Initializer.HasValue)
            yield return Initializer.Element;
    }
}