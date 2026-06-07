using GitHub.Copilot;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text;

namespace RepoAnalyzer.Core;

/// <summary>
/// Custom read-only tools exposed to the Copilot session.
/// These complement the built-in file tools and ensure Copilot stays
/// sandboxed within the cloned repo directory.
/// </summary>
public static class RepoTools
{
    private static readonly HashSet<string> IgnoredDirs =
    [
        ".git", "node_modules", ".next", "dist", "build",
        "bin", "obj", ".vs", "__pycache__", ".mypy_cache",
        "coverage", ".turbo", ".cache",
    ];

    public static List<AIFunction> Create(string repoPath)
    {
        // Canonicalize once so path-traversal checks are reliable
        var root = Path.GetFullPath(repoPath).TrimEnd(Path.DirectorySeparatorChar);

        return
        [
            // ── read a single file ─────────────────────────────────────
            CopilotTool.DefineTool(
                ([Description("Path to the file relative to the repo root, e.g. src/main.ts")] string path) =>
                {
                    var full = Safe(root, path);
                    if (full is null)   return "❌ Access denied — path traversal detected";
                    if (!File.Exists(full)) return $"❌ File not found: {path}";

                    var info = new FileInfo(full);
                    if (info.Length > 500_000) return $"⚠️ File too large to read ({info.Length:N0} bytes). Use search_in_repo to scan specific content.";

                    return File.ReadAllText(full);
                },
                toolOptions: new CopilotToolOptions { SkipPermission = true },
                factoryOptions: new AIFunctionFactoryOptions
                {
                    Name        = "read_repo_file",
                    Description = "Read the full content of any text file in the repository",
                }),

            // ── list a directory ───────────────────────────────────────
            CopilotTool.DefineTool(
                ([Description("Relative directory path (empty string = repo root)")] string path = "") =>
                {
                    var full = Safe(root, path);
                    if (full is null) return "❌ Access denied";
                    if (!Directory.Exists(full)) return $"❌ Directory not found: {path}";

                    var sb = new StringBuilder();
                    foreach (var dir in Directory.GetDirectories(full).Order())
                    {
                        var name = Path.GetFileName(dir);
                        if (!IgnoredDirs.Contains(name))
                            sb.AppendLine($"[dir]  {name}/");
                    }
                    foreach (var file in Directory.GetFiles(full).Order())
                        sb.AppendLine($"[file] {Path.GetFileName(file)}");

                    return sb.Length > 0 ? sb.ToString().TrimEnd() : "(empty directory)";
                },
                toolOptions: new CopilotToolOptions { SkipPermission = true },
                factoryOptions: new AIFunctionFactoryOptions
                {
                    Name        = "list_repo_directory",
                    Description = "List files and subdirectories at a given path inside the repository",
                }),

            // ── grep-style search ──────────────────────────────────────
            CopilotTool.DefineTool(
                (
                    [Description("Text or substring to search for (case-insensitive)")] string pattern,
                    [Description("File extension filter — e.g. .ts, .py, .cs — or * for all files")] string extension = "*",
                    [Description("Max number of matching lines to return (default 30)")] int maxResults = 30
                ) =>
                {
                    var glob = extension == "*" ? "*.*" : $"*{extension}";
                    var results = new List<string>();

                    foreach (var file in Directory.EnumerateFiles(root, glob, SearchOption.AllDirectories))
                    {
                        if (IsIgnored(root, file)) continue;

                        string[] lines;
                        try { lines = File.ReadAllLines(file); }
                        catch { continue; }

                        for (var i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                results.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {lines[i].Trim()}");
                                if (results.Count >= maxResults) goto done;
                            }
                        }
                    }

                    done:
                    return results.Count > 0
                        ? string.Join("\n", results)
                        : $"No matches found for '{pattern}'";
                },
                toolOptions: new CopilotToolOptions { SkipPermission = true },
                factoryOptions: new AIFunctionFactoryOptions
                {
                    Name        = "search_in_repo",
                    Description = "Grep-style search across all files in the repository. Returns matching lines with file:line references.",
                }),

            // ── repo-wide file tree ────────────────────────────────────
            CopilotTool.DefineTool(
                ([Description("Maximum depth to traverse (default 4)")] int maxDepth = 4) =>
                {
                    var sb = new StringBuilder();
                    AppendTree(root, root, sb, 0, maxDepth);
                    return sb.ToString().TrimEnd();
                },
                toolOptions: new CopilotToolOptions { SkipPermission = true },
                factoryOptions: new AIFunctionFactoryOptions
                {
                    Name        = "get_repo_tree",
                    Description = "Get a recursive file tree of the entire repository",
                }),
        ];
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>Returns the canonical full path, or null if it escapes the repo root.</summary>
    private static string? Safe(string root, string relative)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
        catch { return null; }
    }

    private static bool IsIgnored(string root, string fullPath)
    {
        var rel = Path.GetRelativePath(root, fullPath);
        return rel.Split(Path.DirectorySeparatorChar)
                  .Any(segment => IgnoredDirs.Contains(segment));
    }

    private static void AppendTree(string root, string dir, StringBuilder sb, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        var indent = new string(' ', depth * 2);

        try
        {
            foreach (var sub in Directory.GetDirectories(dir).Order())
            {
                var name = Path.GetFileName(sub);
                if (IgnoredDirs.Contains(name)) continue;

                sb.AppendLine($"{indent}{name}/");
                AppendTree(root, sub, sb, depth + 1, maxDepth);
            }

            foreach (var file in Directory.GetFiles(dir).Order())
                sb.AppendLine($"{indent}{Path.GetFileName(file)}");
        }
        catch (UnauthorizedAccessException) { }
    }
}