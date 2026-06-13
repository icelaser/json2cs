using System.Text.RegularExpressions;

namespace Json2Cs.Client.Generation;

/// <summary>
/// Pure, stateless helpers for turning JSON keys into valid C# identifiers and
/// for reasoning about type names. Shared between the generator and the smart-mode transformer.
/// </summary>
public static class NameUtilities
{
    private static readonly HashSet<string> PrimitiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "long", "double", "bool", "byte", "short", "uint", "ulong",
        "ushort", "sbyte", "float", "decimal", "char"
    };

    public static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
        "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
        "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
        "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
        "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    public static string ToPascalCase(string input)
    {
        var words = Regex.Split(input.Trim(), "[^A-Za-z0-9]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1));

        var result = string.Concat(words);
        if (string.IsNullOrEmpty(result))
        {
            return "Item";
        }

        return result;
    }

    public static string ToSingular(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        // Englische Pluralregeln - von spezifisch zu allgemein
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
            return word[..^3] + "y";  // categories → category, companies → company

        if (word.EndsWith("ves", StringComparison.OrdinalIgnoreCase) && word.Length > 3)
            return word[..^3] + "f";  // leaves → leaf, knives → knife (vereinfacht)

        if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
        {
            // Spezielle Fälle für -es
            if (word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("xes", StringComparison.OrdinalIgnoreCase))
                return word[..^2];  // churches → church, bushes → bush, etc.

            if (word.EndsWith("oes", StringComparison.OrdinalIgnoreCase))
                return word[..^2];  // heroes → hero

            // Reguläre -es Endungen
            return word[..^1];  // addresses → address, boxes → box
        }

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) && word.Length > 1 &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return word[..^1];  // roles → role, users → user, items → item, addresses → address

        return word;  // unverändert für Wörter die nicht auf s enden oder Sonderfälle
    }

    public static bool IsReferenceType(string typeName)
    {
        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            return true;
        }

        if (typeName.StartsWith("List<", StringComparison.Ordinal))
        {
            return true;
        }

        return !PrimitiveTypes.Contains(typeName.TrimEnd('?'));
    }

    public static bool IsPrimitiveOrSystemType(string typeName)
    {
        var systemTypes = new[] { "int", "long", "double", "string", "bool", "object", "byte", "short",
            "uint", "ulong", "ushort", "sbyte", "float", "decimal", "char", "List", "Dictionary",
            "DateOnly", "DateTimeOffset", "JsonElement" };
        return systemTypes.Contains(typeName);
    }

    /// <summary>Removes a trailing numeric suffix, keeping everything before it (e.g. Address2 → Address).</summary>
    public static string RemoveNumericSuffix(string name)
    {
        var match = Regex.Match(name, @"^(.+?)(\d+)$");
        return match.Success ? match.Groups[1].Value : name;
    }

    /// <summary>Strips trailing digits from a name (e.g. Address2 → Address).</summary>
    public static string StripNumberSuffix(string name)
    {
        return Regex.Replace(name, @"\d+$", "");
    }
}
