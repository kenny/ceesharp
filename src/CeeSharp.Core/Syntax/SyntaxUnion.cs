global using SyntaxNodeOrToken =
    CeeSharp.Core.Syntax.SyntaxUnion<CeeSharp.Core.Syntax.SyntaxNode, CeeSharp.Core.Syntax.SyntaxToken>;
global using BlockNodeOrToken =
    CeeSharp.Core.Syntax.SyntaxUnion<CeeSharp.Core.Syntax.Nodes.Statements.BlockStatementNode,
        CeeSharp.Core.Syntax.SyntaxToken>;
using System.Diagnostics.CodeAnalysis;

namespace CeeSharp.Core.Syntax;

public readonly struct SyntaxUnion<TLeft, TRight>
    where TLeft : SyntaxElement
    where TRight : SyntaxElement
{
    private readonly SyntaxElement? value;
    private readonly bool isLeft;

    private SyntaxUnion(SyntaxElement value, bool isLeft)
    {
        this.value = value;
        this.isLeft = isLeft;
    }

    public static SyntaxUnion<TLeft, TRight> Left(TLeft value)
    {
        return new SyntaxUnion<TLeft, TRight>(value, true);
    }

    public static SyntaxUnion<TLeft, TRight> Right(TRight value)
    {
        return new SyntaxUnion<TLeft, TRight>(value, false);
    }

    [MemberNotNullWhen(true, nameof(LeftValue))]
    public bool IsLeft => value != null && isLeft;

    [MemberNotNullWhen(true, nameof(RightValue))]
    public bool IsRight => value != null && !isLeft;

    public TLeft? LeftValue => IsLeft ? (TLeft)value! : null;
    public TRight? RightValue => IsRight ? (TRight)value! : null;

    public static implicit operator SyntaxUnion<TLeft, TRight>(TLeft value)
    {
        return new SyntaxUnion<TLeft, TRight>(value, true);
    }

    public static implicit operator SyntaxUnion<TLeft, TRight>(TRight value)
    {
        return new SyntaxUnion<TLeft, TRight>(value, false);
    }
}