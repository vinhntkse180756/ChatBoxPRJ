param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$webProject = Join-Path $RepositoryRoot "ChatBoxPRJ/ChatBoxPRJ.Web.csproj"
$businessProject = Join-Path $RepositoryRoot "ChatBoxPRJ/ChatBoxPRJ.Business/ChatBoxPRJ.Business.csproj"
$dataProject = Join-Path $RepositoryRoot "ChatBoxPRJ/ChatBoxPRJ.DataAccess/ChatBoxPRJ.DataAccess.csproj"

function Get-ProjectReferences([string]$ProjectPath) {
    [xml]$project = Get-Content -Raw -LiteralPath $ProjectPath
    $projectDirectory = Split-Path -Parent $ProjectPath
    @($project.Project.ItemGroup.ProjectReference.Include) |
        Where-Object { $_ } |
        ForEach-Object { [IO.Path]::GetFullPath((Join-Path $projectDirectory $_)) }
}

function Assert-References([string]$ProjectPath, [string[]]$ExpectedReferences) {
    $actual = @(Get-ProjectReferences $ProjectPath | Sort-Object)
    $expected = @($ExpectedReferences | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object)
    if (Compare-Object $actual $expected) {
        throw "Unexpected project references in $ProjectPath. Expected: $($expected -join ', '). Actual: $($actual -join ', ')."
    }
}

function Assert-NoForbiddenNamespace(
    [string[]]$Paths,
    [string[]]$Patterns,
    [string]$LayerName
) {
    $violations = foreach ($path in $Paths) {
        $files = if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -Recurse -File -Include *.cs,*.cshtml |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        }
        elseif (Test-Path -LiteralPath $path -PathType Leaf) {
            Get-Item -LiteralPath $path
        }

        foreach ($file in $files) {
            foreach ($pattern in $Patterns) {
                Select-String -LiteralPath $file.FullName -Pattern $pattern -SimpleMatch |
                    ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
            }
        }
    }

    if ($violations) {
        throw "$LayerName contains forbidden dependencies:`n$($violations -join [Environment]::NewLine)"
    }
}

Assert-References $webProject @($businessProject)
Assert-References $businessProject @($dataProject)
Assert-References $dataProject @()

Assert-NoForbiddenNamespace @(
    (Join-Path $RepositoryRoot "ChatBoxPRJ/Program.cs"),
    (Join-Path $RepositoryRoot "ChatBoxPRJ/Pages"),
    (Join-Path $RepositoryRoot "ChatBoxPRJ/Infrastructure")
) @("ChatBoxPRJ.DataAccess", "Microsoft.EntityFrameworkCore") "Presentation layer"

Assert-NoForbiddenNamespace @(
    (Join-Path $RepositoryRoot "ChatBoxPRJ/ChatBoxPRJ.DataAccess")
) @("ChatBoxPRJ.Business", "ChatBoxPRJ.Pages", "ChatBoxPRJ.Infrastructure") "Data access layer"

Write-Host "Layer architecture is valid: Web -> Business -> DataAccess."
