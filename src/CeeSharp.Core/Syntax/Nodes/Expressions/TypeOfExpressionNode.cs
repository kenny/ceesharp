using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record TypeOfExpressionNode(
    SyntaxToken TypeOfKeyword,
    SyntaxToken OpenParen,
    TypeSyntax Type,
    SyntaxToken CloseParen) : ExpressionNode;