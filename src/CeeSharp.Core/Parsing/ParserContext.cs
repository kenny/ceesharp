namespace CeeSharp.Core.Parsing;

public enum ParserContext
{
    None = 0,
    Namespace = 1 << 0,
    Type = 1 << 1,
    ParameterList = 1 << 2,
    AttributeList = 1 << 3,
    EnumMember = 1 << 4,
    Property = 1 << 5,
    Statement = 1 << 6
} 