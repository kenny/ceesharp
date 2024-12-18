namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record ConstructorInitializerNode(
    SyntaxToken Colon,
    SyntaxToken BaseOrThisKeyword,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ArgumentNode> Arguments,
    SyntaxToken CloseParen) : SyntaxNode
{
    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Arguments.Elements)
            yield return child;
    }
}