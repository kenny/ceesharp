namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public record EnumMemberDeclarationNode : MemberDeclarationNode
{
    public override DeclarationKind DeclarationKind => DeclarationKind.EnumMember;
}