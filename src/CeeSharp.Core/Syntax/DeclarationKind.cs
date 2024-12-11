namespace CeeSharp.Core.Syntax;

public enum DeclarationKind
{
    Namespace = 1 << 0,
    Class = 1 << 1,
    
    Type = Class
}