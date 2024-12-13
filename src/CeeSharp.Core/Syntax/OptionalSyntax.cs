using System.Diagnostics.CodeAnalysis;

namespace CeeSharp.Core.Syntax;

public readonly struct OptionalSyntax<TElement>(TElement? element)
    where TElement : SyntaxElement
{
    public TElement? Element { get; } = element;
    
    [MemberNotNullWhen(true, nameof(Element))]
    public bool HasValue => Element != null;

    public static readonly OptionalSyntax<TElement> None = new();
}

public static class OptionalSyntax
{
    public static OptionalSyntax<TElement> With<TElement>(TElement element) 
        where TElement : SyntaxElement
    {
        return new OptionalSyntax<TElement>(element);
    }
}