$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$srcPath = Join-Path $root 'Shield.cs'
$text = [System.IO.File]::ReadAllText($srcPath, [System.Text.Encoding]::UTF8)

$nsMatch = [regex]::Match($text, 'namespace\s+MROSDShield\s*\{')
if (-not $nsMatch.Success) {
    throw 'namespace MROSDShield not found'
}

$usings = $text.Substring(0, $nsMatch.Index).Trim() + [Environment]::NewLine
$body = $text.Substring($nsMatch.Index + $nsMatch.Length)
$last = $body.LastIndexOf('}')
if ($last -lt 0) {
    throw 'namespace closing brace not found'
}
$body = $body.Substring(0, $last)

$declPattern = '(?m)^    (?:(?:static|public|internal|sealed|partial|abstract)\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)|^    (?:(?:static|public|internal|sealed|partial|abstract)\s+)*struct\s+([A-Za-z_][A-Za-z0-9_]*)'
$matches = [regex]::Matches($body, $declPattern)

if ($matches.Count -eq 0) {
    throw 'no top-level types found'
}

$chunks = @{}
for ($i = 0; $i -lt $matches.Count; $i++) {
    $m = $matches[$i]
    $name = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
    $start = $m.Index
    $end = if ($i + 1 -lt $matches.Count) { $matches[$i + 1].Index } else { $body.Length }
    $chunks[$name] = $body.Substring($start, $end - $start).Trim("`r", "`n") + [Environment]::NewLine
}

$groups = [ordered]@{
    'src/AppInfo.cs' = @('AppInfo')
    'src/Program.cs' = @('Program')
    'src/App.cs' = @('App')
    'src/Backend/Engine.cs' = @('Engine', 'StatusInfo')
    'src/Backend/Preferences.cs' = @('Pref')
    'src/Backend/AutoStart.cs' = @('AS')
    'src/Infrastructure/Log.cs' = @('Log')
    'src/Infrastructure/Localization.cs' = @('L')
    'src/Infrastructure/ThemeColors.cs' = @('Co')
    'src/Frontend/Controls/ToggleSwitch.cs' = @('ToggleSwitch')
    'src/Frontend/Controls/GlowCard.cs' = @('GlowCard')
    'src/Frontend/MainForm.cs' = @('MainForm')
}

$generated = New-Object 'System.Collections.Generic.HashSet[string]'

foreach ($rel in $groups.Keys) {
    $names = $groups[$rel]
    foreach ($name in $names) {
        if (-not $chunks.ContainsKey($name)) {
            throw "missing chunk for ${rel}: ${name}"
        }
    }

    $content = $usings + [Environment]::NewLine + "namespace MROSDShield" + [Environment]::NewLine + "{" + [Environment]::NewLine
    foreach ($name in $names) {
        $content += $chunks[$name] + [Environment]::NewLine
        [void]$generated.Add($name)
    }
    $content += "}" + [Environment]::NewLine

    $outPath = Join-Path $root $rel
    $outDir = Split-Path $outPath -Parent
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    }
    [System.IO.File]::WriteAllText($outPath, $content, [System.Text.UTF8Encoding]::new($false))
}

$extras = @()
foreach ($key in $chunks.Keys) {
    if (-not $generated.Contains($key)) {
        $extras += $key
    }
}
if ($extras.Count -gt 0) {
    throw ('unassigned chunks: ' + ($extras -join ', '))
}

$legacy = @'
// MR OSD Shield source has been split into src/.
// This file is intentionally kept as a migration note for older releases.
// Build with compile.bat, which compiles all .cs files under src/.

'@
[System.IO.File]::WriteAllText($srcPath, $legacy, [System.Text.UTF8Encoding]::new($false))

Write-Host 'Split complete:'
foreach ($rel in $groups.Keys) {
    Write-Host " - $rel"
}