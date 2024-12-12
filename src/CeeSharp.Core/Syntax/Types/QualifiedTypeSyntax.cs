namespace CeeSharp.Core.Syntax.Types;

public sealed record QualifiedTypeSyntax(
    TypeSyntax Left,
    SyntaxToken Dot,
    SimpleTypeSyntax Right) : TypeSyntax;