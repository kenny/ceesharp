namespace CeeSharp.Core.Syntax;

public interface IMemberNode
{
    public static abstract bool IsModifierValid(DeclarationKind declarationContext, TokenKind modifier);
}