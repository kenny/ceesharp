namespace CeeSharp.Core.Syntax;

public abstract record SyntaxNode
{
    public virtual IEnumerable<SyntaxNode> GetChildren()
    {
        yield break;
    }
}