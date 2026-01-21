# GekkoLab Deployment Script for Raspberry Pi (PowerShell)
# Usage: .\deploy.ps1 -PiHost <hostname-or-ip> -PiUser <username>
# Example: .\deploy.ps1 -PiHost 192.168.1.100 -PiUser pi

param(
    [Parameter(Mandatory=$true)]
    [string]$PiHost,
    
    [Parameter(Mandatory=$false)]
    [string]$PiUser = "pi"
)

$AppName = "GekkoLab"
$RemoteDir = "/home/$PiUser/gekkolab"
$ScriptDir = $PSScriptRoot
$ProjectDir = "$ScriptDir\..\GekkoLab"
$PublishDir = "$ProjectDir\publish"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "GekkoLab Deployment to Raspberry Pi" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Target: $PiUser@$PiHost"
Write-Host "Remote directory: $RemoteDir"
Write-Host ""

# Step 1: Publish the application for ARM64
Write-Host "[1/5] Publishing application for linux-arm64..." -ForegroundColor Yellow
Push-Location $ProjectDir
dotnet publish -c Release -r linux-arm64 --self-contained true -o $PublishDir
$publishResult = $LASTEXITCODE
Pop-Location

if ($publishResult -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Step 2: Create remote directory
Write-Host "[2/5] Creating remote directory..." -ForegroundColor Yellow
ssh "$PiUser@$PiHost" "mkdir -p $RemoteDir/gekkodata"

# Step 3: Copy files to Pi
Write-Host "[3/5] Copying files to Raspberry Pi (this may take a few minutes)..." -ForegroundColor Yellow
scp -r "$PublishDir/*" "${PiUser}@${PiHost}:${RemoteDir}/"

# Step 4: Set permissions
Write-Host "[4/5] Setting permissions..." -ForegroundColor Yellow
ssh "$PiUser@$PiHost" "chmod +x $RemoteDir/GekkoLab"

# Step 5: Create systemd service
Write-Host "[5/5] Installing systemd service..." -ForegroundColor Yellow

$ServiceContent = @"
[Unit]
Description=GekkoLab Sensor Monitor
After=network.target

[Service]
Type=exec
WorkingDirectory=$RemoteDir
ExecStart=$RemoteDir/GekkoLab
Restart=always
RestartSec=10
User=$PiUser
Environment=ASPNETCORE_ENVIRONMENT=Development
Environment=ASPNETCORE_URLS=http://*:5050

[Install]
WantedBy=multi-user.target
"@

# Write service file and install
ssh "$PiUser@$PiHost" "echo '$ServiceContent' | sudo tee /etc/systemd/system/gekkolab.service > /dev/null"
ssh "$PiUser@$PiHost" "sudo systemctl daemon-reload && sudo systemctl enable gekkolab && sudo systemctl restart gekkolab"

Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Dashboard URL: http://${PiHost}:5050" -ForegroundColor Cyan
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  Check status:  ssh $PiUser@$PiHost 'sudo systemctl status gekkolab'"
Write-Host "  View logs:     ssh $PiUser@$PiHost 'sudo journalctl -u gekkolab -f'"
Write-Host "  Restart:       ssh $PiUser@$PiHost 'sudo systemctl restart gekkolab'"
Write-Host "  Stop:          ssh $PiUser@$PiHost 'sudo systemctl stop gekkolab'"
Write-Host ""
