using CeeSharp.Core.Syntax.Nodes.Expressions;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record ForStatementNode(
    SyntaxToken ForKeyword,
    SyntaxToken OpenParen,
    VariableDeclarationOrInitializer Initializer,
    SyntaxToken FirstSemicolon,
    OptionalSyntax<ExpressionNode> Condition,
    SyntaxToken SecondSemicolon,
    SeparatedSyntaxList<ExpressionNode> Iterator,
    SyntaxToken CloseParen,
    StatementNode Statement) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        if (Initializer.IsLeft)
            yield return Initializer.LeftValue;
        
        if(Initializer.IsRight)
            foreach (var child in Initializer.RightValue.Elements)
                yield return child;

        if (Condition.HasValue)
            yield return Condition.Element;
        
        foreach (var child in Iterator.Elements)
            yield return child;

        yield return Statement;
    }
}