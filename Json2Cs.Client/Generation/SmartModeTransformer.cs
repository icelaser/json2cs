using System.Text.RegularExpressions;

namespace Json2Cs.Client.Generation;

/// <summary>
/// Post-processing pass applied in "Smart" mode. Cleans up the raw set of class
/// definitions discovered while walking the JSON: deduplicates and merges similar
/// classes, re-points array element types to merged classes, and removes orphans.
/// In "Strict" mode this pass is skipped and types are emitted exactly as discovered.
/// </summary>
public static class SmartModeTransformer
{
    public static void Apply(List<ClassDefinition> definitions, GeneratorOptions options)
    {
        // Phase 1: Identische Klassen deduplizieren
        var uniqueDefinitions = DeduplicateIdenticalClasses(definitions);
        definitions.Clear();
        definitions.AddRange(uniqueDefinitions);

        // Phase 2: Klassen mit ähnlichen Namen und Properties mergen
        MergeSimilarClasses(definitions);

        // Phase 3: Array-Typen nach Merge korrigieren (List<object> → List<KlassenName>)
        FixArrayTypesAfterMerge(definitions, options);

        // Phase 4: Verwaiste Klassen entfernen
        RemoveOrphanedClasses(definitions);

        // Phase 5: Korrigiere Kommentare für wirklich gemischte Array-Typen
        FixArrayTypeComments(definitions);
    }

    private static void RemoveOrphanedClasses(List<ClassDefinition> definitions)
    {
        var allReferencedTypes = new HashSet<string>();

        // Sammle alle referenzierten Typen
        foreach (var def in definitions)
        {
            foreach (var prop in def.Properties)
            {
                ExtractReferencedTypes(prop.Type, allReferencedTypes);
            }
        }

        // Entferne verwaiste Klassen (nicht Root, nicht referenziert)
        var orphaned = definitions
            .Where(d => d.Name != "Root" && !allReferencedTypes.Contains(d.Name))
            .ToList();

        foreach (var orphan in orphaned)
        {
            definitions.Remove(orphan);
        }
    }

    private static void ExtractReferencedTypes(string type, HashSet<string> types)
    {
        // Extrahiere Klassen-Namen aus: List<ClassName>, ClassName[], ClassName?, ClassName
        var matches = Regex.Matches(type, @"\b([A-Z][a-zA-Z0-9]*)\b");
        foreach (Match match in matches)
        {
            var typeName = match.Groups[1].Value;
            // Ignoriere primitive/System-Typen
            if (!NameUtilities.IsPrimitiveOrSystemType(typeName))
            {
                types.Add(typeName);
            }
        }
    }

    private static void FixArrayTypesAfterMerge(List<ClassDefinition> definitions, GeneratorOptions options)
    {
        foreach (var cls in definitions)
        {
            for (int i = 0; i < cls.Properties.Count; i++)
            {
                var prop = cls.Properties[i];
                if (prop.Type != "List<object>?" && prop.Type != "List<object>")
                    continue;

                // Versuche eine passende Klasse zu finden
                var baseName = NameUtilities.ToPascalCase(NameUtilities.ToSingular(prop.JsonName));
                var matchingClass = definitions.FirstOrDefault(d =>
                    d.Name == baseName ||
                    NameUtilities.StripNumberSuffix(d.Name) == baseName);

                if (matchingClass != null)
                {
                    // List<object> durch List<KlassenName> ersetzen
                    var isNullable = prop.Type.EndsWith("?");
                    var newType = options.UseList
                        ? $"List<{matchingClass.Name}>"
                        : $"{matchingClass.Name}[]";
                    if (isNullable) newType += "?";

                    cls.Properties[i] = new PropertyDefinition(
                        prop.JsonName,
                        prop.Name,
                        newType,
                        prop.NeedsJsonAttribute,
                        prop.Comment
                    );
                }
            }
        }
    }

    private static void FixArrayTypeComments(List<ClassDefinition> definitions)
    {
        foreach (var def in definitions)
        {
            for (int i = 0; i < def.Properties.Count; i++)
            {
                var prop = def.Properties[i];
                // Nur "gemischter Array-Typ" Kommentar behalten, wenn wirklich List<object>
                if (prop.Comment == "Gemischter Array-Typ im JSON" && !prop.Type.Contains("List<object>"))
                {
                    def.Properties[i] = new PropertyDefinition(
                        prop.JsonName,
                        prop.Name,
                        prop.Type,
                        prop.NeedsJsonAttribute,
                        null  // Kommentar entfernen, da Array jetzt typisiert ist
                    );
                }
            }
        }
    }

    private static List<ClassDefinition> DeduplicateIdenticalClasses(List<ClassDefinition> definitions)
    {
        var seen = new Dictionary<string, ClassDefinition>();
        var typeMapping = new Dictionary<string, string>();

        foreach (var def in definitions)
        {
            var sig = GetClassSignature(def);
            if (seen.ContainsKey(sig))
            {
                typeMapping[def.Name] = seen[sig].Name;
            }
            else
            {
                seen[sig] = def;
            }
        }

        if (typeMapping.Count > 0)
        {
            UpdateAllReferences(seen.Values.ToList(), typeMapping);
        }

        return seen.Values.ToList();
    }

    private static string GetClassSignature(ClassDefinition def)
    {
        var props = string.Join("|", def.Properties.OrderBy(p => p.Name).Select(p => $"{p.Name}:{p.Type}"));
        return $"{def.Kind}:{props}";
    }

    private static void MergeSimilarClasses(List<ClassDefinition> definitions)
    {
        var groups = new Dictionary<string, List<ClassDefinition>>();

        // Group classes by base name (remove numeric suffixes)
        foreach (var def in definitions)
        {
            var baseName = NameUtilities.RemoveNumericSuffix(def.Name);
            if (!groups.ContainsKey(baseName))
                groups[baseName] = new();
            groups[baseName].Add(def);
        }

        // Merge each group
        var typeMapping = new Dictionary<string, string>();
        var mergedDefs = new List<ClassDefinition>();

        foreach (var (baseName, classGroup) in groups)
        {
            if (classGroup.Count == 1)
            {
                mergedDefs.Add(classGroup[0]);
            }
            else
            {
                // Check if properties are compatible for merging
                if (AreClassesCompatible(classGroup))
                {
                    var merged = MergeClasses(baseName, classGroup);
                    mergedDefs.Add(merged);

                    // Record type mapping for references
                    foreach (var cls in classGroup)
                    {
                        if (cls.Name != merged.Name)
                            typeMapping[cls.Name] = merged.Name;
                    }
                }
                else
                {
                    mergedDefs.AddRange(classGroup);
                }
            }
        }

        if (typeMapping.Count > 0)
        {
            UpdateAllReferences(mergedDefs, typeMapping);
        }

        definitions.Clear();
        definitions.AddRange(mergedDefs);
    }

    private static bool AreClassesCompatible(List<ClassDefinition> group)
    {
        if (group.Count < 2) return false;

        // Prüfe Similarity zwischen allen Klassen
        for (int i = 0; i < group.Count; i++)
        {
            for (int j = i + 1; j < group.Count; j++)
            {
                var classA = group[i];
                var classB = group[j];

                var propsA = new HashSet<string>(classA.Properties.Select(p => p.JsonName));
                var propsB = new HashSet<string>(classB.Properties.Select(p => p.JsonName));

                // Zähle gemeinsame Properties
                var commonCount = propsA.Intersect(propsB).Count();
                var totalProps = Math.Max(propsA.Count, propsB.Count);

                // Nur mergen wenn mindestens 50% Ähnlichkeit oder mindestens 1 gemeinsame Property
                var similarity = totalProps > 0 ? (double)commonCount / totalProps : 0;
                if (commonCount == 0 || similarity < 0.5)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ClassDefinition MergeClasses(string baseName, List<ClassDefinition> group)
    {
        var merged = new ClassDefinition(baseName, group[0].Kind);
        var propMap = new Dictionary<string, PropertyDefinition>();

        // Collect all unique properties with type resolution
        foreach (var cls in group)
        {
            foreach (var prop in cls.Properties)
            {
                if (!propMap.ContainsKey(prop.Name))
                {
                    propMap[prop.Name] = prop;
                }
                else
                {
                    // Resolve type conflict
                    var existingType = propMap[prop.Name].Type;
                    var newType = prop.Type;
                    propMap[prop.Name] = new PropertyDefinition(
                        prop.JsonName,
                        prop.Name,
                        ResolveConflictingType(existingType, newType),
                        propMap[prop.Name].NeedsJsonAttribute || prop.NeedsJsonAttribute,
                        propMap[prop.Name].Comment ?? prop.Comment
                    );
                }
            }
        }

        foreach (var prop in propMap.Values)
        {
            merged.Properties.Add(prop);
        }

        return merged;
    }

    private static string ResolveConflictingType(string type1, string type2)
    {
        if (type1 == type2) return type1;

        // Remove ? suffix for comparison
        var base1 = type1.TrimEnd('?');
        var base2 = type2.TrimEnd('?');
        var isNullable = type1.EndsWith("?") || type2.EndsWith("?");

        var nonObjectType = (base1 != "object" ? base1 : null) ?? (base2 != "object" ? base2 : null);
        var result = nonObjectType ?? "object";

        return isNullable && !result.Contains("?") ? result + "?" : result;
    }

    private static void UpdateAllReferences(List<ClassDefinition> definitions, Dictionary<string, string> typeMapping)
    {
        if (typeMapping.Count == 0) return;

        foreach (var def in definitions)
        {
            for (int i = 0; i < def.Properties.Count; i++)
            {
                var prop = def.Properties[i];
                var updatedType = ApplyTypeMapping(prop.Type, typeMapping);

                if (updatedType != prop.Type)
                {
                    def.Properties[i] = new PropertyDefinition(
                        prop.JsonName,
                        prop.Name,
                        updatedType,
                        prop.NeedsJsonAttribute,
                        prop.Comment
                    );
                }
            }
        }
    }

    private static string ApplyTypeMapping(string type, Dictionary<string, string> mapping)
    {
        foreach (var (oldName, newName) in mapping)
        {
            // Replace in List<X>, X[], X?
            type = Regex.Replace(type, $@"List<{Regex.Escape(oldName)}>", $"List<{newName}>");
            type = Regex.Replace(type, $@"List<{Regex.Escape(oldName)}\?>", $"List<{newName}?>");
            type = type.Replace($"{oldName}[]", $"{newName}[]");
            type = type.Replace($"{oldName}?", $"{newName}?");
            if (type == oldName) type = newName;
        }
        return type;
    }
}
