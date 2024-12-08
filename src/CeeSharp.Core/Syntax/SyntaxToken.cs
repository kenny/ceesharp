using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax;

public class SyntaxToken(TokenKind kind, string text, int position, object? value = null)
{
    public TokenKind Kind { get; } = kind;
    public string Text { get; } = text;
    public object? Value { get; } = value;
    public int Width => Text.Length;
    public int FullWidth => CalculateFullWidth();
    public int Position { get; } = position;
    public int EndTextPosition => Position + Width;
    public int EndPosition => Position + FullWidth;
    public ImmutableArray<SyntaxTrivia> LeadingTrivia { get; set; } = ImmutableArray<SyntaxTrivia>.Empty;
    public ImmutableArray<SyntaxTrivia> TrailingTrivia { get; set; } = ImmutableArray<SyntaxTrivia>.Empty;
    
    private int CalculateFullWidth()
    {
        if (TrailingTrivia.Length == 0)
            return 0;
        
        var trailingTrivia = TrailingTrivia.Last();

        return Width + trailingTrivia.EndPosition - EndTextPosition;
    }
}