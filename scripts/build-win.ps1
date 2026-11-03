# Builds a single-file, framework-dependent LogseqGitLauncher.exe into dist\win (needs the .NET 10 Desktop Runtime)
Set-Location (Split-Path -Parent $PSScriptRoot)
Remove-Item dist\win -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish src\App\App.csproj -c Release -r win-x64 --no-self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist\win
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built: $(Resolve-Path dist\win\LogseqGitLauncher.exe)"
