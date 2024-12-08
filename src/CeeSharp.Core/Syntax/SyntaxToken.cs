using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax;

public class SyntaxToken(TokenKind kind, string text, int position, object? value = null)
{
    public TokenKind Kind { get; } = kind;
    public string Text { get; } = text;
    public object? Value { get; } = value;
    public int Position { get; } = position;
    public ImmutableArray<SyntaxTrivia> LeadingTrivia { get; set; } = [];
    public ImmutableArray<SyntaxTrivia> TrailingTrivia { get; set; } = [];
}