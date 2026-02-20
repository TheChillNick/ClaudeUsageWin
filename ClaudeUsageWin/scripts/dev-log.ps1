# Claude Usage Win — Dev Log Fetcher
# Usage:
#   .\scripts\dev-log.ps1           # tail live (Ctrl+C to stop)
#   .\scripts\dev-log.ps1 -Last 50  # show last N lines
#   .\scripts\dev-log.ps1 -Errors   # show only ERR lines
#   .\scripts\dev-log.ps1 -Copy     # copy full log to clipboard
#   .\scripts\dev-log.ps1 -Clear    # delete log file

param(
    [int]    $Last   = 0,
    [switch] $Errors,
    [switch] $Copy,
    [switch] $Clear,
    [switch] $Help
)

$logPath = "$env:APPDATA\ClaudeUsageWin\logs\debug.log"

if ($Help) {
    Write-Host ""
    Write-Host "dev-log.ps1  —  Claude Usage Win log viewer" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  (no args)        Live tail (Ctrl+C to stop)"
    Write-Host "  -Last N          Show last N lines then exit"
    Write-Host "  -Errors          Filter to ERR lines only"
    Write-Host "  -Copy            Copy full log to clipboard"
    Write-Host "  -Clear           Delete the log file"
    Write-Host "  -Help            Show this help"
    Write-Host ""
    exit 0
}

if (-not (Test-Path $logPath)) {
    Write-Host "[dev-log] Log file not found: $logPath" -ForegroundColor Yellow
    Write-Host "[dev-log] Start the app first to generate logs." -ForegroundColor Yellow
    exit 0
}

if ($Clear) {
    Remove-Item $logPath -Force
    Write-Host "[dev-log] Log cleared." -ForegroundColor Green
    exit 0
}

if ($Copy) {
    $content = Get-Content $logPath -Raw
    $content | Set-Clipboard
    $lines = ($content -split "`n").Count
    Write-Host "[dev-log] Copied $lines lines to clipboard." -ForegroundColor Green
    exit 0
}

function Format-Line($line) {
    if ($line -match '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[ERR\]') {
        Write-Host $line -ForegroundColor Red
    } elseif ($line -match '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[INF\]') {
        $ts   = $line.Substring(0, 19)
        $rest = $line.Substring(25)
        Write-Host $ts -ForegroundColor DarkGray -NoNewline
        Write-Host " $rest"
    } else {
        Write-Host $line
    }
}

if ($Last -gt 0) {
    $lines = Get-Content $logPath -Tail $Last
    if ($Errors) { $lines = $lines | Where-Object { $_ -match '\[ERR\]' } }
    foreach ($l in $lines) { Format-Line $l }
    exit 0
}

if ($Errors) {
    # Filter mode — show existing ERR lines then watch for new ones
    Write-Host "[dev-log] Showing ERR lines only. Ctrl+C to stop." -ForegroundColor Yellow
    Get-Content $logPath | Where-Object { $_ -match '\[ERR\]' } | ForEach-Object { Format-Line $_ }
    Get-Content $logPath -Tail 0 -Wait | Where-Object { $_ -match '\[ERR\]' } | ForEach-Object { Format-Line $_ }
    exit 0
}

# Default: live tail
Write-Host "[dev-log] Tailing $logPath  (Ctrl+C to stop)" -ForegroundColor Cyan
Write-Host ""
Get-Content $logPath -Tail 20 | ForEach-Object { Format-Line $_ }
Get-Content $logPath -Tail 0 -Wait | ForEach-Object { Format-Line $_ }
