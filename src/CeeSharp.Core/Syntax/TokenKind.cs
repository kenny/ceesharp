namespace CeeSharp.Core.Syntax;

public enum TokenKind
{
    // Keywords
    Abstract,
    As,
    Base,
    Bool,
    Break,
    Byte,
    Case,
    Catch,
    Char,
    Checked,
    Class,
    Const,
    Continue,
    Decimal,
    Default,
    Delegate,
    Do,
    Double,
    Else,
    Enum,
    Event,
    Explicit,
    Extern,
    False,
    Finally,
    Fixed,
    Float,
    For,
    Foreach,
    Goto,
    If,
    Implicit,
    In,
    Int,
    Interface,
    Internal,
    Is,
    Lock,
    Long,
    Namespace,
    New,
    Null,
    Object,
    Operator,
    Out,
    Override,
    Params,
    Private,
    Protected,
    Public,
    Readonly,
    Ref,
    Return,
    Sbyte,
    Sealed,
    Short,
    Sizeof,
    Stackalloc,
    Static,
    String,
    Struct,
    Switch,
    This,
    Throw,
    True,
    Try,
    Typeof,
    Uint,
    Ulong,
    Unchecked,
    Unsafe,
    Ushort,
    Using,
    Virtual,
    Void,
    Volatile,
    While,

    // Operators
    Plus, // +
    Minus, // -
    Asterisk, // *
    Divide, // /
    Modulo, // %
    Ampersand, // &
    Pipe, // |
    Xor, // ^
    Exclamation, // !
    Tilde, // ~
    LessThan, // <
    GreaterThan, // >
    Question, // ?
    Equal, // ==
    NotEqual, // !=
    LessThanOrEqual, // <=
    GreaterThanOrEqual, // >=
    LeftShift, // <<
    RightShift, // >>
    Arrow, // ->
    PlusPlus, // ++
    MinusMinus, // --
    AndAnd, // &&
    OrOr, // ||

    // Assign
    Assign, // =
    PlusAssign, // +=
    MinusAssign, // -=
    TimesAssign, // *=
    DivideAssign, // /=
    ModuloAssign, // %=
    AndAssign, // &=
    OrAssign, // |=
    XorAssign, // ^=
    LeftShiftAssign, // <<=
    RightShiftAssign, // >>=

    // Symbols
    OpenBrace, // {
    CloseBrace, // }
    OpenBracket, // [
    CloseBracket, // ]
    OpenParen, // (
    CloseParen, // )
    Dot, // .
    Comma, // ,
    Colon, // :
    Semicolon, // ;

    // Other
    Identifier,
    CharacterLiteral,
    StringLiteral,
    NumericLiteral,
    PreprocessorDirective,
    EndOfFile,
    Unknown
}

public static class TokenKindExtensions
{
    public static bool IsKeyword(this TokenKind tokenKind)
    {
        return (int)tokenKind >= 0 && (int)tokenKind <= (int)TokenKind.While;
    }

    public static bool IsModifier(this TokenKind kind) => kind switch
    {
        TokenKind.Public or
            TokenKind.Private or
            TokenKind.Protected or
            TokenKind.Internal or
            TokenKind.Abstract or
            TokenKind.Sealed or
            TokenKind.Static or
            TokenKind.Virtual or
            TokenKind.Extern or
            TokenKind.Override or
            TokenKind.Readonly or
            TokenKind.Volatile or
            TokenKind.Unsafe or
            TokenKind.New => true,
        _ => false
    };
    
    public static bool IsParameterModifier(this TokenKind kind) => kind switch
    {
        TokenKind.Ref or
            TokenKind.Out or 
            TokenKind.Params => true,
        _ => false
    };

    public static bool IsPredefinedType(this TokenKind kind) => kind switch
    {
        TokenKind.Object or
            TokenKind.String or
            TokenKind.Bool or
            TokenKind.Byte or
            TokenKind.Sbyte or
            TokenKind.Char or
            TokenKind.Decimal or
            TokenKind.Double or
            TokenKind.Float or
            TokenKind.Int or
            TokenKind.Uint or
            TokenKind.Long or
            TokenKind.Ulong or
            TokenKind.Ushort or
            TokenKind.Void => true,
        _ => false
    };
}