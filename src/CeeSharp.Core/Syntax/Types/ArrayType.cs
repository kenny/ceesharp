namespace CeeSharp.Core.Syntax.Types;

public sealed record ArrayTypeSyntax(
    TypeSyntax ElementType,
    SyntaxToken OpenBracket,
    SyntaxToken CloseBracket) : TypeSyntax;