using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record EnumMemberDeclarationNode(
    ImmutableArray<AttributeSectionNode> Attributes,
    SyntaxToken Identifier,
    OptionalSyntax<SyntaxToken> Assign,
    OptionalSyntax<ExpressionNode> Expression,
    OptionalSyntax<SyntaxToken> Comma) : MemberDeclarationNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.EnumMember;
}