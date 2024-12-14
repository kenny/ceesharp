namespace CeeSharp.Core.Syntax;

public enum DeclarationKind
{
    None = 0,
    Namespace = 1 << 0,
    Class = 1 << 1,
    Struct = 1 << 2,
    Interface = 1 << 3,
    Delegate = 1 << 4,
    Field = 1 << 5,
    Event = 1 << 6,
    Enum = 1 << 7,
    Method = 1 << 8,
    Operator = 1 << 9,
    Property = 1 << 10,
    Indexer = 1 << 11,
    Constructor = 1 << 12,
    EnumMember = 1 << 13,
    ParameterList = 1 << 14,
    AttributeList = 1 << 15,

    Type = Class | Struct | Enum | Interface
}