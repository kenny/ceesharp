using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax;

public class TokenStream(ImmutableArray<SyntaxToken> tokens)
{
    private int position;

    public SyntaxToken Current => position < tokens.Length
        ? tokens[position]
        : EndOfFile();

    public SyntaxToken Lookahead => position + 1 < tokens.Length
        ? tokens[position + 1]
        : EndOfFile();

    public SyntaxToken Previous => position > 0
        ? tokens[position - 1]
        : new SyntaxToken(TokenKind.Unknown, "", 0);

    public ReadOnlySpan<SyntaxToken> Tokens => tokens.AsSpan();

    public void Advance()
    {
        position++;
    }

    public SyntaxToken Peek(int offset = 0)
    {
        var index = position + offset;
        return index < tokens.Length ? tokens[index] : Current;
    }

    public RestorePoint CreateRestorePoint()
    {
        return new RestorePoint(position);
    }

    public void Restore(RestorePoint restorePoint)
    {
        position = restorePoint.Position;
    }

    private SyntaxToken EndOfFile()
    {
        return new SyntaxToken(TokenKind.EndOfFile, "", tokens.Any() ? tokens[^1].Position + 1 : 0);
    }

    public readonly struct RestorePoint(int position)
    {
        public int Position { get; } = position;
    }
}