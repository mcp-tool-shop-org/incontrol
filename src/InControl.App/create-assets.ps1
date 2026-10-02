# Create Assets directory if not exists
New-Item -ItemType Directory -Force -Path 'Assets' | Out-Null

Add-Type -AssemblyName System.Drawing

function Create-PlaceholderPng {
    param(
        [string]$path,
        [int]$width,
        [int]$height
    )

    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    # Fill with a nice blue background (InControl accent color)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 120, 212))
    $graphics.FillRectangle($brush, 0, 0, $width, $height)

    # Add 'IC' text in center
    $fontSize = [Math]::Max(8, [Math]::Min($width, $height) / 3)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $width, $height)
    $graphics.DrawString('IC', $font, $textBrush, $rect, $format)

    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)

    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Created: $path"
}

# Create all required MSIX assets
Create-PlaceholderPng 'Assets\StoreLogo.png' 50 50
Create-PlaceholderPng 'Assets\Square44x44Logo.png' 44 44
Create-PlaceholderPng 'Assets\Square71x71Logo.png' 71 71
Create-PlaceholderPng 'Assets\Square150x150Logo.png' 150 150
Create-PlaceholderPng 'Assets\Square310x310Logo.png' 310 310
Create-PlaceholderPng 'Assets\Wide310x150Logo.png' 310 150
Create-PlaceholderPng 'Assets\SmallTile.png' 71 71
Create-PlaceholderPng 'Assets\LargeTile.png' 310 310
Create-PlaceholderPng 'Assets\SplashScreen.png' 620 300

Write-Host 'All assets created successfully!'
