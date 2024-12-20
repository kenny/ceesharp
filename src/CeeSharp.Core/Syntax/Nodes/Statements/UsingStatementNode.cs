using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record UsingStatementNode(
    SyntaxToken UsingKeyword,
    SyntaxToken OpenParen,
    SyntaxUnion<VariableDeclarationNode, ExpressionNode> Declaration,
    SyntaxToken CloseParen,
    StatementNode Statement) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        if (Declaration.IsLeft)
            yield return Declaration.LeftValue;
         
        if(Declaration.IsRight)
            yield return Declaration.RightValue;
        
        yield return Statement;
    }
}