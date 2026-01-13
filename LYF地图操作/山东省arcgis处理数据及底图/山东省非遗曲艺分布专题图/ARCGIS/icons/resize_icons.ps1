
Add-Type -AssemblyName System.Drawing

$targetDir = "c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\icons"
$files = Get-ChildItem -Path $targetDir -Filter "*.png"

foreach ($file in $files) {
    Write-Host "Processing $($file.Name)..."
    try {
        $img = [System.Drawing.Image]::FromFile($file.FullName)
        $newSize = New-Object System.Drawing.Size(64, 64)
        $newImg = new-object System.Drawing.Bitmap($newSize.Width, $newSize.Height)
        $graph = [System.Drawing.Graphics]::FromImage($newImg)
        $graph.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graph.DrawImage($img, 0, 0, $newSize.Width, $newSize.Height)
        
        $img.Dispose()
        
        # Save as temp then move to overwrite to avoid locking issues if possible, though Dispose should handle it.
        $tempName = $file.FullName + ".tmp.png"
        $newImg.Save($tempName, [System.Drawing.Imaging.ImageFormat]::Png)
        $newImg.Dispose()
        $graph.Dispose()

        Move-Item -Path $tempName -Destination $file.FullName -Force
        Write-Host "Resized $($file.Name) to 64x64"
    }
    catch {
        Write-Error "Failed to resize $($file.Name): $_"
    }
}
