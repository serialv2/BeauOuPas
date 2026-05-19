# =============================================================================
#  Migrate-Sezane.ps1  (v2 - ASCII safe)
#  -------------------------------------
#  Migration automatique : ancienne charte (rose/orange/violet) vers
#  nouvelle charte Sezane (terracotta/creme/vert sauge).
#
#  Usage :
#    .\Migrate-Sezane.ps1                  # DRY-RUN (preview)
#    .\Migrate-Sezane.ps1 -Apply           # Applique reellement
#    .\Migrate-Sezane.ps1 -Apply -Backup   # + ZIP backup avant
#    .\Migrate-Sezane.ps1 -Detail          # Preview avec details ligne par ligne
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$Backup,
    [switch]$Detail,
    [string]$ProjectRoot = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

# Si execute sans PSScriptRoot defini, fallback CWD
if ([string]::IsNullOrEmpty($ProjectRoot)) {
    $ProjectRoot = (Get-Location).Path
}

# =============================================================================
#  CONFIGURATION - fichiers exclus
# =============================================================================

$ExcludedFiles = @(
    'Colors.xaml',
    'Styles.xaml'
)

$ExcludedDirs = @(
    'bin', 'obj', '.vs', '.git', 'node_modules', 'packages'
)

# =============================================================================
#  MAPPING DES COULEURS
# =============================================================================

$ColorMappings = [ordered]@{

    # --- Ancienne charte rose (avant rebrand) ---
    '#E91E8C'   = '{StaticResource Primary}'
    '#FCE4EC'   = '{StaticResource SurfaceMuted}'
    '#F8BBD0'   = '{StaticResource SurfaceMuted}'
    '#F48FB1'   = '{StaticResource PrimaryLight}'

    # --- Rebrand orange (mai 2026) ---
    '#C2754C'   = '{StaticResource Primary}'
    '#A35C36'   = '{StaticResource PrimaryDark}'
    '#D88A66'   = '{StaticResource PrimaryLight}'
    '#FF6B35'   = '{StaticResource Primary}'
    '#E54E1A'   = '{StaticResource PrimaryDark}'
    '#FFB627'   = '{StaticResource Accent}'

    # --- Violet (CreateContext / cartes Home) ---
    '#6B46C1'   = '{StaticResource Accent}'
    '#8B5CF6'   = '{StaticResource Accent}'
    '#9C27B0'   = '{StaticResource Accent}'

    # --- Bleu Material (boutons duel, info) ---
    '#3F51B5'   = '{StaticResource Accent}'
    '#2196F3'   = '{StaticResource Info}'
    '#1976D2'   = '{StaticResource Info}'
    '#5A7A8C'   = '{StaticResource Info}'

    # --- Vert Material (etats Success) ---
    '#4CAF50'   = '{StaticResource Success}'
    '#2E7D32'   = '{StaticResource Success}'
    '#1B5E20'   = '{StaticResource Accent}'
    '#E8F5E9'   = '{StaticResource SuccessLight}'
    '#C8E6C9'   = '{StaticResource SuccessLight}'
    '#009688'   = '{StaticResource Info}'

    # --- Rouge Material (etats Error) ---
    '#F44336'   = '{StaticResource Error}'
    '#C62828'   = '{StaticResource Error}'
    '#FFEBEE'   = '{StaticResource ErrorLight}'
    '#FFCDD2'   = '{StaticResource ErrorLight}'

    # --- Orange Material (etats Warning) ---
    '#FF9800'   = '{StaticResource Warning}'
    '#F57C00'   = '{StaticResource Warning}'
    '#FF9500'   = '{StaticResource Warning}'
    '#FFB74D'   = '{StaticResource Warning}'
    '#E65100'   = '{StaticResource Warning}'
    '#FFF3E0'   = '{StaticResource WarningLight}'
    '#FF5722'   = '{StaticResource Error}'
    '#FFE0B2'   = '{StaticResource WarningLight}'

    # --- Backgrounds page ---
    '#F8F8F8'   = '{StaticResource BackgroundPage}'
    '#F5F5F5'   = '{StaticResource SurfaceMuted}'
    '#FAFAFA'   = '{StaticResource BackgroundPage}'
    '#F3F3F3'   = '{StaticResource SurfaceMuted}'

    # --- Bordures ---
    '#EEEEEE'   = '{StaticResource BorderLight}'
    '#DDDDDD'   = '{StaticResource BorderMedium}'
    '#E0E0E0'   = '{StaticResource BorderLight}'
    '#CCCCCC'   = '{StaticResource BorderMedium}'
    '#F0F0F0'   = '{StaticResource BorderLight}'

    # --- Gris textes ---
    '#333333'   = '{StaticResource TextPrimary}'
    '#555555'   = '{StaticResource TextSecondary}'
    '#666666'   = '{StaticResource TextSecondary}'
    '#777777'   = '{StaticResource TextTertiary}'
    '#888888'   = '{StaticResource TextTertiary}'
    '#999999'   = '{StaticResource TextTertiary}'
    '#AAAAAA'   = '{StaticResource TextDisabled}'
    '#BBBBBB'   = '{StaticResource TextDisabled}'
}

# =============================================================================
#  ATTRIBUTS XAML CIBLES
# =============================================================================

$TargetAttributes = @(
    'BackgroundColor',
    'TextColor',
    'Stroke',
    'OnColor',
    'ThumbColor',
    'MinimumTrackColor',
    'MaximumTrackColor',
    'ProgressColor',
    'PlaceholderColor',
    'TitleColor',
    'Color',
    'BarBackgroundColor',
    'BarTextColor',
    'Background'
)

# =============================================================================
#  AFFICHAGE
# =============================================================================

function Write-Header {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  MIGRATION CHARTE SEZANE - BeauOuPas v2 (Mai 2026)" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    if ($Apply) {
        Write-Host "  MODE : EXECUTION (les fichiers seront modifies)" -ForegroundColor Yellow
    } else {
        Write-Host "  MODE : DRY-RUN (preview, aucune modification)" -ForegroundColor Green
    }
    Write-Host "  Racine : $ProjectRoot" -ForegroundColor Gray
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "-- $Title --" -ForegroundColor Cyan
}

# =============================================================================
#  BACKUP
# =============================================================================

function Invoke-Backup {
    Write-Section "BACKUP"

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupName = "backup-xaml-$timestamp.zip"
    $backupPath = Join-Path $ProjectRoot $backupName

    $files = Get-ChildItem -Path $ProjectRoot -Filter '*.xaml' -Recurse -File |
        Where-Object {
            $segments = $_.FullName.Substring($ProjectRoot.Length).Split([IO.Path]::DirectorySeparatorChar)
            -not ($segments | Where-Object { $ExcludedDirs -contains $_ })
        }

    Write-Host "  Archivage de $($files.Count) fichiers .xaml..." -ForegroundColor Gray

    Compress-Archive -Path $files.FullName -DestinationPath $backupPath -Force
    Write-Host "  [OK] Backup cree : $backupName" -ForegroundColor Green
    Write-Host "    ($([math]::Round((Get-Item $backupPath).Length / 1KB, 1)) KB)" -ForegroundColor Gray
}

# =============================================================================
#  MIGRATION D'UN FICHIER
# =============================================================================

function Invoke-MigrateFile {
    param([System.IO.FileInfo]$File)

    $content = Get-Content -Path $File.FullName -Raw -Encoding UTF8
    $original = $content
    $changes = @()

    $attrPattern = ($TargetAttributes -join '|')

    foreach ($oldColor in $ColorMappings.Keys) {
        $newRef = $ColorMappings[$oldColor]

        $escapedColor = [regex]::Escape($oldColor)
        $pattern = "(?i)(\b($attrPattern)\s*=\s*`")$escapedColor(`")"

        $regexMatches = [regex]::Matches($content, $pattern)
        if ($regexMatches.Count -gt 0) {
            foreach ($m in $regexMatches) {
                $changes += [PSCustomObject]@{
                    Attribute = $m.Groups[2].Value
                    Old       = $oldColor
                    New       = $newRef
                }
            }

            $content = [regex]::Replace($content, $pattern, "`${1}$newRef`${3}")
        }
    }

    return [PSCustomObject]@{
        File         = $File
        Changes      = $changes
        OriginalText = $original
        NewText      = $content
        Modified     = ($content -ne $original)
    }
}

# =============================================================================
#  COLLECTE DES FICHIERS
# =============================================================================

function Get-XamlFiles {
    Get-ChildItem -Path $ProjectRoot -Filter '*.xaml' -Recurse -File | Where-Object {
        $relativePath = $_.FullName.Substring($ProjectRoot.Length)
        $segments = $relativePath.Split([IO.Path]::DirectorySeparatorChar)
        $inExcludedDir = $segments | Where-Object { $ExcludedDirs -contains $_ }
        if ($inExcludedDir) { return $false }

        if ($ExcludedFiles -contains $_.Name) { return $false }

        return $true
    }
}

# =============================================================================
#  MAIN
# =============================================================================

Write-Header

if ($Apply -and $Backup) {
    Invoke-Backup
}

Write-Section "ANALYSE"
$files = @(Get-XamlFiles)
Write-Host "  Trouve $($files.Count) fichiers .xaml a analyser" -ForegroundColor Gray
if ($ExcludedFiles.Count -gt 0) {
    Write-Host "  ($($ExcludedFiles.Count) fichiers exclus de la migration)" -ForegroundColor DarkGray
}

Write-Section "TRAITEMENT"

$results = @()
$totalChanges = 0
$modifiedFiles = 0

foreach ($file in $files) {
    $result = Invoke-MigrateFile -File $file
    $results += $result

    if ($result.Modified) {
        $modifiedFiles++
        $totalChanges += $result.Changes.Count

        $relativePath = $file.FullName.Substring($ProjectRoot.Length).TrimStart('\', '/')
        $changeCount = $result.Changes.Count
        Write-Host ("  [{0,3} chg] {1}" -f $changeCount, $relativePath) -ForegroundColor Yellow

        if ($Detail) {
            $grouped = $result.Changes | Group-Object Old | Sort-Object Count -Descending
            foreach ($g in $grouped) {
                $newRef = ($g.Group | Select-Object -First 1).New
                Write-Host ("         {0,3}x {1,-10} -> {2}" -f $g.Count, $g.Name, $newRef) -ForegroundColor DarkGray
            }
        }

        if ($Apply) {
            Set-Content -Path $file.FullName -Value $result.NewText -Encoding UTF8 -NoNewline
        }
    }
}

# =============================================================================
#  RESUME
# =============================================================================

Write-Section "RESUME"
Write-Host "  Fichiers analyses        : $($files.Count)" -ForegroundColor Gray
Write-Host "  Fichiers a modifier      : $modifiedFiles" -ForegroundColor Yellow
Write-Host "  Total remplacements      : $totalChanges" -ForegroundColor Yellow

if ($totalChanges -gt 0) {
    Write-Section "TOP 10 DES COULEURS REMPLACEES"
    $allChanges = $results | ForEach-Object { $_.Changes }
    $topColors = $allChanges | Group-Object Old | Sort-Object Count -Descending | Select-Object -First 10
    foreach ($c in $topColors) {
        $newRef = ($c.Group | Select-Object -First 1).New
        Write-Host ("  {0,4}x {1,-10} -> {2}" -f $c.Count, $c.Name, $newRef) -ForegroundColor White
    }

    Write-Section "ATTRIBUTS MODIFIES"
    $topAttrs = $allChanges | Group-Object Attribute | Sort-Object Count -Descending
    foreach ($a in $topAttrs) {
        Write-Host ("  {0,4}x {1}" -f $a.Count, $a.Name) -ForegroundColor White
    }
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
if ($Apply) {
    Write-Host "  [OK] MIGRATION TERMINEE - $modifiedFiles fichiers modifies" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Etape suivante : Clean Solution + supprimer bin/ et obj/" -ForegroundColor Yellow
    Write-Host "  puis Rebuild dans Visual Studio." -ForegroundColor Yellow
} else {
    Write-Host "  [INFO] DRY-RUN - Aucun fichier modifie" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Pour appliquer reellement les changements :" -ForegroundColor Yellow
    Write-Host "    .\Migrate-Sezane.ps1 -Apply -Backup" -ForegroundColor White
}
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
