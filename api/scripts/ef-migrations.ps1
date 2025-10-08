<#
.SYNOPSIS
    Adds a new Entity Framework Core migration and applies it to the local SQLite database.

.DESCRIPTION
    Run from the repository root.
    Windows Terminal / PowerShell: ./api/scripts/ef-migrations.ps1 "MigrationName"
    Visual Studio Package Manager Console:
        Set-Location .\api
        ./scripts/ef-migrations.ps1 "MigrationName"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$MigrationName
)

if (-not $MigrationName) {
    Write-Error "Usage: ./api/scripts/ef-migrations.ps1 \"MigrationName\""
    exit 1
}

$ErrorActionPreference = 'Stop'
$projectPath = './api/api.csproj'
$commands = @(
    @('dotnet', 'ef', 'migrations', 'add', $MigrationName, '--project', $projectPath, '--startup-project', $projectPath, '--output-dir', 'Data/Migrations'),
    @('dotnet', 'ef', 'database', 'update', '--project', $projectPath, '--startup-project', $projectPath)
)

foreach ($command in $commands) {
    $commandDisplay = $command -join ' '
    Write-Host "Executing: $commandDisplay"
    try {
        if ($command.Length -gt 1) {
            & $command[0] @($command[1..($command.Length - 1)])
        }
        else {
            & $command[0]
        }
    }
    catch {
        Write-Error "Command failed: $commandDisplay"
        throw
    }
}
