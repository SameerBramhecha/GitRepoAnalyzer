using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepoAnalyzer.Core;

/// <summary>
/// Deserialized instruction set that drives the analysis.
/// Drop a JSON file in /instructions and point --instructions at it.
/// </summary>
public class InstructionSet
{
    public string        Name           { get; set; } = "General Analysis";
    public string?       Model          { get; set; }                       // null → SDK default
    public string?       BaseSystemPrompt { get; set; }                     // null → built-in default
    public List<string>  Checks         { get; set; } = [];                 // what to look for
    public List<string>  OutputSections { get; set; } =                     // report sections
        ["Executive Summary", "Findings", "Recommendations"];
    public string        ReportFormat   { get; set; } = "markdown";
    public string        ReportFileName { get; set; } = "analysis-report.md";

    // ── Prompt construction ────────────────────────────────────────────────

    public string BuildSystemMessage()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(BaseSystemPrompt ?? """
            You are a code-analysis expert. Thoroughly analyze the provided GitHub repository
            and produce a precise, actionable report. Use specific file paths and line numbers
            wherever possible. Be concise — avoid padding.
            """);

        if (Checks.Count > 0)
        {
            sb.AppendLine("\n<analysis_checks>");
            foreach (var check in Checks)
                sb.AppendLine($"- {check}");
            sb.AppendLine("</analysis_checks>");
        }

        sb.AppendLine($"\n<output_format>");
        sb.AppendLine($"Respond with a single {ReportFormat} document. Include these sections:");
        foreach (var section in OutputSections)
            sb.AppendLine($"- {section}");
        sb.AppendLine("Do not include any text outside the document.");
        sb.AppendLine("</output_format>");

        return sb.ToString();
    }

    /// <summary>
    /// The first user message sent to Copilot, including the repo tree so it
    /// immediately knows the shape of the project without an extra tool call.
    /// </summary>
    public string BuildUserPrompt(string repoStructure)
    {
        return $"""
            Analyze the repository below and produce a report as instructed.

            ## Repository file tree (3 levels)
            ```
            {repoStructure}
            ```

            Use the read_repo_file, list_repo_directory, and search_in_repo tools
            to explore key source files, configuration files, and documentation.
            """;
    }

    // ── Loading ────────────────────────────────────────────────────────────

    public static InstructionSet Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Instruction set not found: {filePath}");

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<InstructionSet>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas        = true,
        }) ?? throw new InvalidOperationException($"Failed to parse instruction set at {filePath}");
    }
}
