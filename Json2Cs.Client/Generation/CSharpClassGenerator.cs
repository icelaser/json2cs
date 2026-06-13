using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Json2Cs.Client.Generation;

/// <summary>
/// Converts a JSON document into C# class/record definitions and renders them to source code.
/// Stateless: all configuration is passed in via <see cref="GeneratorOptions"/>.
/// </summary>
public sealed class CSharpClassGenerator
{
    /// <summary>
    /// Parses <paramref name="json"/> and generates C# source according to <paramref name="options"/>.
    /// Throws if the input is not valid JSON (callers surface the message to the user).
    /// </summary>
    public GenerationResult Generate(string json, GeneratorOptions options)
    {
        using var document = JsonDocument.Parse(json);
        var definitions = new List<ClassDefinition>();
        var rootName = string.IsNullOrWhiteSpace(options.RootClassName) ? "Root" : options.RootClassName;
        var rootType = DetermineType(document.RootElement, rootName, definitions, options);

        if (options.SmartMode)
        {
            SmartModeTransformer.Apply(definitions, options);
        }

        var code = RenderCode(definitions, rootType, options);
        return new GenerationResult(code, definitions.Count, definitions.Sum(d => d.Properties.Count));
    }

    private static string RenderCode(List<ClassDefinition> definitions, string rootType, GeneratorOptions options)
    {
        var builder = new StringBuilder();

        if (options.UseNullableReferences)
        {
            builder.AppendLine("#nullable enable");
            builder.AppendLine();
        }

        if (options.UseList)
        {
            builder.AppendLine("using System.Collections.Generic;");
        }

        builder.AppendLine(options.AttributeMode == "system" ? "using System.Text.Json.Serialization;" : "using Newtonsoft.Json;");
        builder.AppendLine();

        // Prüfe ob DateOnly verwendet wird
        var usesDateOnly = definitions.Any(d => d.Properties.Any(p => p.Type.Contains("DateOnly")));
        if (usesDateOnly)
        {
            builder.AppendLine("using System;");
            builder.AppendLine();
        }

        foreach (var definition in definitions)
        {
            builder.AppendLine($"public {definition.Kind} {definition.Name}");
            builder.AppendLine("{");

            foreach (var property in definition.Properties)
            {
                if (!string.IsNullOrEmpty(property.Comment))
                {
                    builder.AppendLine($"    // {property.Comment}");
                }

                if (property.NeedsJsonAttribute)
                {
                    var attributeName = options.AttributeMode == "system" ? "JsonPropertyName" : "JsonProperty";
                    builder.AppendLine($"    [{attributeName}(\"{property.JsonName}\")]");
                }

                builder.AppendLine($"    public {property.Type} {property.Name} {{ {options.PropertyStyle} }}");
            }

            builder.AppendLine("}");
            builder.AppendLine();
        }

        if (definitions.Count == 0)
        {
            builder.AppendLine($"// Unable to generate types for root type: {rootType}");
        }

        var code = builder.ToString().TrimEnd();

        if (!string.IsNullOrWhiteSpace(options.Namespace))
        {
            code = $"namespace {options.Namespace}\n{{\n{IndentLines(code, "    ")}\n}}";
        }

        return code;
    }

    private static string IndentLines(string value, string indent)
    {
        return string.Join("\n", value.Split('\n').Select(line => string.IsNullOrWhiteSpace(line) ? line : indent + line));
    }

    private string DetermineType(JsonElement element, string typeName, List<ClassDefinition> definitions, GeneratorOptions options)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => BuildObjectType(element, typeName, definitions, options),
            JsonValueKind.Array => BuildArrayType(element, typeName, definitions, options),
            _ => MapPrimitiveType(element)
        };
    }

    private string BuildObjectType(JsonElement element, string typeName, List<ClassDefinition> definitions, GeneratorOptions options)
    {
        var properties = element.EnumerateObject().ToList();
        if (properties.Count == 0)
        {
            // Leeres Objekt: keine Klasse anlegen, Dictionary verwenden
            return "Dictionary<string, object>";
        }

        var baseTypeName = string.IsNullOrWhiteSpace(options.ClassSuffix) ? typeName : typeName + options.ClassSuffix;
        var uniqueName = GetUniqueTypeName(baseTypeName, definitions);
        var kind = options.DeclarationType == "record" ? "record" : "class";
        var definition = new ClassDefinition(uniqueName, kind);

        foreach (var property in properties)
        {
            var propertyName = NameUtilities.ToPascalCase(property.Name);

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                propertyName = "Item";
            }

            if (char.IsDigit(propertyName[0]))
            {
                propertyName = "_" + propertyName;
            }

            if (NameUtilities.ReservedWords.Contains(propertyName))
            {
                propertyName = "@" + propertyName;
            }

            var propertyType = DetermineType(property.Value, propertyName, definitions, options);
            var isMixedArray = propertyType.Contains("__MIXED__");
            if (isMixedArray)
            {
                propertyType = propertyType.Replace("__MIXED__", "");
            }

            if (options.UseNullableReferences && NameUtilities.IsReferenceType(propertyType))
            {
                propertyType += "?";
            }

            var needsAttribute = property.Name != propertyName || property.Name.Contains("_");
            var comment = property.Value.ValueKind == JsonValueKind.Null ? "Typ konnte nicht ermittelt werden (null-Wert im JSON)" :
                         (isMixedArray ? "Gemischter Array-Typ - bitte manuell anpassen" :
                         (propertyType == "Dictionary<string, object>" ? "Leeres Objekt im JSON - Dictionary als Fallback" : null));
            definition.Properties.Add(new PropertyDefinition(property.Name, propertyName, propertyType, needsAttribute, comment));
        }

        definitions.Add(definition);
        return uniqueName;
    }

    private string BuildArrayType(JsonElement element, string typeName, List<ClassDefinition> definitions, GeneratorOptions options)
    {
        var singularName = NameUtilities.ToSingular(typeName);

        // Früh-Check: Sammle alle ValueKinds (außer Null)
        var kinds = element.EnumerateArray()
            .Where(i => i.ValueKind != JsonValueKind.Null)
            .Select(i => i.ValueKind)
            .Distinct()
            .ToList();

        // Prüfe ob gemischt (Objekte + Primitive) → always List<object>
        var hasObjectKind = kinds.Contains(JsonValueKind.Object);
        var hasPrimitiveKind = kinds.Any(k =>
            k == JsonValueKind.String ||
            k == JsonValueKind.Number ||
            k == JsonValueKind.True ||
            k == JsonValueKind.False);

        if (hasObjectKind && hasPrimitiveKind)
        {
            return options.UseList ? "List<object>__MIXED__" : "object[]__MIXED__";
        }

        // Nur Primitive oder nur Objekte
        var itemTypes = new List<string>();

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                var itemType = DetermineType(item, singularName, definitions, options);
                itemTypes.Add(itemType);
            }
            else if (item.ValueKind == JsonValueKind.Number)
            {
                // Determine correct numeric type
                if (item.TryGetInt32(out _))
                {
                    itemTypes.Add("int");
                }
                else if (item.TryGetInt64(out _))
                {
                    itemTypes.Add("long");
                }
                else if (item.TryGetDouble(out _))
                {
                    itemTypes.Add("double");
                }
                else
                {
                    itemTypes.Add("object"); // Fallback
                }
            }
            else
            {
                var itemType = DetermineType(item, singularName, definitions, options);
                itemTypes.Add(itemType);
            }
        }

        string itemTypeResult;
        if (itemTypes.Count == 0)
        {
            itemTypeResult = "object /* Typ unbekannt - leeres Array */";
        }
        else
        {
            var firstType = itemTypes[0];
            var allSame = itemTypes.All(t => t == firstType);

            if (allSame)
            {
                itemTypeResult = firstType;
            }
            else
            {
                // Prüfe ob alle numerisch sind - dann zu größerem Typ upgraden
                var allNumeric = itemTypes.All(t => t == "int" || t == "long" || t == "double");
                if (allNumeric)
                {
                    // Upgrade zu größerem Typ wenn nötig
                    if (itemTypes.Contains("double"))
                        itemTypeResult = "double";
                    else if (itemTypes.Contains("long"))
                        itemTypeResult = "long";
                    else
                        itemTypeResult = "int";
                }
                else
                {
                    itemTypeResult = "object";
                }
            }
        }

        return options.UseList ? $"List<{itemTypeResult}>" : $"{itemTypeResult}[]";
    }

    private static string MapPrimitiveType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var strValue = element.GetString();
            if (!string.IsNullOrEmpty(strValue))
            {
                if (IsIsoDate(strValue)) return "DateOnly";
                if (IsIsoDateTime(strValue)) return "DateTimeOffset";
            }
            return "string";
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out _) ? "int" : element.TryGetInt64(out _) ? "long" : "double",
            JsonValueKind.True or JsonValueKind.False => "bool",
            JsonValueKind.Null => "object",
            _ => "object",
        };
    }

    private static bool IsIsoDateTime(string value)
    {
        // ISO-8601 Pattern: 2026-04-19T10:15:30Z etc.
        return Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}");
    }

    private static bool IsIsoDate(string value)
    {
        // Date-only Pattern: 2026-04-19
        return Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}$");
    }

    private static string GetUniqueTypeName(string baseName, List<ClassDefinition> definitions)
    {
        var name = baseName;
        var count = 1;

        while (definitions.Any(d => d.Name == name))
        {
            name = baseName + count;
            count++;
        }

        return name;
    }
}
