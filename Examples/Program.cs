using System.Reflection;
using System.Text.RegularExpressions;

namespace Examples;

internal class Program
{
    private static readonly List<Example> examples = new();

    static void Main(string[] args)
    {
        RegisterExamples();
        if (args.Length < 1)
        {
            OutputHelp();
            return;
        }

        if (!Int32.TryParse(args[0], out int exampleIndex))
        {
            OutputHelp();
            return;
        }

        var example = examples[exampleIndex];
        example.Run();
    }

    private static void OutputHelp()
    {
        Console.WriteLine("Add an example index as an argument to run the example. Available examples:");
        for (int i = 0; i < examples.Count; i++)
        {
            Console.WriteLine($"  {i, -3} {examples[i].Name}");
        }
    }

    private static void RegisterExamples()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(Example)) && !t.IsAbstract);
        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is Example example)
            {
                examples.Add(example);
            }
        }
        examples.Sort((a, b) => a.Order - b.Order);
    }
}
