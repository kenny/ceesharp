using CeeSharp.Core.Parsing;

namespace CeeSharp.Core.Syntax;

public interface IModifierValidator
{
    public static abstract bool IsModifierValid(ParserContext parserContext, TokenKind modifier);
}