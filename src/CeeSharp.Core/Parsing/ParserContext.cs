namespace CeeSharp.Core.Parsing;

public enum ParserContext
{
    None,
    Namespace,
    Type,
    Delegate,
    ParameterList,
    AttributeList,
    EnumMember,
    Property,
    Statement
} 