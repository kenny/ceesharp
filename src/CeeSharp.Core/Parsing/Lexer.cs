using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CeeSharp.Core.Syntax;
using CeeSharp.Core.Text;

namespace CeeSharp.Core.Parsing;

public sealed class Lexer(Diagnostics diagnostics, SourceText sourceText)
{
    private readonly StringBuilder literalBuilder = new();
    private int position;

    private char Current => GetCharAt(position);
    private char Lookahead => Peek(1);

    public TokenStream Tokenize()
    {
        var tokens = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (position < sourceText.Length)
        {
            var token = NextToken();

            if (token.Kind == TokenKind.EndOfFile)
                break;

            tokens.Add(token);
        }

        return new TokenStream(tokens.ToImmutable());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char Peek(int lookahead)
    {
        var index = position + lookahead;
        return GetCharAt(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char GetCharAt(int offset)
    {
        return offset >= sourceText.Length ? '\0' : sourceText[offset];
    }

    private void Advance(int offset = 1)
    {
        if (position + offset > sourceText.Length)
            return;

        position += offset;
    }

    private SyntaxToken NextToken()
    {
        var leadingTrivia = ScanTrivia();

        var token = ScanToken();

        token.LeadingTrivia = leadingTrivia;

        if (token.Kind != TokenKind.EndOfFile)
            token.TrailingTrivia = ScanTrivia();

        return token;
    }

    private SyntaxToken ScanToken()
    {
        var start = position;

        var current = Current;

        switch (current)
        {
            case '\0': // End of file
                return new SyntaxToken(TokenKind.EndOfFile, "", position);

            case '{':
                Advance();
                return new SyntaxToken(TokenKind.LeftBrace, "{", start);
            case '}':
                Advance();
                return new SyntaxToken(TokenKind.RightBrace, "}", start);

            case '(':
                Advance();
                return new SyntaxToken(TokenKind.LeftParen, "(", start);
            case ')':
                Advance();
                return new SyntaxToken(TokenKind.RightParen, ")", start);

            case '[':
                Advance();
                return new SyntaxToken(TokenKind.LeftBracket, "[", start);
            case ']':
                Advance();
                return new SyntaxToken(TokenKind.RightBracket, "]", start);

            case ';':
                Advance();
                return new SyntaxToken(TokenKind.Semicolon, ";", start);

            case ',':
                Advance();
                return new SyntaxToken(TokenKind.Comma, ",", start);

            case '.':
                Advance();

                if (!char.IsDigit(current))
                    return new SyntaxToken(TokenKind.Dot, ".", start);

                position--;
                return ScanNumberLiteral();

            case ':':
                Advance();
                return new SyntaxToken(TokenKind.Colon, ":", start);

            case '?':
                Advance();
                return new SyntaxToken(TokenKind.Question, "?", start);

            case '!' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.NotEqual, "!=", start);
            case '!':
                Advance();
                return new SyntaxToken(TokenKind.Exclamation, "!", start);

            case '=' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.Equal, "==", start);
            case '=':
                Advance();
                return new SyntaxToken(TokenKind.Assign, "=", start);

            case '+' when Lookahead == '+':
                position += 2;
                return new SyntaxToken(TokenKind.PlusPlus, "++", start);
            case '+' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.PlusAssign, "+=", start);
            case '+':
                Advance();
                return new SyntaxToken(TokenKind.Plus, "+", start);

            case '-' when Lookahead == '-':
                position += 2;
                return new SyntaxToken(TokenKind.MinusMinus, "--", start);
            case '-' when Lookahead == '>':
                position += 2;
                return new SyntaxToken(TokenKind.Arrow, "->", start);
            case '-' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.MinusAssign, "-=", start);
            case '-':
                Advance();
                return new SyntaxToken(TokenKind.Minus, "-", start);

            case '*' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.TimesAssign, "*=", start);
            case '*':
                Advance();
                return new SyntaxToken(TokenKind.Times, "*", start);

            case '/' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.DivideAssign, "/=", start);
            case '/':
                Advance();
                return new SyntaxToken(TokenKind.Divide, "*", start);

            case '%' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.ModuloAssign, "%=", start);
            case '%':
                Advance();
                return new SyntaxToken(TokenKind.Modulo, "%", start);

            case '<' when Lookahead == '<':
                position += 2;
                return new SyntaxToken(TokenKind.LeftShift, "<<", start);
            case '>' when Lookahead == '>' && Peek(2) == '=':
                position += 3;
                return new SyntaxToken(TokenKind.LeftShiftAssign, "<<=", start);
            case '<' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.LessThanOrEqual, "<=", start);
            case '<':
                Advance();
                return new SyntaxToken(TokenKind.LessThan, "<", start);

            case '>' when Lookahead == '>':
                position += 2;
                return new SyntaxToken(TokenKind.RightShift, ">>", start);
            case '>' when Lookahead == '>' && Peek(2) == '=':
                position += 3;
                return new SyntaxToken(TokenKind.RightShiftAssign, ">>=", start);
            case '>' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.GreaterThanOrEqual, ">=", start);
            case '>':
                Advance();
                return new SyntaxToken(TokenKind.GreaterThan, ">", start);

            case '&' when Lookahead == '&':
                position += 2;
                return new SyntaxToken(TokenKind.AndAnd, "&&", start);
            case '&' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.AndAssign, "&=", start);
            case '&':
                Advance();
                return new SyntaxToken(TokenKind.Ampersand, "&", start);

            case '|' when Lookahead == '|':
                position += 2;
                return new SyntaxToken(TokenKind.OrOr, "||", start);
            case '|' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.OrAssign, "|=", start);
            case '|':
                Advance();
                return new SyntaxToken(TokenKind.Pipe, "|", start);

            case '^' when Lookahead == '=':
                position += 2;
                return new SyntaxToken(TokenKind.XorAssign, "^=", start);
            case '^':
                Advance();
                return new SyntaxToken(TokenKind.Xor, "^", start);

            case '~':
                Advance();
                return new SyntaxToken(TokenKind.Tilde, "~", start);

            case '\'':
                return ScanCharacterLiteral();
            case '@':
            case '"':
                return ScanStringLiteral();
            default:
                if (char.IsLetter(current) || current == '_')
                    return ScanIdentifierOrKeyword();
                if (char.IsDigit(current))
                    return ScanNumberLiteral();

                Advance();
                return new SyntaxToken(TokenKind.Unknown, current.ToString(), start);
        }
    }

    private SyntaxToken ScanCharacterLiteral()
    {
        var start = position;
        
        Advance(); // Skip '

        literalBuilder.Clear();

        switch (Current)
        {
            case '\'':
                diagnostics.ReportError(start, "Empty character literal");
                Advance(); // Skip '
                return new SyntaxToken(TokenKind.CharacterLiteral, "", position);
            case '\\':
                ScanEscapeCharacter();
                break;
        }

        Advance(); // Advance

        if (Current is not '\'' and not '\0' || literalBuilder.Length > 1)
        {
            diagnostics.ReportError(position, "Too many characters in character literal");

            while (Current is not '\'' and not '\0')
            {
                if (Current == '\\')
                    ScanEscapeCharacter();

                Advance();
            }
        }

        Advance(); // Skip '

        var length = position - start;
        var text = sourceText.GetText(start, length);

        var validLiteral = char.TryParse(sourceText.GetText(start + 1, length - 2), out var literalValue);

        return new SyntaxToken(TokenKind.CharacterLiteral, text, start, validLiteral ? literalValue : null);
    }

    private SyntaxToken ScanStringLiteral()
    {
        var start = position;
        var isVerbatim = false;

        literalBuilder.Clear();
        
        if (Current == '@')
        {
            isVerbatim = true;
            Advance(); // Skip @
        }
        
        Advance(); // Skip "

        while (Current is not '\0')
        {
            var current = Current;

            if (!isVerbatim)
            {
                if (current is '\r' or '\n')
                {
                    diagnostics.ReportError(start, "Newline in constant");
                    break;
                }

                if (current == '\\')
                    ScanEscapeCharacter();
            }
            else if (isVerbatim && current == '"' && Peek(1) == '"')
            {
                literalBuilder.Append('"');
                
                Advance(); // Skip "
                
                continue;
            }
            else if (current == '"')
                break;
            
            literalBuilder.Append(current);

            Advance();
        }

        if (Current is '"' or '\r' or '\n')
            Advance(); // Skip "
        else
            diagnostics.ReportError(start, "Unterminated string literal");

        var length = position - start;
        var text = sourceText.GetText(start, length);

        return new SyntaxToken(TokenKind.StringLiteral, text, start, literalBuilder.ToString());
    }

    private void ScanEscapeCharacter()
    {
        long value;

        var start = position;

        Advance();

        switch (Current)
        {
            case '\"':
                literalBuilder.Append('"');
                return;
            case '\\':
                literalBuilder.Append('\\');
                return;
            case '\'':
                literalBuilder.Append('\'');
                return;
            case '0':
                literalBuilder.Append('\0');
                return;
            case 'a':
                literalBuilder.Append('\a');
                return;
            case 'b':
                literalBuilder.Append('\b');
                return;
            case 'f':
                literalBuilder.Append('\f');
                return;
            case 'n':
                literalBuilder.Append('\n');
                return;
            case 'r':
                literalBuilder.Append('\r');
                return;
            case 't':
                literalBuilder.Append('\t');
                return;
            case 'v':
                literalBuilder.Append('\v');
                return;
            case 'x':
                Advance(); // Skip x
                value = ScanHexadecimalEscape(4);
                if (value == -1)
                    break;
                literalBuilder.Append((char)value);
                position--;
                return;
            case 'u':
                Advance(); // Skip u
                value = ScanHexadecimalEscape(4, true);
                if (value == -1)
                    break;
                literalBuilder.Append((char)value);
                position--;
                return;
            case 'U':
                Advance(); // Skip U
                value = ScanHexadecimalEscape(8, true);
                switch (value)
                {
                    case -1:
                    case > 0x10FFFF:
                        position--;
                        break;
                    case <= 0xFFFF:
                        literalBuilder.Append((char)value);
                        position--;
                        return;
                    default:
                        value -= 0x10000;
                        literalBuilder.Append((char)(0xD800 + (value >> 10))); // High surrogate
                        literalBuilder.Append((char)(0xDC00 + (value & 0x3FF))); // Low surrogate
                        position--;
                        return;
                }

                break;
        }

        diagnostics.ReportError(start, "Unrecognized escape sequence");
    }

    private long ScanHexadecimalEscape(int maxDigits, bool exact = false)
    {
        long value = 0;
        var digitCount = 0;

        while (digitCount < maxDigits)
        {
            var c = Current;
            int digit;

            if (c is >= '0' and <= '9')
                digit = c - '0';
            else if (c is >= 'a' and <= 'f')
                digit = c - 'a' + 10;
            else if (c is >= 'A' and <= 'F')
                digit = c - 'A' + 10;
            else
                break;

            value = value * 16 + digit;
            digitCount++;
            Advance();
        }

        if (digitCount == 0 || (exact && digitCount != maxDigits))
            return -1;

        return value;
    }

    private SyntaxToken ScanNumberLiteral()
    {
        var start = position;
        var isInvalid = false;
        var isHex = false;
        
        if (Current == '0' && Lookahead is 'x' or 'X')
        {
            Advance(2); // Skip 0x

            isHex = true;
            
            if (!IsHexDigit(Current))
                isInvalid = true;
            else
                do
                {
                    Advance();
                } while (IsHexDigit(Current));
        }
        else
        {
            while (char.IsDigit(Current))
                Advance();

            if (Current == '.')
            {
                Advance();

                if (!char.IsDigit(Current))
                    isInvalid = true;
                else
                    do
                    {
                        Advance();
                    } while (char.IsDigit(Current));
            }

            if (Current is 'e' or 'E')
            {
                Advance();

                if (Current is '+' or '-')
                    Advance();

                if (!char.IsDigit(Current))
                    isInvalid = true;
                else
                    do
                    {
                        Advance();
                    } while (char.IsDigit(Current));
            }
        }

        if (Current is 'f' or 'F' or 'd' or 'D' or 'M' or 'm' or 'l' or 'L' or 'u' or 'U')
        {
            var suffixStart = position;
            Advance();

            if ((Current is 'u' or 'U' && sourceText[suffixStart] is 'l' or 'L') ||
                (Current is 'l' or 'L' && sourceText[suffixStart] is 'u' or 'U'))
                Advance();
        }

        var length = position - start;
        var text = sourceText.GetText(start, length);

        object? value = null;

        if (isInvalid)
            diagnostics.ReportError(start, "Invalid number");
        else
            value = ParseNumericValue(text, isHex);

        return new SyntaxToken(TokenKind.NumericLiteral, text, start, value);

        static bool IsHexDigit(char c)
        {
            return char.IsDigit(c) || c is >= 'a' and <= 'f' || c is >= 'A' and <= 'F';
        }
    }

    private static object ParseNumericValue(string text, bool isHex = false)
    {
        var upperText = text.ToUpperInvariant();

        if (isHex)
            text = text[2..];

        var numberStyle = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;

        if (upperText.EndsWith("UL") || upperText.EndsWith("LU"))
            return ulong.Parse(text[..^2]);
        if (upperText.EndsWith('U'))
            return uint.Parse(text[..^1]);
        if (upperText.EndsWith('L'))
            return long.Parse(text[..^1]);

        if (upperText.EndsWith('F'))
            return float.Parse(text[..^1]);
        if (upperText.EndsWith('D'))
            return double.Parse(text[..^1]);
        if (upperText.EndsWith('M'))
            return decimal.Parse(text[..^1]);

        if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
            return double.Parse(text);

        if (int.TryParse(text, numberStyle, null, out var intValue))
            return intValue;
        if (long.TryParse(text, numberStyle, null, out var longValue))
            return longValue;

        return ulong.Parse(text, numberStyle);
    }

    private SyntaxToken ScanIdentifierOrKeyword()
    {
        var start = position;
        while (char.IsLetterOrDigit(Current) || Current == '_')
            Advance();

        var length = position - start;
        var text = sourceText.GetText(start, length);

        var kind = text switch
        {
            "abstract" => TokenKind.Abstract,
            "as" => TokenKind.As,
            "base" => TokenKind.Base,
            "bool" => TokenKind.Bool,
            "break" => TokenKind.Break,
            "byte" => TokenKind.Byte,
            "case" => TokenKind.Case,
            "catch" => TokenKind.Catch,
            "char" => TokenKind.Char,
            "checked" => TokenKind.Checked,
            "class" => TokenKind.Class,
            "const" => TokenKind.Const,
            "continue" => TokenKind.Continue,
            "decimal" => TokenKind.Decimal,
            "default" => TokenKind.Default,
            "delegate" => TokenKind.Delegate,
            "do" => TokenKind.Do,
            "double" => TokenKind.Double,
            "else" => TokenKind.Else,
            "enum" => TokenKind.Enum,
            "event" => TokenKind.Event,
            "explicit" => TokenKind.Explicit,
            "extern" => TokenKind.Extern,
            "false" => TokenKind.False,
            "finally" => TokenKind.Finally,
            "fixed" => TokenKind.Fixed,
            "float" => TokenKind.Float,
            "for" => TokenKind.For,
            "foreach" => TokenKind.Foreach,
            "goto" => TokenKind.Goto,
            "if" => TokenKind.If,
            "implicit" => TokenKind.Implicit,
            "in" => TokenKind.In,
            "int" => TokenKind.Int,
            "interface" => TokenKind.Interface,
            "internal" => TokenKind.Internal,
            "is" => TokenKind.Is,
            "lock" => TokenKind.Lock,
            "long" => TokenKind.Long,
            "namespace" => TokenKind.Namespace,
            "new" => TokenKind.New,
            "null" => TokenKind.Null,
            "object" => TokenKind.Object,
            "operator" => TokenKind.Operator,
            "out" => TokenKind.Out,
            "override" => TokenKind.Override,
            "params" => TokenKind.Params,
            "private" => TokenKind.Private,
            "protected" => TokenKind.Protected,
            "public" => TokenKind.Public,
            "readonly" => TokenKind.Readonly,
            "ref" => TokenKind.Ref,
            "return" => TokenKind.Return,
            "sbyte" => TokenKind.Sbyte,
            "sealed" => TokenKind.Sealed,
            "short" => TokenKind.Short,
            "sizeof" => TokenKind.Sizeof,
            "stackalloc" => TokenKind.Stackalloc,
            "static" => TokenKind.Static,
            "string" => TokenKind.String,
            "struct" => TokenKind.Struct,
            "switch" => TokenKind.Switch,
            "this" => TokenKind.This,
            "throw" => TokenKind.Throw,
            "true" => TokenKind.True,
            "try" => TokenKind.Try,
            "typeof" => TokenKind.Typeof,
            "uint" => TokenKind.Uint,
            "ulong" => TokenKind.Ulong,
            "unchecked" => TokenKind.Unchecked,
            "unsafe" => TokenKind.Unsafe,
            "ushort" => TokenKind.Ushort,
            "using" => TokenKind.Using,
            "virtual" => TokenKind.Virtual,
            "void" => TokenKind.Void,
            "volatile" => TokenKind.Volatile,
            "while" => TokenKind.While,
            _ => TokenKind.Identifier
        };

        return new SyntaxToken(kind, text, start);
    }

    private ImmutableArray<SyntaxTrivia> ScanTrivia()
    {
        var triviaList = ImmutableArray.CreateBuilder<SyntaxTrivia>();

        while (true)
        {
            switch (Current)
            {
                case ' ' or '\t' or '\r' or '\n':
                    triviaList.Add(ScanWhitespace());
                    continue;
                case '/' when Lookahead is '/' or '*':
                    triviaList.Add(ScanComment());
                    continue;
            }

            return triviaList.ToImmutable();
        }
    }

    private SyntaxTrivia ScanWhitespace()
    {
        var start = position;

        while (char.IsWhiteSpace(Current))
            Advance();

        var length = position - start;
        var text = sourceText.GetText(start, length);

        return new SyntaxTrivia(text, TriviaKind.Whitespace, start);
    }

    private SyntaxTrivia ScanComment()
    {
        var start = position;
        position += 2; // Skip // or /*

        TriviaKind triviaKind;

        if (sourceText[start + 1] == '/')
        {
            // Single-line comment
            while (Current is not '\n' and not '\0')
                Advance();

            triviaKind = TriviaKind.SingleLineComment;
        }
        else
        {
            // Multi-line comment
            while (!(Current == '*' && Lookahead == '/') && Current != '\0')
                Advance();

            if (Current == '\0')
                diagnostics.ReportError(start, "End-of-file found, '*/' expected");
            else
                position += 2; // Skip */

            triviaKind = TriviaKind.MultiLineComment;
        }

        var length = position - start;
        var text = sourceText.GetText(start, length);

        return new SyntaxTrivia(text, triviaKind, start);
    }
}