namespace CeeSharp.Core.Parsing;

public enum ParserContext
{
    None,
    Namespace,
    Type,
    EnumMember,
    ParameterList,
    AttributeList,
    Property,
    Event,
    Statement
}