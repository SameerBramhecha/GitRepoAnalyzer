namespace RepoAnalyzer.Core;

/// <summary>Runtime configuration supplied by the caller.</summary>
public record AnalysisOptions
{
    public required string  RepoUrl          { get; init; }
    public required string  InstructionsFile { get; init; }
    public          string? Branch           { get; init; }
    public          string  OutputDirectory  { get; init; } = "reports";
    public          bool    DryRun           { get; init; }
}

/// <summary>Captured output from a completed Copilot analysis run.</summary>
public record AnalysisResult
{
    public required string         Content           { get; init; }
    public required string         RepoUrl           { get; init; }
    public required DateTimeOffset Timestamp         { get; init; }
    public required string         InstructionSetName { get; init; }
    public required string         ModelUsed         { get; init; }
}
