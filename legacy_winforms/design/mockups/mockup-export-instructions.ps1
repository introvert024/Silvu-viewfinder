# Simple PowerShell helper to open the mockup in Chrome and export PNG using headless Chrome (if installed)
# Usage: .\mockup-export-instructions.ps1 1366 768 output-1366x768.png
param(
  [int]$width = 1366,
  [int]$height = 768,
  [string]$output = "mockup.png"
)
$chrome = "C:\Program Files\Google\Chrome\Application\chrome.exe"
if(-not (Test-Path $chrome)){
  Write-Host "Chrome not found at $chrome. Open manually and use DevTools Device Screenshot or take a screenshot." -ForegroundColor Yellow
  exit 1
}
$uri = "file:///$pwd\design\mockups\index.html"
Start-Process -FilePath $chrome -ArgumentList "--headless","--disable-gpu","--window-size=$width,$height","--screenshot=$output","$uri"
Write-Host "Screenshot command issued; check $output in $pwd"