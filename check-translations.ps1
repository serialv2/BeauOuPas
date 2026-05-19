$projectRoot = Get-Location
$stringsPath = Join-Path $projectRoot "Resources\Strings"

if (!(Test-Path $stringsPath)) {
    Write-Host "Dossier Resources\Strings introuvable." -ForegroundColor Red
    exit
}

Write-Host "Analyse des traductions..." -ForegroundColor Cyan

# Fichiers du projet à scanner
$sourceFiles = Get-ChildItem $projectRoot -Recurse -Include *.xaml,*.cs |
    Where-Object {
        $_.FullName -notmatch "\\bin\\" -and
        $_.FullName -notmatch "\\obj\\" -and
        $_.FullName -notmatch "\\.vs\\"
    }

$usedKeys = New-Object System.Collections.Generic.HashSet[string]

foreach ($file in $sourceFiles) {
    $content = Get-Content $file.FullName -Raw

    # XAML : {tr:Translate Key=MaCle}
    [regex]::Matches($content, "tr:Translate\s+Key=([A-Za-z0-9_]+)") |
        ForEach-Object { [void]$usedKeys.Add($_.Groups[1].Value) }

    # C# : L.T("MaCle")
    [regex]::Matches($content, 'L\.T\("([A-Za-z0-9_]+)"\)') |
        ForEach-Object { [void]$usedKeys.Add($_.Groups[1].Value) }
}

$resxFiles = Get-ChildItem $stringsPath -Filter "AppResources*.resx"

$resxKeysByFile = @{}

foreach ($resx in $resxFiles) {
    [xml]$xml = Get-Content $resx.FullName
    $keys = New-Object System.Collections.Generic.HashSet[string]

    foreach ($data in $xml.root.data) {
        if ($data.name) {
            [void]$keys.Add($data.name)
        }
    }

    $resxKeysByFile[$resx.Name] = $keys
}

Write-Host ""
Write-Host "Clés utilisées trouvées : $($usedKeys.Count)" -ForegroundColor Green
Write-Host "Fichiers RESX trouvés : $($resxFiles.Count)" -ForegroundColor Green

Write-Host ""
Write-Host "=== Clés utilisées mais absentes dans les RESX ===" -ForegroundColor Yellow

foreach ($resxName in $resxKeysByFile.Keys) {
    $missing = $usedKeys | Where-Object { -not $resxKeysByFile[$resxName].Contains($_) }

    if ($missing.Count -gt 0) {
        Write-Host ""
        Write-Host "$resxName :" -ForegroundColor Magenta
        $missing | Sort-Object | ForEach-Object {
            Write-Host "  - $_"
        }
    }
}

Write-Host ""
Write-Host "=== Clés présentes dans AppResources.resx mais absentes dans une langue ===" -ForegroundColor Yellow

$baseName = "AppResources.resx"

if ($resxKeysByFile.ContainsKey($baseName)) {
    $baseKeys = $resxKeysByFile[$baseName]

    foreach ($resxName in $resxKeysByFile.Keys) {
        if ($resxName -eq $baseName) { continue }

        $missingLang = $baseKeys | Where-Object { -not $resxKeysByFile[$resxName].Contains($_) }

        if ($missingLang.Count -gt 0) {
            Write-Host ""
            Write-Host "$resxName :" -ForegroundColor Magenta
            $missingLang | Sort-Object | ForEach-Object {
                Write-Host "  - $_"
            }
        }
    }
}
else {
    Write-Host "AppResources.resx introuvable comme fichier de référence." -ForegroundColor Red
}

Write-Host ""
Write-Host "Analyse terminée." -ForegroundColor Cyan