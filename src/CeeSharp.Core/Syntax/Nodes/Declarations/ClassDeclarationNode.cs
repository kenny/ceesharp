namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record ClassDeclarationNode(
    SyntaxToken ClassKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenBrace,
    SyntaxToken CloseBrace) : DeclarationNode;