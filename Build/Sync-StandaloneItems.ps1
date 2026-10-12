param([switch]$Check)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$path = Join-Path $root 'WMS/Standalone/WMS.Standalone.csproj'
[xml]$xml = [IO.File]::ReadAllText($path)
$ns = New-Object Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace('m', $xml.DocumentElement.NamespaceURI)
$expected = @()
foreach ($folder in @('WMS/Host.Modules', 'WMS/Infrastructure', 'WMS-HU-Model/Infrastructure', 'WMS-HU-Model/Models', 'WMS-HU-Model/Repositories', 'WMS-HU-Model/Services', 'WMS-Map/Models', 'WMS-Map/Services', 'WMS-Map/Repositories', 'WMS-Map/Infrastructure')) {
    foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $root $folder) -Filter '*.cs' -File -Recurse | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($root.Length + 1).Replace('/', '\')
        $include = '..\..\' + $relative
        $link = if ($relative.StartsWith('WMS\')) { $relative.Substring(4) } else { $relative }
        $expected += [pscustomobject]@{ Include = $include; Link = $link }
    }
}
$existing = @($xml.SelectNodes('//m:Compile', $ns) | Where-Object { $_.GetAttribute('Include').StartsWith('..\') })
if ($Check) {
    $actual = @($existing | ForEach-Object { $_.GetAttribute('Include') + '|' + $_.Link })
    $desired = @($expected | ForEach-Object { $_.Include + '|' + $_.Link })
    if (($actual -join "`n") -cne ($desired -join "`n")) { throw 'Shared file links out of date. Run Build/Sync-StandaloneItems.ps1, then reload the VS project.' }
    Write-Host "Verified $($expected.Count) explicit file links and folders."
    exit
}
$group = $xml.SelectSingleNode('//m:ItemGroup[m:Compile]', $ns)
foreach ($item in $existing) { [void]$item.ParentNode.RemoveChild($item) }
foreach ($item in $expected) {
    $node = $xml.CreateElement('Compile', $xml.DocumentElement.NamespaceURI)
    $node.SetAttribute('Include', $item.Include)
    $link = $xml.CreateElement('Link', $xml.DocumentElement.NamespaceURI)
    $link.InnerText = $item.Link
    [void]$node.AppendChild($link)
    [void]$group.AppendChild($node)
}
$xml.Save($path)
Write-Host "Updated $($expected.Count) file links. Reload the project in Visual Studio."
