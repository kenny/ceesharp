using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record SwitchStatementNode(
    SyntaxToken SwitchKeyword,
    SyntaxToken OpenParen,
    ExpressionNode Expression,
    SyntaxToken CloseParen,
    SyntaxToken OpenBrace,
    ImmutableArray<SwitchSectionNode> Sections,
    SyntaxToken CloseBrace) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Expression;

        foreach (var section in Sections)
            yield return section;
    }
}