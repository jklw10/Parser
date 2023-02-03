using System.Collections.Generic;

namespace Parser;
public class MathParser
{
    public static MathParser? CurrentParser { get; private set; }
    public IValueContainer<double> DVC;
    public IValueContainer<long> IVC;
    public MathParser(IValueContainer<double> dvc, IValueContainer<long> ivc)
    {
        DVC = dvc;
        IVC = ivc;
        CurrentParser = this;
    }
    public MathContext Parse(string text)
    {
        CurrentParser = this;
        string[] tokens = OperatorGetter.SplitOperators().Split(text);
        tokens = tokens.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return WeighTokens(Tokenize(tokens));
    }
    private static MathContext WeighTokens(List<Token> tokens)
    {
        int ContextID = 0;
        //todo default operator
        MathContext floor = new() { contextOperator = Operator.NoOp };
        Operator nextConOp = Operator.NoOp;
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
        for (int i = 0; i < tokenSpan.Length; i++)
        {
            Token t = new(tokenSpan[i]);
            if(tokens.Count != 0 && tokens.Last().Type != TokenType.Operator && t.Type.HasFlag(TokenType.Operator | TokenType.ContextOpen | TokenType.ContextClose | TokenType.ContextOperator))
            {
                tokens.Add(new("*"));
            }
            if (t.Type.IsSet(TokenType.Operator) && t.Op == Operator.Dot) {
                tokens.RemoveAt(tokens.Count - 1);
                i += TryConjoinToken(tokenSpan[i-1], tokenSpan[i..^0], out t)-1;
            }
            tokens.Add(t);
        }
        return tokens;
    }
    private static int TryConjoinToken(string toJoin, Span<string> tokenSpan, out Token t)
    {
        Exception invalidToken = new(toJoin + " is an invalid token.");
        if (tokenSpan.Length<=2) throw invalidToken;
        if (!(OperatorGetter.GetOperator(tokenSpan[0], out Operator op) && op == Operator.Dot)) throw invalidToken;
        t = new(toJoin+tokenSpan[0] + tokenSpan[1]);
        if (t.Type.IsSet(TokenType.Invalid))
        {
            if(tokenSpan.Length <= 3) throw invalidToken;
            return TryConjoinToken(toJoin + tokenSpan[0] + tokenSpan[1], tokenSpan[2..^0], out t) +2;
        }
        return 2;
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
        result = Token.INVALID;
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

            result = first.ApplyOperator(second.Op, third, contextOperator);
            return second.Op == Operator.Equals;
        }

        result = first.ApplyFunction(contextOperator);
        return false;
    }
    static Token GetToken(MathPiece mp)
    {
        Token TokenOut = mp.Token ?? Token.INVALID;
        mp.MathContext?.Calculate(out TokenOut);
        return TokenOut;
    }
}
