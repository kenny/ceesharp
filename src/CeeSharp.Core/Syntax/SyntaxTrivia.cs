namespace CeeSharp.Core.Syntax;

public abstract record SyntaxTrivia(TriviaKind Kind, int Position)
{
    public abstract int Width { get; }
    public int EndPosition => Position + Width;
}

public record TextSyntaxTrivia(TriviaKind Kind, string Text, int Position) : SyntaxTrivia(Kind, Position)
{
    public override int Width => Text.Length;
}

public record TokenSyntaxTrivia(TriviaKind Kind, SyntaxToken Token, int Position) : SyntaxTrivia(Kind, Position)
{
    public override int Width => Token.Width;
}