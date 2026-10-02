# create-scaled-assets.ps1
# Generates scaled variants of tile assets for Microsoft Store compliance.
#
# Microsoft requires scale variants: .scale-100, .scale-125, .scale-150, .scale-200, .scale-400
# Also requires target size variants for small tiles: .targetsize-16, .targetsize-24, etc.
#
# Usage: Run from src/InControl.App directory
#   .\create-scaled-assets.ps1

Add-Type -AssemblyName System.Drawing

$ScaleFactors = @{
    'scale-100' = 1.0
    'scale-125' = 1.25
    'scale-150' = 1.5
    'scale-200' = 2.0
    'scale-400' = 4.0
}

$TargetSizes = @(16, 24, 32, 48, 256)

# Base asset definitions (base size at scale-100)
$Assets = @{
    'Square44x44Logo'   = @{ Width = 44;  Height = 44 }
    'Square71x71Logo'   = @{ Width = 71;  Height = 71 }
    'Square150x150Logo' = @{ Width = 150; Height = 150 }
    'Square310x310Logo' = @{ Width = 310; Height = 310 }
    'Wide310x150Logo'   = @{ Width = 310; Height = 150 }
    'StoreLogo'         = @{ Width = 50;  Height = 50 }
    'SplashScreen'      = @{ Width = 620; Height = 300 }
    'SmallTile'         = @{ Width = 71;  Height = 71 }
    'LargeTile'         = @{ Width = 310; Height = 310 }
}

# InControl brand colors
$BrandBackground = [System.Drawing.Color]::FromArgb(255, 24, 52, 89)   # Dark navy blue
$BrandAccent     = [System.Drawing.Color]::FromArgb(255, 74, 144, 226) # Light blue accent
$BrandText       = [System.Drawing.Color]::White

function Create-BrandedIcon {
    param(
        [string]$Path,
        [int]$Width,
        [int]$Height
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Fill with brand background
    $bgBrush = New-Object System.Drawing.SolidBrush($BrandBackground)
    $graphics.FillRectangle($bgBrush, 0, 0, $Width, $Height)

    # Draw circular accent ring
    $minDim = [Math]::Min($Width, $Height)
    $margin = [Math]::Max(2, $minDim * 0.1)
    $ringWidth = [Math]::Max(2, $minDim * 0.08)

    $pen = New-Object System.Drawing.Pen($BrandAccent, $ringWidth)
    $circleSize = $minDim - ($margin * 2) - $ringWidth
    $circleX = ($Width - $circleSize) / 2
    $circleY = ($Height - $circleSize) / 2
    $graphics.DrawEllipse($pen, $circleX, $circleY, $circleSize, $circleSize)

    # Draw "I" letter in center
    $fontSize = [Math]::Max(8, $minDim * 0.35)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush($BrandText)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $Width, $Height)
    $graphics.DrawString('I', $font, $textBrush, $rect, $format)

    # Draw 4 small dots around the "I" (like the original logo)
    $dotSize = [Math]::Max(2, $minDim * 0.03)
    $dotBrush = New-Object System.Drawing.SolidBrush($BrandAccent)
    $dotOffset = $minDim * 0.25

    # Top, Bottom, Left, Right dots
    $centerX = $Width / 2
    $centerY = $Height / 2
    $graphics.FillEllipse($dotBrush, $centerX - $dotSize/2, $centerY - $dotOffset - $dotSize/2, $dotSize, $dotSize)
    $graphics.FillEllipse($dotBrush, $centerX - $dotSize/2, $centerY + $dotOffset - $dotSize/2, $dotSize, $dotSize)
    $graphics.FillEllipse($dotBrush, $centerX - $dotOffset - $dotSize/2, $centerY - $dotSize/2, $dotSize, $dotSize)
    $graphics.FillEllipse($dotBrush, $centerX + $dotOffset - $dotSize/2, $centerY - $dotSize/2, $dotSize, $dotSize)

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)

    $graphics.Dispose()
    $bitmap.Dispose()
    $pen.Dispose()
    $bgBrush.Dispose()
    $textBrush.Dispose()
    $dotBrush.Dispose()
}

function Resize-Image {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [int]$Width,
        [int]$Height
    )

    if (Test-Path $SourcePath) {
        $source = [System.Drawing.Image]::FromFile($SourcePath)
        $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source, 0, 0, $Width, $Height)
        $bitmap.Save($DestPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bitmap.Dispose()
        $source.Dispose()
    } else {
        # Create fresh branded icon if source doesn't exist
        Create-BrandedIcon -Path $DestPath -Width $Width -Height $Height
    }
}

# Ensure Assets directory exists
$assetsDir = Join-Path $PSScriptRoot 'Assets'
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

Write-Host "Generating Microsoft Store compliant tile assets..." -ForegroundColor Cyan
Write-Host ""

# Generate base assets first (scale-100 equivalent)
Write-Host "[1/3] Creating base assets..." -ForegroundColor Yellow
foreach ($assetName in $Assets.Keys) {
    $spec = $Assets[$assetName]
    $basePath = Join-Path $assetsDir "$assetName.png"
    Create-BrandedIcon -Path $basePath -Width $spec.Width -Height $spec.Height
    Write-Host "  Created: $assetName.png ($($spec.Width)x$($spec.Height))" -ForegroundColor Gray
}

# Generate scaled variants
Write-Host ""
Write-Host "[2/3] Creating scaled variants..." -ForegroundColor Yellow
foreach ($assetName in $Assets.Keys) {
    $spec = $Assets[$assetName]
    $basePath = Join-Path $assetsDir "$assetName.png"

    foreach ($scaleName in $ScaleFactors.Keys) {
        $scale = $ScaleFactors[$scaleName]
        $scaledWidth = [Math]::Round($spec.Width * $scale)
        $scaledHeight = [Math]::Round($spec.Height * $scale)
        $scaledPath = Join-Path $assetsDir "$assetName.$scaleName.png"

        Resize-Image -SourcePath $basePath -DestPath $scaledPath -Width $scaledWidth -Height $scaledHeight
        Write-Host "  Created: $assetName.$scaleName.png ($scaledWidth x $scaledHeight)" -ForegroundColor Gray
    }
}

# Generate target size variants for Square44x44Logo (required for taskbar/start menu)
Write-Host ""
Write-Host "[3/3] Creating target size variants for Square44x44Logo..." -ForegroundColor Yellow
$basePath = Join-Path $assetsDir "Square44x44Logo.png"
foreach ($size in $TargetSizes) {
    $targetPath = Join-Path $assetsDir "Square44x44Logo.targetsize-$size.png"
    Resize-Image -SourcePath $basePath -DestPath $targetPath -Width $size -Height $size
    Write-Host "  Created: Square44x44Logo.targetsize-$size.png ($size x $size)" -ForegroundColor Gray

    # Also create unplated variants
    $unplatedPath = Join-Path $assetsDir "Square44x44Logo.targetsize-${size}_altform-unplated.png"
    Resize-Image -SourcePath $basePath -DestPath $unplatedPath -Width $size -Height $size
    Write-Host "  Created: Square44x44Logo.targetsize-${size}_altform-unplated.png" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "Asset generation complete!" -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host ""
Write-Host "Generated assets are in: $assetsDir" -ForegroundColor White
Write-Host ""
Write-Host "Next: Rebuild the project to include new assets in MSIX package" -ForegroundColor Yellow
