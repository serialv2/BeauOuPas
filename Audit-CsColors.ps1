# =============================================================================
#  Audit-CsColors.ps1
#  ------------------
#  Audite tous les .cs du projet et liste les couleurs en dur trouvees.
#  NE MODIFIE RIEN - juste un rapport.
#
#  Usage :
#    .\Audit-CsColors.ps1
# =============================================================================

[CmdletBinding()]
param(
    [string]$ProjectRoot = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($ProjectRoot)) {
    $ProjectRoot = (Get-Location).Path
}

$ExcludedDirs = @('bin', 'obj', '.vs', '.git', 'node_modules', 'packages', 'Platforms')

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  AUDIT COULEURS DANS LES .CS" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Racine : $ProjectRoot" -ForegroundColor Gray
Write-Host ""

$files = Get-ChildItem -Path $ProjectRoot -Filter '*.cs' -Recurse -File | Where-Object {
    $relativePath = $_.FullName.Substring($ProjectRoot.Length)
    $segments = $relativePath.Split([IO.Path]::DirectorySeparatorChar)
    -not ($segments | Where-Object { $ExcludedDirs -contains $_ })
}

Write-Host "  Trouve $($files.Count) fichiers .cs a analyser" -ForegroundColor Gray
Write-Host ""

# Patterns a detecter
$patterns = @{
    'Color.FromArgb hex'       = 'Color\.FromArgb\s*\(\s*"#[0-9A-Fa-f]{6,8}"\s*\)'
    'Color.FromHex'            = 'Color\.FromHex\s*\(\s*"#[0-9A-Fa-f]{6,8}"\s*\)'
    'Color.Parse'              = 'Color\.Parse\s*\(\s*"#[0-9A-Fa-f]{6,8}"\s*\)'
    'String hex (potentiel)'   = '"#[0-9A-Fa-f]{6,8}"'
    'Colors.Pink/Magenta'      = 'Colors\.(Pink|Magenta|HotPink|DeepPink|Orange|Purple|Violet|Indigo)'
}

$totalMatches = 0
$fileMatches = @{}

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    if ([string]::IsNullOrEmpty($content)) { continue }

    $fileFound = @{}
    foreach ($patName in $patterns.Keys) {
        $pat = $patterns[$patName]
        $regexMatches = [regex]::Matches($content, $pat)
        if ($regexMatches.Count -gt 0) {
            $fileFound[$patName] = $regexMatches.Count
            $totalMatches += $regexMatches.Count
        }
    }

    if ($fileFound.Count -gt 0) {
        $rel = $file.FullName.Substring($ProjectRoot.Length).TrimStart('\', '/')
        $fileMatches[$rel] = $fileFound
    }
}

Write-Host "-- RESULTATS PAR FICHIER --" -ForegroundColor Cyan
Write-Host ""

if ($fileMatches.Count -eq 0) {
    Write-Host "  Aucune couleur en dur trouvee dans les .cs" -ForegroundColor Green
    Write-Host ""
    return
}

foreach ($file in ($fileMatches.Keys | Sort-Object)) {
    $found = $fileMatches[$file]
    $total = ($found.Values | Measure-Object -Sum).Sum
    Write-Host ("  [{0,3}] {1}" -f $total, $file) -ForegroundColor Yellow
    foreach ($pat in $found.Keys) {
        Write-Host ("         {0,3}x {1}" -f $found[$pat], $pat) -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "-- DETAIL DES VALEURS HEX TROUVEES --" -ForegroundColor Cyan
Write-Host ""

# Extraire toutes les valeurs hex pour stats
$allHex = @{}
foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    if ([string]::IsNullOrEmpty($content)) { continue }

    $hexMatches = [regex]::Matches($content, '"(#[0-9A-Fa-f]{6,8})"')
    foreach ($m in $hexMatches) {
        $color = $m.Groups[1].Value.ToUpper()
        if ($allHex.ContainsKey($color)) {
            $allHex[$color]++
        } else {
            $allHex[$color] = 1
        }
    }
}

if ($allHex.Count -gt 0) {
    $sorted = $allHex.GetEnumerator() | Sort-Object Value -Descending
    foreach ($entry in $sorted) {
        Write-Host ("  {0,4}x {1}" -f $entry.Value, $entry.Key) -ForegroundColor White
    }
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  TOTAL : $totalMatches occurrences dans $($fileMatches.Count) fichiers" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
