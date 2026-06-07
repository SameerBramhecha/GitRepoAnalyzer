using System.CommandLine;
using RepoAnalyzer.Core;

var repoUrlArg    = new Argument<string>("repo-url", "GitHub repository URL to analyze");
var instructOpt   = new Option<string>("--instructions", () => "instructions/default.json", "Path to instruction set JSON file");
var branchOpt      = new Option<string?>("--branch",      "Branch to clone (default: repo's default branch)");
var tokenOpt       = new Option<string?>("--token",       "GitHub personal access token (falls back to GITHUB_TOKEN env var)");
var copilotTokenOpt = new Option<string?>("--copilot-token", "Copilot authentication token (falls back to COPILOT_TOKEN env var)");
var outputOpt      = new Option<string>("--output",       () => "reports", "Output directory inside the repo for generated reports");
var dryRunOpt      = new Option<bool>("--dry-run",        "Analyze and write reports locally but do not commit or push");

var root = new RootCommand("Analyze a GitHub repo and commit a report back into it using GitHub Copilot SDK")
{
    repoUrlArg, instructOpt, branchOpt, tokenOpt, copilotTokenOpt, outputOpt, dryRunOpt
};

root.SetHandler(async (repoUrl, instructions, branch, token, copilotToken, output, dryRun) =>
{
    token ??= Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    copilotToken ??= Environment.GetEnvironmentVariable("COPILOT_TOKEN");

    Console.WriteLine($"🔍  Repo        : {repoUrl}");
    Console.WriteLine($"📋  Instructions: {instructions}");
    Console.WriteLine($"📂  Output dir  : {output}");
    if (dryRun) Console.WriteLine("⚠️   Dry-run mode — reports will not be committed");
    Console.WriteLine();

    var orchestrator = new AnalysisOrchestrator(token, copilotToken);
    await orchestrator.RunAsync(new AnalysisOptions
    {
        RepoUrl          = repoUrl,
        InstructionsFile = instructions,
        Branch           = branch,
        OutputDirectory  = output,
        DryRun           = dryRun,
    });
},
repoUrlArg, instructOpt, branchOpt, tokenOpt, copilotTokenOpt, outputOpt, dryRunOpt);

return await root.InvokeAsync(args);
