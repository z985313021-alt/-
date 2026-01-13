
Add-Type -AssemblyName System.Drawing

$targetDir = "c:\Users\24147\xwechat_files\wxid_dm4f57ypxzt722_21a2\msg\file\2026-01\ARCGIS\ARCGIS\icons"
$files = Get-ChildItem -Path $targetDir -Filter "*.png"

foreach ($file in $files) {
    try {
        $img = [System.Drawing.Image]::FromFile($file.FullName)
        $bmpPath = $file.FullName -replace '\.png$', '.bmp'
        
        # Create a white background for the BMP since it doesn't support transparency
        $bmp = new-object System.Drawing.Bitmap($img.Width, $img.Height)
        $graph = [System.Drawing.Graphics]::FromImage($bmp)
        $graph.Clear([System.Drawing.Color]::White)
        $graph.DrawImage($img, 0, 0, $img.Width, $img.Height)
        
        $bmp.Save($bmpPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
        
        $img.Dispose()
        $bmp.Dispose()
        $graph.Dispose()

        Write-Host "Converted $($file.Name) to BMP"
    }
    catch {
        Write-Error "Failed to convert $($file.Name): $_"
    }
}
