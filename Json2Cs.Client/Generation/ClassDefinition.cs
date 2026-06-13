namespace Json2Cs.Client.Generation;

/// <summary>
/// An intermediate representation of a generated C# type (class or record)
/// and its properties, produced while walking the JSON and before rendering to code.
/// </summary>
public sealed record ClassDefinition(string Name, string Kind)
{
    public List<PropertyDefinition> Properties { get; } = new();
}

public sealed record PropertyDefinition(string JsonName, string Name, string Type, bool NeedsJsonAttribute, string? Comment);
