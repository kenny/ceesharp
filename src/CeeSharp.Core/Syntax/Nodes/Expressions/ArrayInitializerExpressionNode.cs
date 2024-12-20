namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record ArrayInitializerExpressionNode(
    SyntaxToken OpenBrace,
    SeparatedSyntaxList<ExpressionNode> Expressions,
    SyntaxToken CloseBrace) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var expression in Expressions.Elements)
            yield return expression;
    }
} 