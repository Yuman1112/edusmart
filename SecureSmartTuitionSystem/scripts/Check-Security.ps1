$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
function Check-Exit([string]$Step) {
    if ($LASTEXITCODE -ne 0) { throw "$Step failed. Resolve the error before running the project." }
}
dotnet restore --use-lock-file
Check-Exit 'NuGet restore'
dotnet build --no-restore
Check-Exit 'Build'
$auditText = dotnet list package --vulnerable --include-transitive --format json 2>&1
Check-Exit 'Dependency audit'
$audit = ($auditText -join "`n") | ConvertFrom-Json
$vulnerable = @()
foreach ($project in $audit.projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($package in (@($framework.topLevelPackages) + @($framework.transitivePackages))) {
            if ($package -and @($package.vulnerabilities).Count -gt 0 -and $package.vulnerabilities) { $vulnerable += $package }
        }
    }
}
if ($vulnerable.Count -gt 0) {
    $vulnerable | ConvertTo-Json -Depth 8
    throw 'Vulnerable dependencies were reported. Update compatible packages and rerun the checks.'
}
dotnet ef migrations has-pending-model-changes
Check-Exit 'Migration model check (requires dotnet-ef 9.x)'
Write-Host 'Build, dependency audit and EF model check passed. Run the database and browser checks in SECURITY_UPDATE.md.'
# Retain packages.lock.json. Later restores can use: dotnet restore --locked-mode
