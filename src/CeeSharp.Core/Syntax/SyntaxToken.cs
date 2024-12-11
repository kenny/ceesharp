using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax;

public record SyntaxToken(TokenKind Kind, string Text, int Position, object? Value = null)
{
    public int Width => Text.Length;
    public int FullWidth => CalculateFullWidth();
    public int EndTextPosition => Position + Width;
    public int EndPosition => Position + FullWidth;
    public ImmutableArray<SyntaxTrivia> LeadingTrivia { get; set; } = ImmutableArray<SyntaxTrivia>.Empty;
    public ImmutableArray<SyntaxTrivia> TrailingTrivia { get; set; } = ImmutableArray<SyntaxTrivia>.Empty;

    private int CalculateFullWidth()
    {
        SyntaxTrivia? lineTrail = null;

        foreach (var trivia in TrailingTrivia)
            if (trivia.Kind != TriviaKind.EndOfLine)
                lineTrail = trivia;
            else break;

        if (lineTrail == null)
            return Width;

        return Width + lineTrail.EndPosition - EndTextPosition;
    }
}