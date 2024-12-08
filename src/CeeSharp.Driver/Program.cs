﻿using System.Collections.Immutable;
using CeeSharp.Core;
using CeeSharp.Core.Parsing;
using CeeSharp.Core.Syntax;
using CeeSharp.Core.Text;

const string code = """
                    public class Program {
                        public static void Main() {
                            System.Console.WriteLine("Hello, world!");
                        }
                    }
                    """;

var source = new SourceText(code);

var diagnostics = new Diagnostics();
var lexer = new Lexer(diagnostics, source);

var stream = lexer.Tokenize();

foreach (var token in stream.Tokens)
{
    WriteTrivia(token.LeadingTrivia);
    
    if (token.Kind.IsKeyword())
        Console.ForegroundColor = ConsoleColor.Blue;

    if (token.Kind is TokenKind.StringLiteral or TokenKind.CharacterLiteral)
        Console.ForegroundColor = ConsoleColor.DarkRed;

    Console.Write(token.Text);

    Console.ResetColor();

    WriteTrivia(token.TrailingTrivia);
}

Console.WriteLine();


var parser = new Parser(diagnostics, stream);

var node = parser.Parse();

Console.WriteLine();

WriteNode(node);

Console.WriteLine();

foreach (var diagnostic in diagnostics.AllDiagnostics)
{
    var (line, column) = source.GetLinePosition(diagnostic.Position);

    Console.Write($"({line}, {column}) ");

    if (diagnostic.Severity == DiagnosticSeverity.Error)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;

        Console.Write("error: ");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.Write("warning: ");
    }

    Console.ResetColor();
    
    Console.WriteLine($"{diagnostic.Message}");

}

void WriteTrivia(ImmutableArray<SyntaxTrivia> trivia)
{
    foreach (var triviaNode in trivia)
        switch (triviaNode.Kind)
        {
            case TriviaKind.EndOfLine:
            case TriviaKind.Whitespace:
                Console.Write(triviaNode.Text);
                break;
            case TriviaKind.SingleLineComment:
            case TriviaKind.MultiLineComment:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(triviaNode.Text);
                Console.ResetColor();
                break;
        }
}

static void WriteNode(SyntaxNode node, int indent = 0)
{
    Console.WriteLine($"{new string(' ', indent * 2)}+ {node.GetType().Name}");

    foreach (var child in node.GetChildren())
    {
        WriteNode(child, indent + 1);
    }
}