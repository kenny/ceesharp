namespace CeeSharp.Core.Syntax.Types;

public record PointerTypeSyntax(
    TypeSyntax Type, 
    SyntaxToken AsteriskToken) : TypeSyntax;