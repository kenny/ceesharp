namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public record ElementAccessExpressionNode(
    ExpressionNode Expression,
    SyntaxToken OpenBracket,
    SeparatedSyntaxList<ArgumentNode> Arguments,
    SyntaxToken CloseBracket) : ExpressionNode;