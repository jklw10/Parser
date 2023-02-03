# Parser
A short little c# text to math function parser
this is what a console app using this would look like
```cs
using Parser;

Console.WriteLine("Hello, World!");
Values v = new();
MathParser p = new(v,v);
var text = Console.ReadLine();

try
{
    p.Parse(text ?? "").Calculate(out Token t);
    Console.WriteLine(t.DValue + " " + t.IValue + " " + t.Op + " " + t.Type + " " + t.TokenString + " ");

    Console.WriteLine(v.backingDouble);
    Console.WriteLine(v.backingDouble2);
    Console.WriteLine(v.backingInt);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

public class Values : IValueContainer<double>, IValueContainer<long>
{
    public double backingDouble = 1000;

    public double backingDouble2 = 2000;
    public bool TryGetValueRefrence(string t, out ValueReference<double> v)
    {
        v = new() { value = ref backingDouble };
        if(t != "number.conjoined")
            v = new() { value = ref backingDouble2 };
        return true;
    }
    public long backingInt;
    public bool TryGetValueRefrence(string t, out ValueReference<long> v)
    {
        v = new() { value = ref backingInt };
        return true;
    }
}
```
