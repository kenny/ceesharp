using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes.Statements;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record AccessorDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    ImmutableArray<SyntaxToken> Modifiers,
    OptionalSyntax<SyntaxToken> Keyword,
    OptionalSyntax<BlockStatementNode> Body,
    OptionalSyntax<SyntaxToken> Semicolon) : SyntaxNode;