namespace Json2Cs.Client.Generation;

/// <summary>
/// User-configurable settings that control how C# code is generated from JSON.
/// Mirrors the options exposed in the settings panel of the UI.
/// </summary>
public sealed class GeneratorOptions
{
    public string DeclarationType { get; set; } = "class";
    public string AttributeMode { get; set; } = "system";
    public bool UseNullableReferences { get; set; } = true;
    public bool UseList { get; set; } = true;
    public string Namespace { get; set; } = string.Empty;
    public string RootClassName { get; set; } = "Root";
    public string PropertyStyle { get; set; } = "get; set;";
    public string ClassSuffix { get; set; } = string.Empty;
    public bool SmartMode { get; set; } = true;
}
