<#
.SYNOPSIS
    Builds site-diag.exe using the C# compiler bundled with .NET Framework.

.EXAMPLE
    ./build.ps1
    ./build.ps1 -Output dist/site-diag.exe
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path $PSScriptRoot 'site-diag.exe')
)

$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw '.NET Framework 4.5 or later is required (csc.exe not found).'
}

$sources = @(
    (Join-Path $PSScriptRoot 'src/Net.cs'),
    (Join-Path $PSScriptRoot 'src/Program.cs')
)
$icon = Join-Path $PSScriptRoot 'assets/site-diag.ico'

Write-Host "Compiling $($sources.Count) source files..."

& $csc /nologo /target:exe "/out:$Output" "/win32icon:$icon" /codepage:65001 /optimize+ $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

Write-Host "Built: $Output"
Get-Item $Output | Select-Object Name, Length, LastWriteTime