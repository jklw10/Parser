using System.Collections.Generic;

namespace Parser;
public class MathParser
{
    public static MathParser? CurrentParser { get; private set; }
    public IValueContainer<double> DVC;
    public IValueContainer<int> IVC;
    public MathContext MathContext;
    public MathParser(IValueContainer<double> dvc, IValueContainer<int> ivc, string text)
    {
        DVC = dvc;
        IVC = ivc;
        CurrentParser = this;
        MathContext = Parse(text);
    }
    public MathContext Parse(string text)
    {
        string[] tokens = OperatorGetter.SplitOperators().Split(text);
        return WeighTokens(Tokenize(tokens));
        
    }
    private MathContext WeighTokens(List<Token> tokens)
    {
        int ContextID = 0;
        //todo default operator
        MathContext floor = new() { contextOperator = Operator.Round };
        Operator nextConOp = Operator.Round;
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            switch (t.Type)
            {
                case TokenType.ContextOperator:
                    nextConOp = t.Op;
                    break;
                case TokenType.ValueReference:
                case TokenType.DValue:
                case TokenType.IValue:
                case TokenType.DValue | TokenType.ValueReference:
                case TokenType.IValue | TokenType.ValueReference:
                case TokenType.Operator:
                    floor.AddInto(ContextID, t);
                    break;
                case TokenType.ContextOpen:
                    if(t.Op == Operator.Equals) { 
                        floor.AddInto(ContextID, t);
                    }
                    ContextID = floor.AddNewContext(ContextID, nextConOp);
                    
                    break;
                case TokenType.ContextClose:
                    ContextID = floor.CloseContext(ContextID);
                    break;
            }
        }

        return floor;
    }
    private static List<Token> Tokenize(Span<string> tokenSpan)
    {
        List<Token> tokens = new();
        foreach (var token in tokenSpan)
        {
            if(string.IsNullOrWhiteSpace(token)) continue;
            Token t = new(token);
            if(tokens.Count != 0 && tokens.Last().Type != TokenType.Operator && t.Type.HasFlag(TokenType.Operator | TokenType.ContextOpen | TokenType.ContextClose | TokenType.ContextOperator))
            {
                tokens.Add(new("*"));
            }
            tokens.Add(t);
        }
        return tokens;
    }
}
internal struct MathPiece
{
    public Token? Token;
    public MathContext? MathContext;
    public MathPiece(Token? t = null, MathContext? mc = null)
    {
        Token = t;
        MathContext = mc;
    }
}
public class MathContext
{
    public Operator contextOperator;
    internal readonly List<MathPiece> tokens = new();
    public int id;
    public void AddInto(int id, Token mp)
    {
        if (this.id == id)
            tokens.Add(new(mp));
        else foreach (var mathPiece in tokens)
        {
            mathPiece.MathContext?.AddInto(id, mp);
        }
    }
    public int AddNewContext(int id, Operator op)
    {
        if (this.id == id)
        {
            tokens.Add(new(mc: new() { id = id+1,contextOperator = op}));
            return id+1;
        }
        else foreach (var mathPiece in tokens)
        {
            int ident = mathPiece.MathContext?.AddNewContext(id, op) ?? -1;
            if (ident != -1)
            {
                return ident;
            }
        }
        return -1;
    }
    public int CloseContext(int closedId)
    {
        if (id == closedId)
        {
            return -1;
        }
        else foreach (var mathPiece in tokens)
        {
            int ident = mathPiece.MathContext?.CloseContext(closedId) ?? -2;
            if (ident == -1)
            {
                return id;
            }
        }
        return -2;

    }
    
    public bool Calculate(out Token value)=>
        Calculate(tokens.ToArray(), out value);
    private bool Calculate(Span<MathPiece> tokenSpan, out Token result)
    {
        result = new(TokenType.Invalid, 0, "");
        if(tokenSpan.Length == 0) return false;

        Token first = GetToken(tokenSpan[0]);
        if (tokenSpan.Length >= 3)
        {
            Token second = GetToken(tokenSpan[1]);
            Token third = GetToken(tokenSpan[2]);
            if (tokenSpan.Length >= 5) { 
                Token fourth = GetToken(tokenSpan[3]);
                if(fourth.Weight > second.Weight)
                    Calculate(tokenSpan[2..^0], out third);
            }

            result = first.ApplyOperator(second.Op, third);
            return second.Op == Operator.Equals;
        }
        result = new(TokenType.DValue, 0, "", dValue: contextOperator.ApplyFunction(first.Type.IsSet(TokenType.DValue)? first.DValue:first.IValue));
        return false;
    }
    static Token GetToken(MathPiece mp)
    {
        if (mp.MathContext is not null)
        {
            mp.MathContext.Calculate(out Token cthird);
            return mp.Token ?? cthird;
        }
        return mp.Token ?? Token.INVALID;
    }


    

    
}
