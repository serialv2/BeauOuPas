$oldFile = "AppResources.fr.original.resx"
$newFile = "AppResources.fr.new.resx"
$outputFile = "AppResources.fr.merged.resx"

[xml]$oldXml = Get-Content $oldFile -Raw
[xml]$newXml = Get-Content $newFile -Raw

# Dictionnaire des anciennes valeurs (prioritaires)
$oldDict = @{}
foreach ($data in $oldXml.root.data) {
    if ($data.name) {
        $oldDict[$data.name] = $data.value
    }
}

# Dictionnaire final
$finalDict = @{}

# 1. Ajouter toutes les clés du nouveau
foreach ($data in $newXml.root.data) {
    if ($data.name) {
        $key = $data.name

        if ($oldDict.ContainsKey($key)) {
            # 🔥 On garde l'ancien (emoji + accents)
            $finalDict[$key] = $oldDict[$key]
        }
        else {
            # sinon on garde le nouveau
            $finalDict[$key] = $data.value
        }
    }
}

# 2. Ajouter les clés manquantes de l'ancien
foreach ($key in $oldDict.Keys) {
    if (-not $finalDict.ContainsKey($key)) {
        $finalDict[$key] = $oldDict[$key]
    }
}

# 3. Génération du XML final
$xml = New-Object System.Xml.XmlDocument
$root = $xml.CreateElement("root")
$xml.AppendChild($root) | Out-Null

foreach ($key in ($finalDict.Keys | Sort-Object)) {
    $data = $xml.CreateElement("data")

    $nameAttr = $xml.CreateAttribute("name")
    $nameAttr.Value = $key
    $data.Attributes.Append($nameAttr) | Out-Null

    $spaceAttr = $xml.CreateAttribute("xml", "space", "http://www.w3.org/XML/1998/namespace")
    $spaceAttr.Value = "preserve"
    $data.Attributes.Append($spaceAttr) | Out-Null

    $value = $xml.CreateElement("value")
    $value.InnerText = $finalDict[$key]
    $data.AppendChild($value) | Out-Null

    $root.AppendChild($data) | Out-Null
}

$xml.Save($outputFile)

Write-Host "Fichier fusionné créé : $outputFile" -ForegroundColor Green