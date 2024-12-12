using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes;

public record ParameterNode(TypeSyntax Type, SyntaxToken Identifier) : SyntaxNode;