using CeeSharp.Core.Parsing;

namespace CeeSharp.Core.Syntax;

public interface IMemberNode
{
    public static abstract bool IsModifierValid(ParserContext ParserContext, TokenKind modifier);
}