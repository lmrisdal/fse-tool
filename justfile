set shell := ["powershell.exe", "-NoProfile", "-Command"]
set windows-shell := ["powershell.exe", "-NoProfile", "-Command"]

build:
    @Add-Type -Path ./fse-tool.cs -OutputAssembly ./fse-tool.exe -OutputType ConsoleApplication -ErrorAction Stop

# Run `just bump 0.2.0-beta.1` for a prerelease or `just bump 0.2.0` for a stable release.
bump version:
    @git add -A
    @git commit --allow-empty -m "Release v{{version}}"
    @git tag -a "v{{version}}" -m "Release v{{version}}"
    @Write-Host "Created commit and tag v{{version}}."
    @if ((Read-Host 'Push now? [Y/n]') -notmatch '^[nN]$') { git push -u origin HEAD; if ($LASTEXITCODE -eq 0) { git push origin 'v{{version}}' } } else { Write-Host 'Run when ready: git push -u origin HEAD && git push origin v{{version}}' }

# Generate manifests for an already-published release without submitting a PR.
winget-update version:
    @wingetcreate update lmrisdal.fse-tool --version '{{version}}' --urls 'https://github.com/lmrisdal/fse-tool/releases/download/v{{version}}/fse-tool.exe|x64' --out ".winget"

# Submit the generated manifests to the WinGet community repository.
winget-submit version:
    @wingetcreate submit ".winget/manifests/l/lmrisdal/fse-tool/{{version}}"

