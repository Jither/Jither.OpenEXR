using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples;

public abstract class Example
{
    public abstract string Name { get; }
    public abstract int Order { get; }

    public abstract void Run();

    protected void Output()
    {
        Console.WriteLine();
    }

    protected void Output(string output)
    {
        Console.WriteLine(output);
    }

    protected void Output(string label, object value)
    {
        Console.WriteLine($"{label,-40}: {value}");
    }
}
