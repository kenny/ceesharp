using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Statements;

public sealed record CatchClauseNode(
    SyntaxToken CatchKeyword,
    OptionalSyntax<SyntaxToken> OpenParen,
    OptionalSyntax<TypeSyntax> Type,
    OptionalSyntax<SyntaxToken> Identifier,
    OptionalSyntax<SyntaxToken> CloseParen,
    BlockStatementNode Block) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        yield return Block;
    }
}