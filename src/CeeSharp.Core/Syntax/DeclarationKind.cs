namespace CeeSharp.Core.Syntax;

public enum DeclarationKind
{
    Namespace = 1 << 0,
    Class = 1 << 1,
    Struct = 1 << 2,

    Type = Class | Struct
}