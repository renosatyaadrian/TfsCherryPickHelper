# TFS Cherry-Pick Helper

A WPF (.NET) desktop application to automate fetching Pull Request (PR) details from TFS (Azure DevOps Server) and exporting them to Excel, in order to build a `git cherry-pick` sequence sorted by merge date from oldest to newest.

## Features

- Automatic login pop-up when credentials are empty, saved to `appsettings.json`.
- CR Name input and a list of PR URLs/IDs (one per line).
- Fetches PR details via the TFS REST API (`api-version=5.0`) using **Negotiate (Kerberos/NTLM)** authentication; falls back to Windows default credentials when no credentials are set.
- Fetches commit details via local `git log`.
- Exports an Excel file (`ClosedXML`) containing PR ID, Commit Hash, Merge Date, Message, and the command `git cherry-pick {hash} -m 1`, sorted from the oldest merge to the newest.
- Output file name: `CherryPickList_{CRName}_{ddMMyy_HHmmss}.xlsx` (spaces in the CR Name are replaced with `_`), saved to the `output/` folder in the project root (or a custom folder chosen in Settings).
- Base URL and repository name are **auto-detected from the first PR URL** — no manual configuration needed.
- Live validation: the Process button enables automatically once all required inputs are set, with the missing item shown in the status bar.
- Login is **on-demand**: the Login window appears only when needed (Test Connection / Proses without credentials), with automatic connection re-test after saving.
- Account status in the header (top-right) with a green indicator when logged in; connection status indicator next to the Test Connection button.

## Prerequisites

1. **.NET SDK 8.0** (or the version matching `TargetFramework` in `TfsCherryPickHelper.csproj`). Check with:
   ```powershell
   dotnet --version
   ```
2. **Git** installed and callable from a terminal:
   ```powershell
   git --version
   ```
3. Network access to TFS (e.g. `https://tfs.astra.co.id/ProjectName`).
4. A local clone of the target repository (e.g. `Project.Backend`) — required so `git log` can read the commits.

## Step-by-Step Setup

### Step 1 — Clone / open this project

```powershell
git clone https://github.com/renosatyaadrian/TfsCherryPickHelper.git
cd TfsCherryPickHelper
```

### Step 2 — Create the `appsettings.json` file

This file is **not pushed to Git** (it is already in `.gitignore`). Copy it from the example:

```powershell
Copy-Item appsettings.example.json appsettings.json
```

You can leave `Username`/`Password` empty — the app will ask for them via the Login window when needed.

### Step 3 — Build & run

```powershell
dotnet build
dotnet run
```

The main window opens directly with a **Getting started** guide in the Log panel. No login is forced at startup.

### Step 4 — Paste PR URLs

In **PR Links / IDs**, paste one link or ID per line, for example:

```
https://tfs.astra.co.id/ProjectName/_git/Project.Backend/pullrequest/202182
202183
202184
```

The **TFS Connection** field fills in automatically from the first full URL (Base URL + repo name). Plain PR IDs are also accepted as long as at least one full URL is present.

### Step 5 — Select the local repository

Click **Browse...** next to **Local Repository Path** and select your local clone of the target repository (e.g. `D:\Repositories\Project.Backend`).

> **Tip:** If a PR commit is not found later (`bad object` error), run `git fetch --all` in this local repository first.

### Step 6 — Log in & test connection (optional but recommended)

- Click **Login...** (top-right) and enter your TFS **Username** and **Password / PAT**, then click **Save & Continue**. The account chip in the header turns green.
- Click **Test Connection** — the status chip next to it turns green (**Connected**) on success.
- If you skip this, the login window appears automatically when you click **Proses & Generate Excel**, and the connection is re-tested automatically after saving.

### Step 7 — Process & generate Excel

1. Enter the **CR Name**, e.g. `EventH23`.
2. Click **Proses & Generate Excel** (the button enables automatically once CR Name, PR Links, and Local Repository Path are all set — in any order).
3. Watch the progress in the **Log / Status** panel. Failed PRs are logged and skipped without stopping the process.

### Step 8 — Excel output

If at least one PR was processed successfully, an Excel file is generated in the `output/` folder of the project root:

```
output\CherryPickList_EventH23_111026_103645.xlsx
```

Contents of the `CherryPickList` sheet:

| PR ID | Commit Hash | Merge Date | Message | Cherry-Pick Command |
|---|---|---|---|---|
| 202182 | a1b2c3... | 10/10/2026 09:15:00 | Merge PR 202182... | `git cherry-pick a1b2c3... -m 1` |

Rows are **sorted ascending** (oldest merge on top). Copy the **Cherry-Pick Command** column into a terminal in the target repository to run the cherry-picks in order.

## Configuration Reference (`appsettings.json`)

```json
{
  "TfsSettings": {
    "BaseUrl": "https://tfs.astra.co.id/ProjectName",
    "RepoName": "Project.Backend",
    "LocalRepoPath": "D:\\Repositories\\Project.Backend",
    "OutputDir": "",
    "Username": "",
    "Password": ""
  }
}
```

| Field | Description |
|---|---|
| `BaseUrl` | Auto-detected from the first PR URL when processing. No need to edit manually. |
| `RepoName` | Auto-detected from the first PR URL when processing. No need to edit manually. |
| `LocalRepoPath` | Local path of the target repo clone (used for `git log`). Set via the **Browse...** button. |
| `OutputDir` | Folder for generated Excel files. Empty = `output/` in the project root. Set via the **Browse...** button. |
| `Username` / `Password` | TFS credentials, filled via the **Login...** window. Empty = Windows default credentials (NTLM). |

> **Note:** For Windows paths in JSON, use `\\` (double backslash).

## Troubleshooting

| Symptom | Cause & Solution |
|---|---|
| `401 Unauthorized` from TFS | Wrong Username/Password, or the account has no access to the repo. Click **Login...** (top-right) to update credentials, then **Test Connection** again. |
| `No lastMergeCommit found` | The PR is not completed yet. Complete the PR in TFS first. |
| `git error: bad object {hash}` | The commit is not in the local clone yet. Run `git fetch --all` in the local repository, then process again. |
| `git log returned no output` | `LocalRepoPath` is wrong, or the commit is not in the local clone. Run `git fetch --all` in the local repo. |
| `git error: not a git repository` | Set `LocalRepoPath` to the correct repo clone path via **Browse...**. |
| The Proses button stays disabled | Check the status bar — it names the missing input (PR URL, Local Repository Path, or CR Name). |
| Excel file not generated | All PRs failed to process — check the error messages in the Log panel. |
| Application closes immediately | The main window was closed. Run `dotnet run` again. |

## Security

- No credentials are hardcoded in the source code.
- `appsettings.json` contains credentials and is **intentionally gitignored** — never commit this file.
- Use `appsettings.example.json` as the template for teammates.
- Generated Excel files (`output/`) are also gitignored.
