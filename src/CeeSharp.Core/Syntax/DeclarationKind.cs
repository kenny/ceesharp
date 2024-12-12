namespace CeeSharp.Core.Syntax;

public enum DeclarationKind
{
    None = 0,
    Namespace = 1 << 0,
    Class = 1 << 1,
    Struct = 1 << 2,
    Method = 1 << 3,
    Constructor = 1 << 4,
    ParameterList = 1 << 5,

    Type = Class | Struct
}