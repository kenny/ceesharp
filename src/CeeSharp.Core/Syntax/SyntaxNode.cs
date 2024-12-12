namespace CeeSharp.Core.Syntax;

public abstract record SyntaxNode : SyntaxElement
{
    public virtual IEnumerable<SyntaxNode> GetChildren()
    {
        yield break;
    }
}