using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record TryStatementNode(
    SyntaxToken TryKeyword,
    BlockStatementNode Block,
    ImmutableArray<CatchClauseNode> CatchClauses,
    OptionalSyntax<FinallyClauseNode> Finally) : StatementNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Block;

        foreach (var catchClause in CatchClauses)
            yield return catchClause;

        if (Finally.HasValue)
            yield return Finally.Element;
    }
}