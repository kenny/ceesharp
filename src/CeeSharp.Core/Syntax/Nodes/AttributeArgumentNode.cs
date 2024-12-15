using CeeSharp.Core.Syntax.Nodes.Expressions;

namespace CeeSharp.Core.Syntax.Nodes;

public sealed record AttributeArgumentNode(OptionalSyntax<AttributeNamedArgumentNode> Named, ExpressionNode Expression)
    : SyntaxNode;