# RepoAnalyzer

Analyze any GitHub repository using **GitHub Copilot SDK** and commit the findings back as a Markdown report — all in one command.

```
repo-analyzer https://github.com/owner/repo --instructions instructions/security.json
```

## How it works

1. **Clone** — shallow-clones the target repo into a temp directory
2. **Session** — opens a `CopilotClient` session with your instruction set as the system message and four read-only custom tools available to Copilot
3. **Analyze** — Copilot reads files, searches code, and applies the checks you defined
4. **Report** — generates a structured Markdown document in `reports/`
5. **Commit** — stages the report and pushes it back to the repo

## Installation

```bash
dotnet tool install --global repo-analyzer
```

Requires .NET 8+ and GitHub Copilot CLI in `PATH`.

## Quickstart

```bash
# General quality analysis
repo-analyzer https://github.com/owner/repo

# Security audit, dry run (no commit)
repo-analyzer https://github.com/owner/repo \
  --instructions instructions/security.json \
  --dry-run

# Custom branch, explicit token
repo-analyzer https://github.com/owner/repo \
  --branch main \
  --token ghp_...
```

Set `GITHUB_TOKEN` as an env var to avoid passing `--token` every time.

## CLI reference

| Flag | Default | Description |
|---|---|---|
| `repo-url` | *(required)* | HTTPS URL of the repo to analyze |
| `--instructions` | `instructions/default.json` | Instruction set JSON file |
| `--branch` | *(repo default)* | Branch to clone |
| `--token` | `$GITHUB_TOKEN` | GitHub PAT (needs `repo` scope) |
| `--output` | `reports` | Directory inside the repo for reports |
| `--dry-run` | `false` | Write reports locally, skip commit & push |

## Instruction sets

An instruction set is a small JSON file that tells Copilot what to look for and how to structure the report.

```json
{
  "name": "My custom analysis",
  "model": "gpt-5",
  "checks": [
    "Are all API endpoints authenticated?",
    "Are environment variables used for all secrets?",
    "Is there a CHANGELOG?"
  ],
  "outputSections": [
    "Summary",
    "Findings",
    "Recommendations"
  ],
  "reportFormat": "markdown",
  "reportFileName": "my-report.md"
}
```

| Field | Default | Notes |
|---|---|---|
| `name` | `"General Analysis"` | Used as the report title |
| `model` | `gpt-5` | Any model accepted by `SessionConfig.Model` |
| `baseSystemPrompt` | *(built-in)* | Override the system role entirely |
| `checks` | `[]` | Bullet-point list passed to the system message |
| `outputSections` | `["Executive Summary", …]` | Sections Copilot must include |
| `reportFormat` | `markdown` | `markdown` or `json` |
| `reportFileName` | `analysis-report.md` | File name written into `--output` |

Bundled instruction sets:
- `instructions/default.json` — general code quality
- `instructions/security.json` — security audit with CVSS ratings

## GitHub Actions

The repository includes a workflow at `.github/workflows/analyze.yml` that analyzes a repository and stores the report as a GitHub Actions artifact (retained for 30 days).

**⚠️ Important: Before running the workflow, set up Copilot authentication** — see [SETUP_COPILOT_TOKEN.md](SETUP_COPILOT_TOKEN.md) for step-by-step instructions. The workflow requires a fine-grained PAT with **Copilot Requests** permission.

Using the workflow from this repo

- To run analysis against this repository, open the **Actions** tab, choose the **Repo analysis** workflow and click **Run workflow**. Choose the instruction set (default or security) and run.

- To analyze a different repository, the workflow accepts a `repo` input (full URL or `owner/repo`). Example values:
  - `https://github.com/owner/repo`
  - `owner/repo`

- The workflow **does not modify the target repository** — it only generates a report and uploads it as a GitHub Actions artifact. The artifact is retained for 30 days and can be downloaded from the Actions run details.
- Note: `GITHUB_TOKEN` is not sufficient for Copilot SDK authentication in GitHub Actions. Provide a user PAT or Copilot token with access to Copilot APIs via `TARGET_REPO_TOKEN` or `COPILOT_TOKEN`.

Run the workflow from the Actions UI or use the GitHub CLI:

```bash
# Run the workflow from this repo's default branch and analyze another repo
gh workflow run "Repo analysis" --ref main \
  --field repo="https://github.com/owner/repo" \
  --field instructions=security
```

Notes and permissions
- The workflow publishes this repository and runs `dotnet out/repo-analyzer.dll` with `--dry-run` to skip git operations.
- **Copilot authentication** is handled via environment variable `COPILOT_GITHUB_TOKEN` (set from `TARGET_REPO_TOKEN` or `GITHUB_TOKEN`).
- Reports are uploaded as GitHub Actions artifacts (30-day retention). Download them from the Actions run details.
- **Do not use `GITHUB_TOKEN` for Copilot** — GitHub's automatic token lacks Copilot permissions. Always set `TARGET_REPO_TOKEN` with a fine-grained PAT as shown in [SETUP_COPILOT_TOKEN.md](SETUP_COPILOT_TOKEN.md).


Advanced: trigger via API

You can also call the workflow dispatch endpoint directly. Example (replace placeholders):

```bash
curl -X POST \
  -H "Accept: application/vnd.github+json" \
  -H "Authorization: Bearer <YOUR_TOKEN>" \
  https://api.github.com/repos/<owner>/<repo>/actions/workflows/analyze.yml/dispatches \
  -d '{"ref":"main","inputs":{"repo":"https://github.com/owner/target","instructions":"default"}}'
```

Local testing

You can also run the analyzer locally (fast feedback):

```bash
dotnet tool install --global repo-analyzer
repo-analyzer https://github.com/owner/repo --instructions instructions/security.json --dry-run
```

The `--dry-run` flag prevents commits/pushes and is useful for testing.

## Custom tools available to Copilot

The session exposes four sandboxed read-only tools — Copilot uses these to explore the repo:

| Tool | Description |
|---|---|
| `read_repo_file` | Read the full content of a file |
| `list_repo_directory` | List files and dirs at a path |
| `search_in_repo` | Grep-style search across all files |
| `get_repo_tree` | Recursive file tree (configurable depth) |

All paths are validated to prevent traversal outside the cloned directory.

## License

MIT
