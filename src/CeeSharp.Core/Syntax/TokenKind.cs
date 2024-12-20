namespace CeeSharp.Core.Syntax;

public enum TokenKind
{
    // Keywords
    Abstract,
    Add,
    As,
    Assembly,
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
    Field,
    Finally,
    Fixed,
    Float,
    For,
    Foreach,
    Get,
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
    Method,
    Module,
    Namespace,
    New,
    Null,
    Object,
    Operator,
    Out,
    Override,
    Param,
    Params,
    Private,
    Property,
    Protected,
    Public,
    ReadOnly,
    Ref,
    Remove,
    Return,
    Sbyte,
    Sealed,
    Set,
    Short,
    SizeOf,
    StackAlloc,
    Static,
    String,
    Struct,
    Switch,
    This,
    Throw,
    True,
    Try,
    Type,
    TypeOf,
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

    public static bool IsModifier(this TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Public
                or TokenKind.Private
                or TokenKind.Protected
                or TokenKind.Internal
                or TokenKind.Abstract
                or TokenKind.Sealed
                or TokenKind.Static
                or TokenKind.Virtual
                or TokenKind.Extern
                or TokenKind.Override
                or TokenKind.ReadOnly
                or TokenKind.Volatile
                or TokenKind.Unsafe
                or TokenKind.New => true,
            _ => false
        };
    }

    public static bool IsParameterModifier(this TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Ref
                or TokenKind.Out
                or TokenKind.Params => true,
            _ => false
        };
    }

    public static bool IsLiteral(this TokenKind kind)
    {
        return kind switch
        {
            TokenKind.True
                or TokenKind.False
                or TokenKind.Null
                or TokenKind.NumericLiteral
                or TokenKind.CharacterLiteral
                or TokenKind.StringLiteral => true,
            _ => false
        };
    }

    public static bool IsPredefinedType(this TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Object
                or TokenKind.String
                or TokenKind.Bool
                or TokenKind.Byte
                or TokenKind.Sbyte
                or TokenKind.Char
                or TokenKind.Decimal
                or TokenKind.Double
                or TokenKind.Float
                or TokenKind.Int
                or TokenKind.Uint
                or TokenKind.Long
                or TokenKind.Ulong
                or TokenKind.Ushort
                or TokenKind.Void => true,
            _ => false
        };
    }

    public static bool IsAssignmentOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Assign
                or TokenKind.PlusAssign
                or TokenKind.MinusAssign
                or TokenKind.TimesAssign
                or TokenKind.DivideAssign
                or TokenKind.ModuloAssign
                or TokenKind.AndAssign
                or TokenKind.OrAssign
                or TokenKind.XorAssign
                or TokenKind.LeftShiftAssign
                or TokenKind.RightShiftAssign => true,
            _ => false
        };
    }

    public static bool IsEqualityOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Equal
                or TokenKind.NotEqual => true,
            _ => false
        };
    }

    public static bool IsRelationalOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.LessThan
                or TokenKind.GreaterThan
                or TokenKind.LessThanOrEqual
                or TokenKind.GreaterThanOrEqual => true,
            _ => false
        };
    }

    public static bool IsAdditiveOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Plus
                or TokenKind.Minus => true,
            _ => false
        };
    }

    public static bool IsMultiplicativeOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Asterisk
                or TokenKind.Divide
                or TokenKind.Modulo => true,
            _ => false
        };
    }

    public static bool IsUnaryOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Plus
                or TokenKind.Minus
                or TokenKind.Exclamation
                or TokenKind.Tilde
                or TokenKind.Asterisk
                or TokenKind.Ampersand
                or TokenKind.PlusPlus
                or TokenKind.MinusMinus => true,
            _ => false
        };
    }

    public static bool IsOverloadableUnaryOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Plus
                or TokenKind.Minus
                or TokenKind.Exclamation
                or TokenKind.Tilde
                or TokenKind.PlusPlus
                or TokenKind.MinusMinus
                or TokenKind.True
                or TokenKind.False => true,
            _ => false
        };
    }

    public static bool IsOverloadableBinaryOperator(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Plus
                or TokenKind.Minus
                or TokenKind.Asterisk
                or TokenKind.Divide
                or TokenKind.Modulo
                or TokenKind.Ampersand
                or TokenKind.Pipe
                or TokenKind.Xor
                or TokenKind.LeftShift
                or TokenKind.RightShift
                or TokenKind.Equal
                or TokenKind.NotEqual
                or TokenKind.GreaterThan
                or TokenKind.LessThan
                or TokenKind.GreaterThanOrEqual
                or TokenKind.LessThanOrEqual => true,
            _ => false
        };
    }

    public static bool IsOverloadableOperator(this TokenKind tokenKind)
    {
        return IsOverloadableUnaryOperator(tokenKind) || IsOverloadableBinaryOperator(tokenKind);
    }

    public static bool CanStartExpression(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.OpenParen
                or TokenKind.This
                or TokenKind.Base
                or TokenKind.New
                or TokenKind.TypeOf
                or TokenKind.Default
                or TokenKind.SizeOf
                or TokenKind.StackAlloc
                or TokenKind.Identifier => true,
            _ => false
        };
    }

    public static bool IsValidInNamespace(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Namespace
                or TokenKind.Class
                or TokenKind.Struct
                or TokenKind.Enum
                or TokenKind.Interface
                or TokenKind.Delegate
                or TokenKind.Using
                or TokenKind.OpenBracket => true,
            _ => tokenKind.IsModifier()
        };
    }

    public static bool IsValidInType(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Class
                or TokenKind.Struct
                or TokenKind.Enum
                or TokenKind.Interface
                or TokenKind.Delegate
                or TokenKind.Event
                or TokenKind.Operator
                or TokenKind.Implicit
                or TokenKind.Explicit
                or TokenKind.Identifier
                or TokenKind.CloseBrace
                or TokenKind.OpenBracket => true,
            _ => tokenKind.IsModifier() || tokenKind.IsPredefinedType()
        };
    }

    public static bool IsValidInEnumMember(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Identifier
                or TokenKind.CloseBrace
                or TokenKind.OpenBracket
                or TokenKind.Comma => true,
            _ => false
        };
    }

    public static bool IsValidInParameterList(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Identifier
                or TokenKind.CloseParen
                or TokenKind.Comma
                or TokenKind.OpenBracket => true,
            _ => tokenKind.IsPredefinedType() || tokenKind.IsParameterModifier()
        };
    }

    public static bool IsValidInAttributeList(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Identifier
                or TokenKind.CloseBracket
                or TokenKind.Comma
                or TokenKind.OpenParen
                or TokenKind.CloseParen => true,
            _ => false
        };
    }

    public static bool IsValidInPropertyOrIndexer(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Get
                or TokenKind.Set
                or TokenKind.OpenBrace
                or TokenKind.CloseBrace
                or TokenKind.OpenBracket => true,
            _ => tokenKind.IsModifier()
        };
    }

    public static bool IsValidInEvent(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Add
                or TokenKind.Remove
                or TokenKind.OpenBrace
                or TokenKind.CloseBrace
                or TokenKind.OpenBracket => true,
            _ => tokenKind.IsModifier()
        };
    }

    public static bool IsValidInStatement(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.If
                or TokenKind.For
                or TokenKind.Foreach
                or TokenKind.While
                or TokenKind.Do
                or TokenKind.Switch
                or TokenKind.Break
                or TokenKind.Continue
                or TokenKind.Return
                or TokenKind.Throw
                or TokenKind.Try
                or TokenKind.Using
                or TokenKind.Fixed
                or TokenKind.Lock
                or TokenKind.Checked
                or TokenKind.Unchecked
                or TokenKind.Unsafe
                or TokenKind.Semicolon
                or TokenKind.OpenBrace
                or TokenKind.CloseBrace => true,
            _ => tokenKind.IsModifier() || tokenKind.IsPredefinedType() || tokenKind.CanStartExpression()
        };
    }
}