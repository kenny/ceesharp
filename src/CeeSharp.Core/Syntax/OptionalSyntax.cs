using System.Diagnostics.CodeAnalysis;

namespace CeeSharp.Core.Syntax;

public readonly struct OptionalSyntax<TElement>(TElement? element)
    where TElement : SyntaxElement
{
    public TElement? Element { get; } = element;

    [MemberNotNullWhen(true, nameof(Element))]
    public bool HasValue => Element != null;

    public static readonly OptionalSyntax<TElement> None = new();

    public static implicit operator OptionalSyntax<TElement>(TElement element)
    {
        return new OptionalSyntax<TElement>(element);
    }
}

public static class OptionalSyntax
{
    public static OptionalSyntax<TElement> With<TElement>(TElement? element)
        where TElement : SyntaxElement
    {
        return element == null ? OptionalSyntax<TElement>.None : new OptionalSyntax<TElement>(element);
    }
}