using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record StackAllocExpressionNode(
    SyntaxToken StackallocKeyword,
    TypeSyntax ElementType,
    SyntaxToken OpenBracket,
    ExpressionNode Size,
    SyntaxToken CloseBracket) : ExpressionNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Size;
    }
}