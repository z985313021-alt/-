
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$shpPath = "c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\曲艺非遗项目.shp"
$dbfPath = "c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\曲艺非遗项目.dbf"

function Get-ShpPoints {
    param($path)
    $bytes = [System.IO.File]::ReadAllBytes($path)
    
    # Header 100 bytes
    # File Length (24-27, Big Endian)
    
    $points = @()
    $offset = 100
    while ($offset -lt $bytes.Length) {
        # Record Header (8 bytes)
        # RecNum (0-3, Big), ContentLen (4-7, Big)
        if ($offset + 8 -gt $bytes.Length) { break }
        
        $contentLen = [System.BitConverter]::ToInt32($bytes[$offset+7..($offset+4)], 0) * 2
        $offset += 8
        
        # Shape Type (0-3, Little)
        $shapeType = [System.BitConverter]::ToInt32($bytes, $offset)
        
        if ($shapeType -eq 1 -or $shapeType -eq 11 -or $shapeType -eq 21) { # Point
            $x = [System.BitConverter]::ToDouble($bytes, $offset + 4)
            $y = [System.BitConverter]::ToDouble($bytes, $offset + 12)
            $points += [PSCustomObject]@{X=$x; Y=$y}
        }
        
        $offset += $contentLen
    }
    return $points
}

function Get-DbfRecords {
    param($path)
    # Simple reader, assumes GBK/GB18030 encoding for Chinese
    $enc = [System.Text.Encoding]::GetEncoding("GB18030")
    $bytes = [System.IO.File]::ReadAllBytes($path)
    
    $headerLen = [System.BitConverter]::ToInt16($bytes, 8)
    $recLen = [System.BitConverter]::ToInt16($bytes, 10)
    $recCount = [System.BitConverter]::ToInt32($bytes, 4)
    
    # Read fields
    $fields = @()
    $fieldOffset = 32
    while ($bytes[$fieldOffset] -ne 0x0D) {
        $nameBytes = $bytes[$fieldOffset..($fieldOffset+10)]
        $name = $enc.GetString($nameBytes).Trim([char]0)
        $len = $bytes[$fieldOffset+16]
        $fields += [PSCustomObject]@{Name=$name; Length=$len}
        $fieldOffset += 32
    }
    
    $records = @()
    $currentOffset = $headerLen
    
    for ($i=0; $i -lt $recCount; $i++) {
        $recBytes = $bytes[$currentOffset..($currentOffset+$recLen-1)]
        $isDeleted = $recBytes[0] -eq 0x2A # '*'
        
        if (-not $isDeleted) {
            $rec = [Ordered]@{}
            $fPos = 1
            foreach ($f in $fields) {
                $valBytes = $recBytes[$fPos..($fPos+$f.Length-1)]
                $val = $enc.GetString($valBytes).Trim()
                $rec[$f.Name] = $val
                $fPos += $f.Length
            }
            $records += $rec
        }
        $currentOffset += $recLen
    }
    return $records
}

$pts = Get-ShpPoints $shpPath
$recs = Get-DbfRecords $dbfPath

for ($i=0; $i -lt $pts.Count; $i++) {
    $p = $pts[$i]
    $r = $recs[$i]
    Write-Host "Point $i : X=$($p.X) Y=$($p.Y) Name=$($r.'名称')" # Assuming '名称' is the field
}
