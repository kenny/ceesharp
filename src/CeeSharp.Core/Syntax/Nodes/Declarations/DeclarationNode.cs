namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public abstract record DeclarationNode : SyntaxNode
{
    public abstract DeclarationKind DeclarationKind { get; }
}