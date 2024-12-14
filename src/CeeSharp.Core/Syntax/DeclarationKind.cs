namespace CeeSharp.Core.Syntax;

public enum DeclarationKind
{
    None = 0,
    Namespace = 1 << 0,
    Class = 1 << 1,
    Struct = 1 << 2,
    Enum = 1 << 3,
    Method = 1 << 4,
    Field = 1 << 5,
    Constructor = 1 << 6,
    EnumMember = 1 << 7,
    ParameterList = 1 << 8,
    AttributeList = 1 << 9,

    Type = Class | Struct | Enum
}