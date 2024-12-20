using System.Collections.Immutable;
using CeeSharp.Core.Parsing;
using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record FieldDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    OptionalSyntax<SyntaxToken> ConstKeyword,
    TypeSyntax Type,
    SeparatedSyntaxList<VariableDeclaratorNode> Declarators,
    SyntaxToken Semicolon) : MemberDeclarationNode, IModifierValidator
{
    public static bool IsModifierValid(ParserContext parserContext, TokenKind modifier)
    {
        if (parserContext != ParserContext.Constant)
            return modifier is TokenKind.Static or TokenKind.ReadOnly or TokenKind.Volatile;

        return modifier is TokenKind.New
            or TokenKind.Public
            or TokenKind.Protected
            or TokenKind.Internal
            or TokenKind.Private
            or TokenKind.Unsafe;
    }

    public override IEnumerable<SyntaxNode> GetChildren()
    {
        foreach (var child in Attributes)
            yield return child;

        foreach (var child in Declarators.Elements)
            yield return child;
    }
}