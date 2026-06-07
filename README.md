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

Copy `.github/workflows/analyze.yml` into your target repo to run analysis automatically every Monday or on demand via the **Actions** tab.

```yaml
- name: Run analysis
  env:
    GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  run: |
    repo-analyzer ${{ github.server_url }}/${{ github.repository }} \
      --instructions instructions/security.json \
      --output reports
```

The workflow uses `permissions: contents: write` so `GITHUB_TOKEN` can push the report back.

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
