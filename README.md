# Logseq Git Launcher keeps a Logseq graph in sync through git without any manual steps. Start it instead of Logseq, and it:

Pulls the latest changes with git pull --ff-only.
Opens Logseq and waits until you close it.
Commits everything as Sync yyyy-MM-dd HH:mm and pushes.

If a step fails, the error appears in the window with a Retry button. Built with .NET 10 and Avalonia UI. The graph folder and Logseq path are chosen in a Settings dialog.
