namespace CeeSharp.Core.Parsing;

public enum ParserContext
{
    None,
    Namespace,
    Type,
    Method,
    Delegate,
    ParameterList,
    AttributeList,
    EnumMember,
    Property,
    Statement
}