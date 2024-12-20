using System.Collections.Immutable;
using CeeSharp.Core.Parsing;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record MethodDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    TypeSyntax ReturnType,
    OptionalSyntax<ExplicitInterfaceNode> ExplicitInterface,
    SyntaxToken Identifier,
    SyntaxToken OpenParen,
    SeparatedSyntaxList<ParameterNode> Parameters,
    SyntaxToken CloseParen,
    BlockNodeOrToken BlockOrSemicolon) : MemberDeclarationNode, IModifierValidator
{
    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        return modifier is TokenKind.Public
            or TokenKind.Private
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Static
            or TokenKind.Virtual
            or TokenKind.Override
            or TokenKind.Abstract
            or TokenKind.New
            or TokenKind.Sealed
            or TokenKind.Extern
            or TokenKind.Unsafe;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        if (ExplicitInterface.HasValue)
            yield return ExplicitInterface.Element;

        foreach (var child in Parameters.Elements)
            yield return child;

        if (BlockOrSemicolon.IsLeft)
            yield return BlockOrSemicolon.LeftValue;
    }
}