using System.Globalization;
using System.Text.RegularExpressions;
namespace Wysicraft.Core;

// Recursive descent grammar. No script engine, reflection or evaluation of source code.
public static class Expressions
{
    public static string Bind(string text, IReadOnlyDictionary<string, string> state) => Regex.Replace(text, @"\$\{([a-zA-Z_][a-zA-Z0-9_]*)\}", m => state.GetValueOrDefault(m.Groups[1].Value, ""));
    public static bool Evaluate(string expression, IReadOnlyDictionary<string, string> state)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        if (expression.Length > 1024) throw new FormatException("Condition exceeds 1024 characters");
        var parser = new Parser(expression, state);
        bool result = parser.Or();
        if (parser.Peek() != "") throw new FormatException("Unexpected condition token");
        return result;
    }
    sealed class Parser
    {
        readonly List<string> tokens = []; readonly IReadOnlyDictionary<string, string> state; int pos;
        public Parser(string text, IReadOnlyDictionary<string, string> state)
        {
            this.state = state;
            var regex = new Regex("\\G\\s*(>=|<=|==|!=|&&|\\|\\||[()!<>]|\"[^\"]*\"|'[^']*'|-?\\d+(?:\\.\\d+)?|[a-zA-Z_][a-zA-Z0-9_]*)");
            int offset = 0;
            while (offset < text.Length) { if (string.IsNullOrWhiteSpace(text[offset..])) break; var m = regex.Match(text, offset); if (!m.Success) throw new FormatException("Invalid condition syntax"); tokens.Add(m.Groups[1].Value); offset += m.Length; }
        }
        public string Peek() => pos < tokens.Count ? tokens[pos] : "";
        bool Eat(params string[] choices) { if (!choices.Contains(Peek())) return false; pos++; return true; }
        public bool Or() { bool v = And(); while (Eat("OR", "||")) { bool r = And(); v |= r; } return v; }
        bool And() { bool v = Unary(); while (Eat("AND", "&&")) { bool r = Unary(); v &= r; } return v; }
        bool Unary()
        {
            if (Eat("NOT", "!")) return !Unary();
            if (Eat("(")) { bool v = Or(); if (!Eat(")")) throw new FormatException("Missing )"); return v; }
            string left = Value(); string op = Peek();
            if (!Eat("==", "!=", ">", "<", ">=", "<=")) return left.Equals("true", StringComparison.OrdinalIgnoreCase) || (double.TryParse(left, out double truth) && truth != 0);
            string right = Value();
            int compare = double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out double a) && double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out double b) ? a.CompareTo(b) : string.CompareOrdinal(left, right);
            return op switch { "==" => compare == 0, "!=" => compare != 0, ">" => compare > 0, "<" => compare < 0, ">=" => compare >= 0, _ => compare <= 0 };
        }
        string Value() { string t = Peek(); if (t == "" || t is ")" or "(") throw new FormatException("Expected value"); pos++; if (t.StartsWith('"') || t.StartsWith('\'')) return t[1..^1]; return state.GetValueOrDefault(t, t); }
    }
}

