using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record SwitchSectionNode(
    ImmutableArray<SwitchLabelNode> Labels,
    ImmutableArray<StatementNode> Statements) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var label in Labels)
            yield return label;

        foreach (var statement in Statements)
            yield return statement;
    }
}