using System.Collections.Immutable;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record AccessorDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    OptionalSyntax<SyntaxToken> Keyword,
    OptionalSyntax<BlockNode> Body,
    OptionalSyntax<SyntaxToken> Semicolon) : SyntaxNode;