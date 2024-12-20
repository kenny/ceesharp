using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record ObjectCreationExpressionNode(
    SyntaxToken NewKeyword,
    OptionalSyntax<TypeSyntax> Type,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ArgumentNode> Arguments,
    SyntaxToken CloseParen) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Arguments.Elements)
            yield return child;
    }
}