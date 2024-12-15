namespace CeeSharp.Core.Syntax;

public enum DeclarationKind
{
    Incomplete = 1 << 0,
    Namespace = 1 << 1,
    Class = 1 << 2,
    Struct = 1 << 3,
    Interface = 1 << 4,
    Delegate = 1 << 5,
    Field = 1 << 6,
    Event = 1 << 7,
    Enum = 1 << 8,
    Method = 1 << 9,
    Operator = 1 << 10,
    Property = 1 << 11,
    Indexer = 1 << 12,
    Constructor = 1 << 13,
    EnumMember = 1 << 14
}