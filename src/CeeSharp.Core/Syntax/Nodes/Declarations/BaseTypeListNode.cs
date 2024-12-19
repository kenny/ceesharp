using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Declarations;

public sealed record BaseTypeListNode(
    SyntaxToken Colon,
    SeparatedSyntaxList<TypeSyntax> Types) : SyntaxNode;