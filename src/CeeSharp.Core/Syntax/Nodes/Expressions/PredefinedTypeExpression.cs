using CeeSharp.Core.Syntax.Types;

namespace CeeSharp.Core.Syntax.Nodes.Expressions;

public sealed record PredefinedTypeExpression(TypeSyntax Type) : ExpressionNode;