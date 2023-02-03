
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Parser;
[Flags]
public enum TokenType
{
    Invalid =0,
    Operator=1,
    ValueReference=2,
    DValue=4,
    IValue=8,
    ContextOpen=16,
    ContextClose=32,
    ContextOperator=64,
}
public struct Token
{
    public readonly static Token INVALID = new(TokenType.Invalid,0,"");
    public TokenType Type;
    public int Weight;
    public string TokenString;
    public Operator Op;
    public double DValue; //todo change?
    public long IValue;

    public Token(TokenType type, int weight, string tokenString, Operator op = default, double dValue = default, long iValue = default)
    {
        Type = type;
        Weight = weight;
        TokenString = tokenString;
        Op = op;
        DValue = dValue;
        IValue = iValue;
    }
    public Token(string token)
    {
        if (MathParser.CurrentParser is null) throw new Exception("MathParser.CurrentParser is null prior to token creation(should never happen)");
        if (OperatorGetter.GetOperator(token, out Operator op))
        {
            var t = op switch
            {
                Operator.Floor or Operator.Ceil or Operator.Round or Operator.SquareRoot or Operator.Truncate
                    => TokenType.ContextOperator,
                Operator.OpenParenthesis or Operator.Equals => TokenType.ContextOpen,
                Operator.CloseParenthesis => TokenType.ContextClose,
                _ => TokenType.Operator,
            };
            this = new(t, OperatorGetter.GetWeight(op), token, op);
        }
        else if (long.TryParse(token, out long iNum))
        {
            this = new(TokenType.IValue, 0, token, iValue: iNum);
        }
        else if (double.TryParse(token, out double dNum))
        {
            this = new(TokenType.DValue, 0, token, dValue: dNum);
        }
        else if (MathParser.CurrentParser.DVC.TryGetValueRefrence(token, out ValueReference<double> dvalue))
        {
            this = new(TokenType.ValueReference | TokenType.DValue, 0, token, dValue: dvalue.value);
        }
        else if (MathParser.CurrentParser.IVC.TryGetValueRefrence(token, out ValueReference<long> ivalue))
        {
            this = new(TokenType.ValueReference | TokenType.IValue, 0, token, iValue: ivalue.value);
        }
        else
        {
            this = new(TokenType.Invalid, 0, token);
        }
    }
    public (bool i,bool d) GetValueReference(out ValueReference<long> ivalue, out ValueReference<double> dvalue)
    {
        if (MathParser.CurrentParser is null || !Type.IsSet(TokenType.ValueReference))
            throw new Exception("Value cannot be refrenced");
        bool d = MathParser.CurrentParser.DVC.TryGetValueRefrence(TokenString, out dvalue);
        bool i = MathParser.CurrentParser.IVC.TryGetValueRefrence(TokenString, out ivalue);
        return (i, d);
    }
    public void AssignToRefrence(Operator contextOperator, Token value)
    {
        var vr = GetValueReference(out ValueReference<long> i, out ValueReference<double> d);

        if (vr.d)
            d.value = value.Type.IsSet(TokenType.DValue) ? value.DValue : value.IValue;
        else
            i.value = value.Type.IsSet(TokenType.IValue) ? value.IValue : contextOperator.ApplyFunctionLong(value.DValue);
    }
    public Token ApplyFunction(Operator op)
    {
        if (op == Operator.NoOp) return this;
        if (Type.IsSet(TokenType.DValue))
            DValue = op.ApplyFunction(DValue);
        else
            IValue = op.ApplyFunctionLong(IValue);
        return this;
    }
    public Token ApplyOperator(Operator op, Token value, Operator contextOperator)
    {
        if (op == Operator.Equals) return SetValue(contextOperator, value);
        TokenString = TokenString + op + value.TokenString;
        if (Type.IsSet(TokenType.DValue))
            DValue = Operate(IValue, value.Type.IsSet(TokenType.DValue) ? value.DValue : value.IValue,op);
        else
            IValue = Operate(IValue, value.Type.IsSet(TokenType.IValue) ? value.IValue : (long)(contextOperator.ApplyFunction(value.DValue)),op);
        return this;
    }
    public static long Operate(long v1, long v2, Operator op)
    {
        return op switch
        {
            Operator.Add => v1 + v2,
            Operator.Subtract => v1 - v2,
            Operator.Multiply => v1 * v2,
            Operator.Divide => v1 / v2,
            Operator.Raise => (long)Math.Pow(v1, v2),
            Operator.Smaller => v1 < v2 ? 1 : 0,
            Operator.SmallerOrEqual => v1 <= v2 ? 1 : 0,
            Operator.Larger => v1 > v2 ? 1 : 0,
            Operator.LargerOrEqual => v1 >= v2 ? 1 : 0,
            Operator.Not => v1 != v2 ? 1 : 0,
            Operator.Or => v1 | v2,
            Operator.And => v1 & v2,
            _ => throw new("invalid operator type:" + op + " between " + v1 + " and " + v2)
        };
    }
    public static double Operate(double v1, double v2, Operator op)
    {
        return op switch
        {
            Operator.Add => v1 + v2,
            Operator.Subtract => v1 - v2,
            Operator.Multiply => v1 * v2,
            Operator.Divide => v1 / v2,
            Operator.Raise => Math.Pow(v1, v2),
            Operator.Smaller => v1 < v2 ? 1 : 0,
            Operator.SmallerOrEqual => v1 <= v2 ? 1 : 0,
            Operator.Larger => v1 > v2 ? 1 : 0,
            Operator.LargerOrEqual => v1 >= v2 ? 1 : 0,
            _ => throw new("invalid operator type:" + op + " between " + v1 + " and " + v2)
        };
    }
    public Token SetValue(Operator contextOp, Token value)
    {
        if (!Type.IsSet(TokenType.ValueReference))
            throw new("can't set a non refrence value.");

        AssignToRefrence(contextOp, value);
        return this;
    }
    
}
public interface IValueContainer<T> where T : INumber<T>
{
    public bool TryGetValueRefrence(string t, out ValueReference<T> v);
}
public ref struct ValueReference<T> where T : INumber<T>
{
    public ref T value;
}
public static partial class OperatorGetter
{

    [GeneratedRegex(@"([*()\^\/=\+\-!|&<>])|([0-9,.]+)|([a-zA-Z]+)", RegexOptions.Compiled|RegexOptions.IgnorePatternWhitespace, 100)]
    public static partial Regex SplitOperators();

    public static readonly Dictionary<string, Operator> OperatorDictionary = new(){
        { "+", Operator.Add },
        { "-", Operator.Subtract },
        { "*", Operator.Multiply },
        { "/", Operator.Divide },
        { "^", Operator.Raise },
        { "(", Operator.OpenParenthesis },
        { ")", Operator.CloseParenthesis },
        { "=", Operator.Equals },
        { "!", Operator.Not },
        { "|", Operator.Or },
        { "&", Operator.And },
        { "<", Operator.Smaller },
        { ">", Operator.Larger },
        { "<=", Operator.SmallerOrEqual },
        { ">=", Operator.LargerOrEqual },
        { ".", Operator.Dot },
        { "sqrt", Operator.SquareRoot },
        { "floor", Operator.Floor },
        { "ceil", Operator.Ceil },
        { "round", Operator.Round },
        { "trunc", Operator.Truncate },
    };
    public static string[] GetOperators() => OperatorDictionary.Keys.ToArray();
    public static bool GetOperator(string name, out Operator op) =>
        OperatorDictionary.TryGetValue(name, out op);
    public static int GetWeight(Operator op) => op switch
    {
        Operator.NoOp => -1,
        Operator.Add => 1,
        Operator.Subtract => 1,
        Operator.Multiply => 2,
        Operator.Divide => 2,
        Operator.Raise => 3,
        Operator.SquareRoot => 3,
        Operator.OpenParenthesis => 0,
        Operator.CloseParenthesis => 0,
        Operator.Equals => 0,
        Operator.Not => 0,
        Operator.Or => 0,
        Operator.And => 0,
        Operator.Smaller => 0,
        Operator.SmallerOrEqual => 0,
        Operator.Larger => 0,
        Operator.LargerOrEqual => 0,
        Operator.Dot => 0,
        Operator.Floor => 3,
        Operator.Ceil => 3,
        Operator.Round => 3,
        Operator.Truncate => 3,
        _ => -1,
    };

    public static T ApplyOperator<T>(this Operator op, T a, T b) where T: INumber<T>
    {
        return op switch
        {
            _ => T.Zero,
        };
    }
    public static double ApplyFunction(this Operator op, double value)
    {
        return op switch
        {
            Operator.Truncate => (int)value,
            Operator.SquareRoot => Math.Sqrt(value),
            Operator.Round => Math.Round(value),
            Operator.Ceil => Math.Ceiling(value),
            Operator.Floor => Math.Floor(value),
            Operator.NoOp => value,
            _ => throw new(op + "is not a context operator"),
        };
    }
    public static long ApplyFunctionLong(this Operator op, double value) 
    {
        return op switch
        {
            Operator.Truncate => (long)value,
            Operator.SquareRoot => (long)Math.Sqrt(value),
            Operator.Round => (long)Math.Round(value),
            Operator.Ceil => (long)Math.Ceiling(value),
            Operator.Floor => (long)Math.Floor(value),
            _ => throw new(op + "can't assign double trying to assign a double to long"),
        };
    }
    public static bool IsSet(this TokenType self, TokenType flag)
    {
        if(flag == TokenType.Invalid)
        {
            return self == TokenType.Invalid;
        }
        return (self & flag) == flag;
    }
}
public enum Operator
{
    NoOp = 0,
    Add,
    Subtract,
    Multiply,
    Divide,
    Raise,
    SquareRoot,
    OpenParenthesis,
    CloseParenthesis,
    Equals,
    Not,
    Or,
    And,
    Smaller,
    SmallerOrEqual,
    Larger,
    LargerOrEqual,
    Dot,
    Floor,
    Ceil,
    Round,
    Truncate,
}

