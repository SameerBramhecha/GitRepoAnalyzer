# Setting Up Copilot Token for GitHub Actions

The `GitRepoAnalyzer` workflow requires a personal access token (PAT) with Copilot permissions to authenticate with the GitHub Copilot SDK.

## Step 1: Create a Fine-Grained Personal Access Token

1. Go to https://github.com/settings/personal-access-tokens/new
2. Fill in the form:
   - **Token name**: `GitRepoAnalyzer-Copilot`
   - **Expiration**: Choose as needed (90 days recommended)
   - **Description**: "Token for GitRepoAnalyzer to authenticate with Copilot SDK"
   - **Resource owner**: Your personal account (not an organization)
   - **Repository access**: Select "All repositories" (or specific repos if preferred)

3. **Grant required permissions** under **Account permissions**:
   - ✅ **Copilot Requests** (required for Copilot SDK)
   - ✅ **Read user** (recommended for user identification)

5. Click **Generate token** and copy the resulting token (format: `github_pat_...`)

**Important**: Store this token securely. You won't be able to see it again.

## Step 2: Set as Repository Secret

Use GitHub CLI to set the token as a repository secret:

```bash
gh secret set TARGET_REPO_TOKEN --body "$COPIED_TOKEN" --repo SameerBramhecha/GitRepoAnalyzer
```

Or set it via the web UI:
1. Go to https://github.com/SameerBramhecha/GitRepoAnalyzer/settings/secrets/actions
2. Click **New repository secret**
3. **Name**: `TARGET_REPO_TOKEN`
4. **Value**: Paste your fine-grained PAT
5. Click **Add secret**

## Step 3: Verify the Secret is Set

Check that the secret was created:

```bash
gh secret list --repo SameerBramhecha/GitRepoAnalyzer
```

You should see `TARGET_REPO_TOKEN` in the list.

## Step 4: Run the Workflow

Trigger the workflow manually:

1. Go to https://github.com/SameerBramhecha/GitRepoAnalyzer/actions
2. Select **Repo analysis**
3. Click **Run workflow**
4. Fill in repo URL (optional) and instruction set
5. Click **Run workflow**

Monitor the run. If successful, you'll see:
```
✅ Copilot session started — analysis in progress…
✅ Report written to reports/analysis.md
```

## Troubleshooting

**Error: "Authorization error, you may need to run /login"**
- Verify the PAT was created with the **Copilot Requests** permission
- Confirm `TARGET_REPO_TOKEN` secret is set in repository settings
- Check that the token hasn't expired

**Error: "Cannot push to target repo"**
- The workflow uses `--dry-run` and stores reports as artifacts, so this error should not occur.
- Reports are always available for download from the GitHub Actions run details.

**Token permissions reference:**
- `COPILOT_GITHUB_TOKEN` env var is set in workflow from `TARGET_REPO_TOKEN`
- The C# code receives this via `--copilot-token` argument
- Since the workflow uses `--dry-run` and stores reports as artifacts, your PAT only needs Copilot permissions—not repository write access
