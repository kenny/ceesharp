using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record SizeOfExpressionNode(
    SyntaxToken SizeOfKeyword,
    SyntaxToken OpenParen,
    TypeSyntax Type,
    SyntaxToken CloseParen) : ExpressionNode;