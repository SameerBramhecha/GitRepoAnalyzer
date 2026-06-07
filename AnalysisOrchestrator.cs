using GitHub.Copilot;
using System.Linq;
using Microsoft.Extensions.AI;

namespace RepoAnalyzer.Core;

/// <summary>
/// Top-level controller that runs the full analysis pipeline:
///   1. Clone the repo to a temp directory
///   2. Load the instruction set
///   3. Start a CopilotClient with custom tools + instruction-set system message
///   4. Stream the analysis back, capturing the full output
///   5. Write the report into the repo
///   6. Commit and push (unless --dry-run)
///   7. Clean up the temp clone
/// </summary>
public class AnalysisOrchestrator(string? githubToken)
{
    public async Task RunAsync(AnalysisOptions options)
    {
        string? tempDir = null;

        try
        {
            // ── 1. Clone ───────────────────────────────────────────────
            tempDir = await GitOperations.CloneAsync(options.RepoUrl, options.Branch, githubToken);

            // ── 2. Load instructions ───────────────────────────────────
            var instructions = InstructionSet.Load(options.InstructionsFile);
            Console.WriteLine($"📋  Instruction set: {instructions.Name}");
            Console.WriteLine($"🤖  Model          : {instructions.Model ?? "(default)"}");
            Console.WriteLine();

            // ── 3. Analyze with Copilot ────────────────────────────────
            var result = await AnalyzeAsync(tempDir, instructions, options);

            // ── 4. Write reports ───────────────────────────────────────
            var reportPaths = await ReportWriter.WriteAsync(tempDir, result, instructions, options);

            // ── 5. Commit & push ───────────────────────────────────────
            if (!options.DryRun)
            {
                await GitOperations.CommitAndPushAsync(tempDir, reportPaths, githubToken);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("🏁  Dry-run complete. Reports are in:");
                foreach (var p in reportPaths)
                    Console.WriteLine($"    {p}");
            }
        }
        finally
        {
            // Always clean up the temp clone (best-effort, don't throw)
            if (tempDir is not null && Directory.Exists(tempDir))
            {
                try
                {
                    DeleteDirectory(tempDir);
                    Console.WriteLine("\n🧹  Temp clone removed.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\n⚠️  Failed to remove temp clone: {ex.Message}");
                }
            }
        }
    }

    // ── Analysis ───────────────────────────────────────────────────────────

    private async Task<AnalysisResult> AnalyzeAsync(
        string          repoPath,
        InstructionSet  instructions,
        AnalysisOptions options)
    {
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            GitHubToken      = githubToken,
            WorkingDirectory = repoPath,   // SDK runtime process runs inside the clone
        });

        await client.StartAsync();

        var model   = instructions.Model ?? "gpt-5";
        var content = new System.Text.StringBuilder();
        var done    = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model     = model,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode    = SystemMessageMode.Append,
                Content = instructions.BuildSystemMessage(),
            },
            Tools              = RepoTools.Create(repoPath).Cast<AIFunctionDeclaration>().ToList(),
            OnPermissionRequest = PermissionHandler.ApproveAll,  // read-only tools — no risk
        });

        session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    Console.Write(delta.Data.DeltaContent);
                    content.Append(delta.Data.DeltaContent);
                    break;

                case AssistantMessageEvent msg when content.Length == 0:
                    // Non-streaming fallback: populate content from the final event
                    Console.Write(msg.Data.Content);
                    content.Append(msg.Data.Content);
                    break;

                case SessionErrorEvent err:
                    Console.Error.WriteLine($"\n❌  Session error: {err.Data.Message}");
                    done.TrySetException(new Exception(err.Data.Message));
                    break;

                case SessionIdleEvent:
                    done.TrySetResult(true);
                    break;
            }
        });

        // Build the opening user prompt (includes a condensed repo tree)
        var repoTree   = BuildRepoTree(repoPath);
        var userPrompt = instructions.BuildUserPrompt(repoTree);

        Console.WriteLine("🚀  Copilot session started — analysis in progress…\n");
        await session.SendAsync(new MessageOptions { Prompt = userPrompt });
        await done.Task;

        Console.WriteLine("\n\n✅  Analysis complete.");

        return new AnalysisResult
        {
            Content            = content.ToString().Trim(),
            RepoUrl            = options.RepoUrl,
            Timestamp          = DateTimeOffset.UtcNow,
            InstructionSetName = instructions.Name,
            ModelUsed          = model,
        };
    }

    // ── Repo tree helper ───────────────────────────────────────────────────

    private static readonly HashSet<string> IgnoredDirs =
    [
        ".git", "node_modules", ".next", "dist", "build",
        "bin", "obj", ".vs", "__pycache__",
    ];

    private static string BuildRepoTree(string root, int maxDepth = 3)
    {
        var sb = new System.Text.StringBuilder();
        AppendTree(root, root, sb, 0, maxDepth);
        return sb.ToString().TrimEnd();
    }

    private static void AppendTree(string root, string dir, System.Text.StringBuilder sb, int depth, int max)
    {
        if (depth > max) return;

        var indent = new string(' ', depth * 2);

        try
        {
            foreach (var sub in Directory.GetDirectories(dir).Order())
            {
                var name = Path.GetFileName(sub);
                if (IgnoredDirs.Contains(name)) continue;

                sb.AppendLine($"{indent}{name}/");
                AppendTree(root, sub, sb, depth + 1, max);
            }

            foreach (var file in Directory.GetFiles(dir).Order())
                sb.AppendLine($"{indent}{Path.GetFileName(file)}");
        }
        catch (UnauthorizedAccessException) { }
    }
    private static void DeleteDirectory(string path)
    {
        // normalize attributes for files and directories, then delete recursively
        foreach (var dir in Directory.GetDirectories(path))
        {
            DeleteDirectory(dir);
            try { File.SetAttributes(dir, FileAttributes.Normal); } catch { }
        }

        foreach (var file in Directory.GetFiles(path))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            try { File.Delete(file); } catch { }
        }

        try { Directory.Delete(path, recursive: false); } catch { }
    }
}
