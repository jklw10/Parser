
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
    public int IValue;

    public Token(TokenType type, int weight, string tokenString, Operator op = default, double dValue = default, int iValue = default)
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
        else if (double.TryParse(token, out double dNum))
        {
            this = new(TokenType.DValue, 0, token, dValue: dNum);
        }
        else if (int.TryParse(token, out int iNum))
        {
            this = new(TokenType.IValue, 0, token, iValue: iNum);
        }
        else if (MathParser.CurrentParser.DVC.TryGetValueRefrence(token, out ValueReference<double> dvalue))
        {
            this = new(TokenType.ValueReference | TokenType.DValue, 0, token, dValue: dvalue.value);
        }
        else if (MathParser.CurrentParser.IVC.TryGetValueRefrence(token, out ValueReference<int> ivalue))
        {
            this = new(TokenType.ValueReference | TokenType.IValue, 0, token, iValue: ivalue.value);
        }
        else
        {
            this = new(TokenType.Invalid, 0, token);
        }
    }
    public (bool i,bool d) GetValueReference(out ValueReference<int> ivalue, out ValueReference<double> dvalue)
    {
        if (MathParser.CurrentParser is null || !Type.IsSet(TokenType.ValueReference))
            throw new Exception("Value cannot be refrenced");
        bool d = MathParser.CurrentParser.DVC.TryGetValueRefrence(TokenString, out dvalue);
        bool i = MathParser.CurrentParser.IVC.TryGetValueRefrence(TokenString, out ivalue);
        return (i, d);
    }
    public void AssignToRefrence(Operator contextOperator, Token value)
    {
        var vr = GetValueReference(out ValueReference<int> i, out ValueReference<double> d);

        if (vr.d)
            d.value = value.Type.IsSet(TokenType.DValue) ? value.DValue : value.IValue;
        else
            i.value = value.Type.IsSet(TokenType.IValue) ? value.IValue : (int)contextOperator.ApplyFunction(value.DValue);
    }
    public Token ApplyOperator(Operator op, Token b)
    {

        if (Type.IsSet(TokenType.IValue) && b.Type.IsSet(TokenType.IValue))
        {
            return new(TokenType.IValue, 0, TokenString + op + b.TokenString, iValue: op switch
            {
                Operator.Add => IValue + b.IValue,
                Operator.Subtract => IValue - b.IValue,
                Operator.Multiply => IValue * b.IValue,
                Operator.Divide => IValue / b.IValue,
                Operator.Raise => (int)Math.Pow(IValue, b.IValue),
                Operator.Equals => SetValue( b.IValue),
                Operator.Smaller => IValue < b.IValue ? 1 : 0,
                Operator.SmallerOrEqual => IValue <= b.IValue ? 1 : 0,
                Operator.Larger => IValue > b.IValue ? 1 : 0,
                Operator.LargerOrEqual => IValue >= b.IValue ? 1 : 0,
                Operator.Not => IValue != b.IValue ? 1 : 0,
                Operator.Or => IValue | b.IValue,
                Operator.And => IValue & b.IValue,
                _ => throw new("invalid operator type:" + op + " between " + TokenString + " and " + b.TokenString)
            });
        }
        if (b.Type.IsSet(TokenType.IValue))
            b.DValue = b.IValue;
        if (Type.IsSet(TokenType.IValue))
            DValue = IValue;
        return new(TokenType.DValue, 0, TokenString + op + b.TokenString, dValue: op switch
        {
            Operator.Add => DValue + b.DValue,
            Operator.Subtract => DValue - b.DValue,
            Operator.Multiply => DValue * b.DValue,
            Operator.Divide => DValue / b.DValue,
            Operator.Raise => Math.Pow(DValue, b.DValue),
            Operator.Equals => SetValue(b.DValue),
            Operator.Smaller => DValue < b.DValue ? 1 : 0,
            Operator.SmallerOrEqual => DValue <= b.DValue ? 1 : 0,
            Operator.Larger => DValue > b.DValue ? 1 : 0,
            Operator.LargerOrEqual => DValue >= b.DValue ? 1 : 0,
            _ => throw new("invalid operator type:" + op + " between " + TokenString + " and " + b.TokenString)
        });
        
        
    }
    public double SetValue(double value)
    {
        if (Type.IsSet(TokenType.ValueReference))
        {
            if (Type.IsSet(TokenType.DValue))
            {
                AssignToRefrence(Operator.NoOp, new(TokenType.DValue,0,"",dValue: value));
                return value;
            }
            throw new("Cannot set int to a double");
        }
        throw new("can't set a non refrence value.");

    }
    public int SetValue(int value)
    {
        DValue = value;
        IValue = value;
        if (Type.IsSet(TokenType.ValueReference))
        {
            AssignToRefrence(Operator.NoOp, new(TokenType.IValue, 0, "", iValue: value));
            return value;
        }
        throw new("can't set a non refrence value.");
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
            _ => throw new(op + "is not a context operator"),
        };
    }
    public static bool IsSet(this TokenType self, TokenType flag)
    {
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

