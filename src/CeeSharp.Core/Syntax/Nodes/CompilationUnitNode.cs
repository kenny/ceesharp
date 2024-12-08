using System.Collections.Immutable;
using CeeSharp.Core.Syntax.Nodes.Declarations;

namespace CeeSharp.Core.Syntax.Nodes;

public record CompilationUnitNode(
    ImmutableArray<UsingDirectiveNode> Usings,
    ImmutableArray<DeclarationNode> Declarations) : SyntaxNode;