using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes;

public record AttributeArgumentNode(OptionalSyntax<AttributeNamedArgumentNode> Named, ExpressionNode Expression)
    : SyntaxNode;