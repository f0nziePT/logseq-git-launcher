# Logseq Git Launcher

Keep a [Logseq](https://logseq.com) graph in sync through git, without remembering to do it.
Start this app instead of Logseq: it pulls, opens Logseq, and commits and pushes when you close it.

Windows and macOS (Apple Silicon). Built with .NET 10 and Avalonia UI.

## The challenge

A Logseq graph is a folder of plain Markdown files, so git is a natural way to back it up and to use it on
more than one computer. The catch is that git only helps if you do the right thing at the right moment:

- **Before you open Logseq** you have to pull, otherwise you edit an old copy and create conflicts.
- **After you close Logseq** you have to commit and push, otherwise the changes stay on one machine.
- Forgetting either step is easy, and the cost shows up later: a diverged branch, a journal page edited on
  two machines, or notes you thought were saved that are only on the laptop you left at home.

A shell script can automate this, but a script is easy to get wrong in ways you only notice later:

- Logseq is an Electron app. Its launcher process can exit while the real app keeps running, so a naive
  "wait for the process" commits and pushes while you are still typing.
- A failed pull or push scrolls past in a console that closes, so nothing tells you the sync did not happen.
- Scripts are tied to one operating system, and to hard-coded paths.

## What this app does

1. **Pulls** the latest changes with `git pull --ff-only`. If the pull cannot fast-forward, Logseq is not opened,
   so you never start editing on top of an unresolved divergence.
2. **Opens Logseq** and waits until every Logseq process has gone.
3. **Commits** everything as `Sync yyyy-MM-dd HH:mm` (only if something changed) and **pushes**.

A small status window shows where it is: *Pulling*, a running timer while Logseq is open, *Syncing*, and
*Synced*. The git output is one click away under the details button.

### When something goes wrong

The error is shown in the window together with the git output, and a **Retry** button repeats only the step
that failed.

| Situation | What happens |
|---|---|
| Pull cannot fast-forward, or the network is down | Logseq is not opened. Fix the cause, then press Retry. |
| Logseq is already running | The sync does not start, because it could not tell when you are done. Close Logseq, then press Retry. |
| Push fails | Your changes are already committed locally. Press Retry once the problem is fixed. |
| Git would prompt for a password | The app fails with git's message instead of hanging, because it has no console to type into. Use SSH keys or a credential helper. |

You cannot close the window while a sync is running. Closing it between Logseq and the push would skip the
push.

## Using it

1. Download or build the app (see below) and start it.
2. On first run the **Settings** dialog opens. Choose:
   - the **graph folder**, which must be a git repository (the app checks for `.git`), and
   - the **Logseq application** (`Logseq.exe`, or `Logseq.app` on macOS). It is filled in automatically when
     Logseq is in a standard location.
3. Work in Logseq as usual. Close it when you are done, and the app commits and pushes.

Your graph needs a remote and an upstream branch already set up, for example by cloning it. The app does not
create repositories or resolve conflicts for you.

### Settings file

The settings live in a small JSON file:

| OS | Location |
|---|---|
| Windows | `%APPDATA%\LogseqGitLauncher\settings.json` |
| macOS | `~/Library/Application Support/LogseqGitLauncher/settings.json` |

```json
{
  "GraphPath": "D:\\Notes\\my-graph",
  "LogseqPath": "C:\\Users\\you\\AppData\\Local\\Logseq\\Logseq.exe"
}
```

Set the `LOGSEQ_LAUNCHER_DATA` environment variable to use a different folder, for example to keep separate
setups.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and git. The published app is
framework-dependent, so the machine that runs it needs the .NET 10 runtime.

### Windows

```powershell
.\scripts\build-win.ps1
```

Produces a single file, `dist\win\LogseqGitLauncher.exe` (about 27 MB).

### macOS (Apple Silicon)

```bash
xcode-select --install     # provides git, if you do not have it
bash ./scripts/build-mac.sh
```

Produces `dist/mac/Logseq Git Launcher.app`. It is signed ad hoc, which is enough for an app built and run on
the same Mac. If you copy the built app to another Mac, macOS will quarantine it; clear that with
`xattr -dr com.apple.quarantine "Logseq Git Launcher.app"`.

### Tests

```bash
dotnet test
```

The tests run the sync workflow against throwaway git repositories, with a stand-in for Logseq.

## How it is built

```
src/Core     Sync logic with no UI: git runner, workflow, settings, graph validation
src/App      Avalonia UI: status window and settings dialog
tests        Workflow tests against real git repositories
scripts      Build scripts for Windows and macOS, and the icon generator
```

Windows and macOS differences (where Logseq is installed, how to launch it and wait, where settings live) sit
behind a small `IPlatform` interface in `Core`. The rest of the code is shared.

Waiting for Logseq closes the loop on the Electron problem above: after the launched process exits, the app
keeps polling until no process named `Logseq` has been seen for three checks in a row.

## Limitations

- One graph at a time. To switch graphs, change the folder in Settings.
- Fast-forward pulls only. Diverged branches and merge conflicts have to be resolved by hand with git.
- Everything in the graph folder is committed, including files you did not mean to track. Use a `.gitignore`.
- Windows and macOS on Apple Silicon only.
