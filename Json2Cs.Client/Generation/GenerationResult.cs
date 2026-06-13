namespace Json2Cs.Client.Generation;

/// <summary>
/// The result of a generation run: the rendered C# code plus summary counts
/// used by the UI status line.
/// </summary>
public sealed record GenerationResult(string Code, int ClassCount, int PropertyCount)
{
    public int LineCount => string.IsNullOrWhiteSpace(Code) ? 0 : Code.Split('\n').Length;
}
