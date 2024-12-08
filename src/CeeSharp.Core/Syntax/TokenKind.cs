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
    Times, // *
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
}