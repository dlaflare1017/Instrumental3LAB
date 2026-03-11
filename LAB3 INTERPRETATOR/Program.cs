using System;
using System.Collections.Generic;
using System.Linq;

namespace InterpreterMeatGrinder
{
    public class GrinderContext
    {
        public List<MeatPiece> Input { get; } = new();
        public Dictionary<string, int> MinceComposition { get; } = new(); // type grams
        public int GrindLevel { get; private set; } = 0; // how many times ground
        public string ProductType { get; private set; } = "Raw Mince";

        public int MaxHardnessAllowed { get; } = 7; // if something too hard - error

        public GrinderContext(IEnumerable<MeatPiece> pieces)
        {
            Input.AddRange(pieces);
            // initial composition from input
            foreach (var p in Input)
            {
                AddToComposition(p.Type, p.Grams);
            }
        }

        public void CheckHardness()
        {
            var tooHard = Input.Where(p => p.Hardness > MaxHardnessAllowed).ToList();
            if (tooHard.Any())
            {
                var names = string.Join(", ", tooHard.Select(t => $"{t.Type}(hard={t.Hardness})"));
                throw new InvalidOperationException($"Meat grinder jammed: too hard input -> {names}");
            }
        }

        public void AddToComposition(string type, int grams)
        {
            if (!MinceComposition.ContainsKey(type))
                MinceComposition[type] = 0;
            MinceComposition[type] += grams;
        }

        public void RemoveFromComposition(string type)
        {
            if (MinceComposition.ContainsKey(type))
                MinceComposition.Remove(type);
        }

        public void SetComposition(Dictionary<string, int> newComp)
        {
            MinceComposition.Clear();
            foreach (var kv in newComp)
                MinceComposition[kv.Key] = kv.Value;
        }

        public void Grind(int times)
        {
            GrindLevel += Math.Max(0, times);
        }

        public void Make(string product)
        {
            ProductType = product;
        }

        public string GetReport()
        {
            var comp = MinceComposition.Count == 0
                ? "(empty)"
                : string.Join(", ", MinceComposition.Select(kv => $"{kv.Key}:{kv.Value}g"));

            return $"Product: {ProductType}\nGrind level: {GrindLevel}\nComposition: {comp}";
        }
    }

    public record MeatPiece(string Type, int Grams, int Hardness);

    // AbstractExpression
    public interface IExpression
    {
        void Interpret(GrinderContext context);
    }

    public class GrindExpression : IExpression
    {
        private readonly int _times;
        public GrindExpression(int times) => _times = times;

        public void Interpret(GrinderContext context)
        {
            context.CheckHardness();
            context.Grind(_times);
        }
    }

    public class RemoveExpression : IExpression
    {
        private readonly string _type;
        public RemoveExpression(string type) => _type = type;

        public void Interpret(GrinderContext context)
        {
            context.RemoveFromComposition(_type);
        }
    }

    public class MakeExpression : IExpression
    {
        private readonly string _product;
        public MakeExpression(string product) => _product = product;

        public void Interpret(GrinderContext context)
        {
            context.Make(_product);
        }
    }

    // MIX A B = leave only selected types and sum them
    public class MixExpression : IExpression
    {
        private readonly List<string> _types;
        public MixExpression(IEnumerable<string> types) => _types = types.ToList();

        public void Interpret(GrinderContext context)
        {
            var newComp = new Dictionary<string, int>();
            foreach (var t in _types)
            {
                if (context.MinceComposition.TryGetValue(t, out int grams))
                    newComp[t] = grams;
            }
            context.SetComposition(newComp);
        }
    }

    // Sequence of expressions
    public class SequenceExpression : IExpression
    {
        private readonly List<IExpression> _expressions;
        public SequenceExpression(IEnumerable<IExpression> expressions)
            => _expressions = expressions.ToList();

        public void Interpret(GrinderContext context)
        {
            foreach (var e in _expressions)
                e.Interpret(context);
        }
    }

    // Parser for mini-language
    public static class RecipeParser
    {
        // Example:
        // GRIND 3
        // MIX Pork Beef
        // REMOVE Fat
        // MAKE Cutlet
        public static IExpression Parse(string recipeText)
        {
            var lines = recipeText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("//"))
                .ToList();

            var expressions = new List<IExpression>();

            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToUpperInvariant();

                switch (cmd)
                {
                    case "GRIND":
                        expressions.Add(new GrindExpression(int.Parse(parts[1])));
                        break;

                    case "MIX":
                        expressions.Add(new MixExpression(parts.Skip(1)));
                        break;

                    case "REMOVE":
                        expressions.Add(new RemoveExpression(parts[1]));
                        break;

                    case "MAKE":
                        expressions.Add(new MakeExpression(parts[1]));
                        break;

                    default:
                        throw new ArgumentException($"Unknown command: {parts[0]}");
                }
            }

            return new SequenceExpression(expressions);
        }
    }

    class Program
    {
        static void Main()
        {
            // Input pieces (meat chunks)
            var pieces = new List<MeatPiece>
            {
                new("Pork", 300, Hardness: 4),
                new("Beef", 200, Hardness: 6),
                new("Fat",  80,  Hardness: 2),
                // Uncomment to see too hard case:
                new("Bone", 50, Hardness: 10),
            };

            var context = new GrinderContext(pieces);

            // Recipe (rules / language)
            string recipe = @"
                // Meat grinder recipe (Interpreter demo)
                GRIND 2
                REMOVE Fat
                MIX Pork Beef
                GRIND 1
                MAKE Cutlet
            ";

            try
            {
                var program = RecipeParser.Parse(recipe);
                program.Interpret(context);

                Console.WriteLine("=== RESULT ===");
                Console.WriteLine(context.GetReport());
            }
            catch (Exception ex)
            {
                Console.WriteLine("=== ERROR ===");
                Console.WriteLine(ex.Message);
            }
        }
    }
}