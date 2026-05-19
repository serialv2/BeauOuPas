# =============================================================================
#  Migrate-Sezane-Cs.ps1  (v1)
#  ----------------------------
#  Migration automatique des couleurs hex en dur dans les fichiers .cs
#  vers la palette Sezane (terracotta/creme/vert sauge).
#
#  STRATEGIE : on remplace les anciens hex par les nouveaux hex de la
#  palette Sezane. Pas de StaticResource (impossible en C#).
#
#  Usage :
#    .\Migrate-Sezane-Cs.ps1                  # DRY-RUN (preview)
#    .\Migrate-Sezane-Cs.ps1 -Apply           # Applique reellement
#    .\Migrate-Sezane-Cs.ps1 -Apply -Backup   # + ZIP backup avant
#    .\Migrate-Sezane-Cs.ps1 -Detail          # Preview avec details
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$Backup,
    [switch]$Detail,
    [string]$ProjectRoot = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($ProjectRoot)) {
    $ProjectRoot = (Get-Location).Path
}

# =============================================================================
#  CONFIGURATION
# =============================================================================

# Dossiers a exclure
$ExcludedDirs = @('bin', 'obj', '.vs', '.git', 'node_modules', 'packages', 'Platforms')

# =============================================================================
#  MAPPING ANCIENS HEX -> NOUVEAUX HEX (palette Sezane)
#  Reference :
#    Primary terracotta       = #C2754C
#    PrimaryDark              = #A35C36
#    PrimaryLight             = #D88A66
#    Accent vert sauge        = #6B7F5C
#    AccentDark               = #566649
#    BackgroundPage           = #F4EFE6
#    SurfaceCard              = #FAF6EF
#    SurfaceCardAlt           = #FFFFFF (garde blanc)
#    SurfaceMuted             = #E5DCC9
#    BorderLight              = #E5DCC9
#    BorderMedium             = #D4C7B5
#    TextPrimary              = #3D2817
#    TextSecondary            = #6B5234
#    TextTertiary             = #8A6F4A
#    TextDisabled             = #B8A589
#    Success                  = #4A7A52
#    SuccessLight             = #E8F0E5
#    Error                    = #B5482F
#    ErrorLight               = #F5E3DC
#    Warning                  = #C9943E
#    WarningLight             = #F5EBD5
#    Info                     = #5A7A8C
#    InfoLight                = #E0E8EC
#    DarkBackground           = #1A1612
#    DarkSurface              = #252019
# =============================================================================

$HexMappings = [ordered]@{

    # --- Anciennes brand colors (rebrand orange + rose) ---
    '#FF6B35'   = '#C2754C'   # ancien orange -> Primary
    '#E54E1A'   = '#A35C36'   # ancien orange dark -> PrimaryDark
    '#FFB627'   = '#6B7F5C'   # ancien jaune dore -> Accent vert sauge
    '#E91E8C'   = '#C2754C'   # ancien rose -> Primary
    '#FCE4EC'   = '#E5DCC9'   # rose pale -> SurfaceMuted beige
    '#F8BBD0'   = '#E5DCC9'   # rose pastel -> SurfaceMuted

    # --- Violet (vers Accent) ---
    '#6B46C1'   = '#6B7F5C'   # violet -> Accent
    '#9C27B0'   = '#6B7F5C'   # violet Material -> Accent
    '#3F51B5'   = '#6B7F5C'   # indigo -> Accent

    # --- Bleu Material -> Info ---
    '#2196F3'   = '#5A7A8C'   # bleu Material
    '#1976D2'   = '#5A7A8C'   # bleu fonce
    '#7DC8E5'   = '#5A7A8C'   # bleu clair
    '#B0C0E0'   = '#5A7A8C'   # bleu pale
    '#B0B0CC'   = '#5A7A8C'   # bleu gris
    '#1A3F8A'   = '#5A7A8C'   # bleu fonce custom
    '#1A1850'   = '#5A7A8C'   # bleu marine
    '#0A1648'   = '#5A7A8C'   # bleu nuit
    '#0D2255'   = '#5A7A8C'   # bleu nuit 2
    '#0A0420'   = '#5A7A8C'   # tres fonce

    # --- Vert Material -> Success ---
    '#4CAF50'   = '#4A7A52'   # vert Material
    '#2E7D32'   = '#4A7A52'   # vert fonce
    '#1B5E20'   = '#6B7F5C'   # vert tres fonce -> Accent
    '#E8F5E9'   = '#E8F0E5'   # vert pale -> SuccessLight
    '#C8E6C9'   = '#E8F0E5'   # vert pale bordure
    '#009688'   = '#5A7A8C'   # teal -> Info

    # --- Rouge Material -> Error ---
    '#F44336'   = '#B5482F'   # rouge Material
    '#C62828'   = '#B5482F'   # rouge fonce
    '#C0392B'   = '#B5482F'   # rouge orange
    '#B23A1F'   = '#B5482F'   # rouge orange custom
    '#FFEBEE'   = '#F5E3DC'   # rouge pale -> ErrorLight
    '#FFCDD2'   = '#F5E3DC'   # rouge pale bordure

    # --- Orange Material -> Warning ---
    '#FF9800'   = '#C9943E'   # orange Material
    '#F57C00'   = '#C9943E'   # orange fonce
    '#FF9500'   = '#C9943E'   # orange iOS
    '#FFB74D'   = '#C9943E'   # orange clair
    '#FFB300'   = '#C9943E'   # orange ambre
    '#E65100'   = '#C9943E'   # orange tres fonce
    '#FFF3E0'   = '#F5EBD5'   # orange pale -> WarningLight
    '#FFE9A8'   = '#F5EBD5'   # jaune pale
    '#FFE8A0'   = '#F5EBD5'   # jaune pale 2
    '#FFE0A0'   = '#F5EBD5'   # jaune pale 3
    '#FFD93D'   = '#C9943E'   # jaune vif
    '#FF5722'   = '#B5482F'   # deep orange -> Error
    '#F3E5F5'   = '#F5EBD5'   # mauve pale -> WarningLight

    # --- Backgrounds page ---
    '#F8F8F8'   = '#F4EFE6'   # fond page -> BackgroundPage
    '#F5F5F5'   = '#E5DCC9'   # fond muted -> SurfaceMuted
    '#FAFAFA'   = '#F4EFE6'   # variante
    '#F3F3F3'   = '#E5DCC9'   # variante muted

    # --- Bordures ---
    '#EEEEEE'   = '#E5DCC9'   # bordure legere
    '#DDDDDD'   = '#D4C7B5'   # bordure moyenne
    '#E0E0E0'   = '#E5DCC9'
    '#CCCCCC'   = '#D4C7B5'
    '#F0F0F0'   = '#E5DCC9'

    # --- Gris textes ---
    '#333333'   = '#3D2817'   # texte principal -> TextPrimary
    '#555555'   = '#6B5234'   # texte secondaire
    '#666666'   = '#6B5234'
    '#777777'   = '#8A6F4A'
    '#888888'   = '#8A6F4A'   # texte tertiaire
    '#888899'   = '#8A6F4A'
    '#999999'   = '#8A6F4A'
    '#9E9E9E'   = '#8A6F4A'
    '#AAAAAA'   = '#B8A589'
    '#BBBBBB'   = '#B8A589'   # placeholder/desactive

    # --- Sombres ---
    '#1A1A1A'   = '#252019'   # dark surface
    '#1A1A2A'   = '#252019'
    '#0A0A0A'   = '#1A1612'   # dark background tres fonce

    # NOTE : on NE TOUCHE PAS a #FFFFFF (blanc) ni #000000 (noir),
    # ils sont fonctionnels et utilises tels quels dans la nouvelle palette.
}

# =============================================================================
#  AFFICHAGE
# =============================================================================

function Write-Header {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  MIGRATION CHARTE SEZANE .CS - BeauOuPas v2" -ForegroundColor Cyan
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
    $backupName = "backup-cs-$timestamp.zip"
    $backupPath = Join-Path $ProjectRoot $backupName

    $files = Get-ChildItem -Path $ProjectRoot -Filter '*.cs' -Recurse -File |
        Where-Object {
            $segments = $_.FullName.Substring($ProjectRoot.Length).Split([IO.Path]::DirectorySeparatorChar)
            -not ($segments | Where-Object { $ExcludedDirs -contains $_ })
        }

    Write-Host "  Archivage de $($files.Count) fichiers .cs..." -ForegroundColor Gray

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

    foreach ($oldHex in $HexMappings.Keys) {
        $newHex = $HexMappings[$oldHex]

        # On cible UNIQUEMENT les hex entoures de guillemets : "#XXXXXX"
        # Pour eviter de toucher a des commentaires ou du code arbitraire.
        # Pattern (?i) = case insensitive
        $escapedHex = [regex]::Escape($oldHex)
        $pattern = "(?i)`"$escapedHex`""
        $replacement = "`"$newHex`""

        $regexMatches = [regex]::Matches($content, $pattern)
        if ($regexMatches.Count -gt 0) {
            foreach ($m in $regexMatches) {
                $changes += [PSCustomObject]@{
                    Old = $oldHex
                    New = $newHex
                }
            }
            $content = [regex]::Replace($content, $pattern, $replacement)
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

function Get-CsFiles {
    Get-ChildItem -Path $ProjectRoot -Filter '*.cs' -Recurse -File | Where-Object {
        $relativePath = $_.FullName.Substring($ProjectRoot.Length)
        $segments = $relativePath.Split([IO.Path]::DirectorySeparatorChar)
        $inExcludedDir = $segments | Where-Object { $ExcludedDirs -contains $_ }
        if ($inExcludedDir) { return $false }
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
$files = @(Get-CsFiles)
Write-Host "  Trouve $($files.Count) fichiers .cs a analyser" -ForegroundColor Gray

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
                $newHex = ($g.Group | Select-Object -First 1).New
                Write-Host ("         {0,3}x {1,-10} -> {2}" -f $g.Count, $g.Name, $newHex) -ForegroundColor DarkGray
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
    Write-Section "TOP 15 DES COULEURS REMPLACEES"
    $allChanges = $results | ForEach-Object { $_.Changes }
    $topColors = $allChanges | Group-Object Old | Sort-Object Count -Descending | Select-Object -First 15
    foreach ($c in $topColors) {
        $newHex = ($c.Group | Select-Object -First 1).New
        Write-Host ("  {0,4}x {1,-10} -> {2}" -f $c.Count, $c.Name, $newHex) -ForegroundColor White
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
    Write-Host "    .\Migrate-Sezane-Cs.ps1 -Apply -Backup" -ForegroundColor White
}
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
