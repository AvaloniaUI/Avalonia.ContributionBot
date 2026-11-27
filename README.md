# Avalonia Contribution Bot

A GitHub Actions bot that combines two checks on pull requests:

- **Vouching**: only contributors vouched for by a maintainer may open pull requests. Pull requests from anyone else are closed with a comment explaining how to get vouched for, and optionally labeled. For more information about vouching, see the [Vouch project](https://github.com/mitchellh/vouch), which inspired this one.
- **Contributor License Agreement**: contributors who are vouched for must accept the CLA before their pull request can be merged, by commenting `!contribution-bot agree`.

Maintainers vouch for contributors with commands in discussions. Vouches and CLA signatures are stored in a JSON file in a private repository, shared by every repository using the bot.

## Commands

| Where | Who | Command | Effect |
|---|---|---|---|
| Pull request | Author | `{prefix} agree` | Accepts the CLA |
| Pull request | Collaborators | `{prefix} recheck` | Evaluates the pull request again |
| Discussion | Maintainers | `{prefix} vouch [@user] [reason]` | Vouches for the user |
| Discussion | Maintainers | `{prefix} denounce [@user] [reason]` | Denounces the user |
| Discussion | Maintainers | `{prefix} unvouch [@user] [reason]` | Removes the vouch or denouncement |

Without `@user`, discussion commands target the author of the discussion.
Collaborators and maintainers are users with `write` permission or higher on the repository.

## Setup

### 1. Store repository

Create a private repository, for example `my-org/contributors`, with a `contributors.json` file on its `main` branch:

```json
{
  "contributors": []
}
```

The bot never creates this file: a missing file is an error.

### 2. GitHub App

The bot runs as a GitHub App, which gives its comments an identity and lets it write to the store repository.

1. Create a GitHub App in the organization owning the repositories (**Settings → Developer settings → GitHub Apps → New GitHub App**):
   - **Webhook**: uncheck **Active**. The bot runs from workflows and doesn't need webhook deliveries.
   - **Repository permissions**:

     | Permission | Access | Used for |
     |---|---|---|
     | Contents | Read and write | Updating `contributors.json` in the store repository |
     | Discussions | Read and write | Replying to discussion commands |
     | Issues | Read and write | Commenting on and labeling pull requests |
     | Pull requests | Read and write | Closing pull requests |
     | Commit statuses | Read and write | Posting the commit status |
     | Metadata | Read-only | Required by GitHub |

   - **Where can this GitHub App be installed?**: **Only on this account**.
2. Note the App's **Client ID** and generate a **private key**.
3. Install the App on the organization, selecting the store repository and every repository that uses the bot.

The workflow creates a token for each run, limited to the calling repository and the store repository. Since App permissions apply to every repository the App is installed on, this token can also write the contents of the calling repository.

### 3. Consumer repository

1. Add the App's Client ID as a variable named `CONTRIBUTION_BOT_CLIENT_ID` and the private key as a secret named `CONTRIBUTION_BOT_PRIVATE_KEY`, in the repository or the organization (**Settings → Secrets and variables → Actions**).
2. Add the workflow `.github/workflows/contribution-bot.yml`:

   ```yaml
   name: Contribution Bot

   on:
     pull_request_target:
       types: [opened, reopened, synchronize]
     issue_comment:
       types: [created]
     discussion_comment:
       types: [created]
     merge_group:
       types: [checks_requested]

   jobs:
     bot:
       name: Contribution Bot
       uses: AvaloniaUI/Avalonia.ContributionBot/.github/workflows/contribution-bot.yml@v1
       with:
         client-id: ${{ vars.CONTRIBUTION_BOT_CLIENT_ID }}
         store-repository: my-org/contributors
         command-prefix: "!my-bot"
         cla-url: https://github.com/my-org/my-repo/blob/main/CLA.md
         introduction-url: https://github.com/my-org/my-repo/discussions/categories/introductions
       secrets:
         private-key: ${{ secrets.CONTRIBUTION_BOT_PRIVATE_KEY }}
   ```

3. Make the commit status (`Avalonia Contribution / CLA` by default) a required check in the branch protection rules or rulesets of the default branch.

The workflow runs the bot on `pull_request_target`, which runs in the context of the base repository. The bot never checks out pull request code.

### Workflow inputs

| Input | Required | Default | Description |
|---|---|---|---|
| `client-id` | Yes | | Client ID of the GitHub App |
| `store-repository` | Yes | | Repository holding the contributors file (`owner/name`) |
| `command-prefix` | Yes | | Prefix starting every bot command |
| `store-branch` | No | `main` | Branch holding the contributors file |
| `store-path` | No | `contributors.json` | Path of the contributors file |
| `cla-url` | No | Avalonia's CLA | Public URL of the Contributor License Agreement |
| `introduction-url` | No | Avalonia's introductions | URL where new contributors introduce themselves |
| `status-context` | No | `Avalonia Contribution / CLA` | Context name of the commit status |
| `closed-label` | No | | Label added to pull requests closed by the bot |
| `team-label` | No | | Label added to pull requests of repository collaborators |
| `dry-run` | No | `false` | Log write operations instead of performing them |

| Secret | Description |
|---|---|
| `private-key` | Private key of the GitHub App |

Workflows that create their own token can use the composite action `AvaloniaUI/Avalonia.ContributionBot@v1` directly. It takes the same inputs except `client-id`, plus `token`, and requires a Linux x64 runner.

## Development

The bot is a .NET 11 console application, published with Native AOT.

```bash
dotnet build
```

```bash
dotnet test
```

To run the bot locally against real repositories without changing anything, pass `--dry-run` and a saved event payload. Outside GitHub Actions, the token comes from `gh auth token` when `GITHUB_TOKEN` isn't set.

```bash
dotnet run --project src/Avalonia.ContributionBot -- --dry-run --event-name pull_request_target --event-file event.json --store-repository my-org/contributors
```

## Releasing

Set the new version (for example `1.2.0`) in the `Version` property of `Directory.Build.props` and merge it to `main`, then run the **Release** workflow from `main`. The workflow:

1. Reads the version from the project, and stops if its tag already exists.
2. Builds and tests the bot, then publishes the Native AOT binary for linux-x64.
3. Generates a build provenance attestation for the binary.
4. Creates a release commit on top of `main` that pins the binary's tag and SHA-256 in the composite action, `action.yml`.
5. Pushes that commit as the tag `v1.2.0` and moves the major tag `v1` to it. The commit isn't pushed to `main`, so `action.yml` on `main` stays unpinned and only works from a release tag.
6. Creates the GitHub release with the binary.

The action downloads the binary of its own tag and checks its SHA-256 before running it, so a replaced release asset fails the check.

The reusable workflow runs the action at its major tag (`@v1`), because the release workflow can't push changes to workflow files. Before releasing a new major version, update that reference on `main`: the release fails otherwise.
