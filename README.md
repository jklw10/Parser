# Parser
A short little c# text to math function parser
this is what a console app using this would look like
```cs
using Parser;

Console.WriteLine("Hello, World!");
Values v = new();
var text = Console.ReadLine();
MathParser p = new(v,v, text ?? "");
p.MathContext.Calculate(out Token t);
Console.WriteLine(t.DValue+" "+ t.IValue + " " + t.Op + " " + t.Type +" " +t.TokenString + " " + t.Type);

Console.WriteLine(v.value);
public class Values : IValueContainer<double>, IValueContainer<int>
{
    public double value = 1000;
    public bool TryGetValueRefrence(string t, out ValueReference<double> v)
    {
        v = new() { value = ref this.value};
        return true;
    }
    int notrly;
    public bool TryGetValueRefrence(string t, out ValueReference<int> v)
    {
        v = new() { value = ref notrly };
        return false;
    }
}
```
