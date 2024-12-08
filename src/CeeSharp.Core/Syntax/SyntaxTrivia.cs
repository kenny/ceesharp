namespace CeeSharp.Core.Syntax;

public class SyntaxTrivia(string text, TriviaKind kind, int position)
{
    public string Text { get; } = text;
    public TriviaKind Kind { get; } = kind;
    public int Position { get; } = position;
}