# GekkoLab Direct Deployment Script for Raspberry Pi (No Docker)
# Use this for production with real camera access
# Uses self-contained deployment (no .NET runtime required on Pi)
# Usage: .\deploy-direct.ps1 -PiHost <hostname-or-ip> -PiUser <username>
# Example: .\deploy-direct.ps1 -PiHost 192.168.0.168 -PiUser admin

param(
    [Parameter(Mandatory=$true)]
    [string]$PiHost,
    
    [Parameter(Mandatory=$false)]
    [string]$PiUser = "admin",

    [Parameter(Mandatory=$false)]
    [string]$Environment = "Production"
)

$ScriptDir = $PSScriptRoot
$ProjectDir = Split-Path $ScriptDir -Parent
$SshTarget = "${PiUser}@${PiHost}"
$RemoteAppDir = "/home/$PiUser/gekkolab"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "GekkoLab Direct Deployment (No Docker)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Target: $SshTarget"
Write-Host "Environment: $Environment"
Write-Host ""

# Step 1: Build locally for linux-arm64 (self-contained)
Write-Host "[1/4] Building for linux-arm64 (self-contained)..." -ForegroundColor Yellow
Push-Location "$ProjectDir\GekkoLab"

dotnet publish -c Release -r linux-arm64 --self-contained true -o "$ProjectDir\publish-arm64"
$buildResult = $LASTEXITCODE

Pop-Location

if ($buildResult -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful." -ForegroundColor Green

# Step 2: Create archive
Write-Host "[2/4] Creating deployment archive..." -ForegroundColor Yellow
$ArchivePath = "$env:TEMP\gekkolab-deploy.tar.gz"
if (Test-Path $ArchivePath) { Remove-Item -Force $ArchivePath }

Push-Location "$ProjectDir\publish-arm64"
tar -czf $ArchivePath *
Pop-Location

$archiveSize = [math]::Round((Get-Item $ArchivePath).Length / 1MB, 2)
Write-Host "Archive created: $archiveSize MB" -ForegroundColor Green

# Step 3: Transfer to Pi
Write-Host "[3/4] Transferring to Raspberry Pi..." -ForegroundColor Yellow
scp $ArchivePath "${SshTarget}:/tmp/gekkolab-deploy.tar.gz"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Transfer failed!" -ForegroundColor Red
    Remove-Item -Force $ArchivePath
    exit 1
}
Remove-Item -Force $ArchivePath
Write-Host "Transfer complete." -ForegroundColor Green

# Step 4: Deploy on Pi
Write-Host "[4/4] Deploying on Raspberry Pi..." -ForegroundColor Yellow

# Stop existing service, extract, start
ssh $SshTarget "sudo systemctl stop gekkolab 2>/dev/null; mkdir -p $RemoteAppDir; cd $RemoteAppDir; rm -rf ./*; tar -xzf /tmp/gekkolab-deploy.tar.gz; rm /tmp/gekkolab-deploy.tar.gz; mkdir -p gekkodata; chmod +x GekkoLab"

# Create systemd service - run the self-contained executable directly
$serviceContent = @"
[Unit]
Description=GekkoLab Sensor Dashboard
After=network.target

[Service]
Type=simple
User=$PiUser
WorkingDirectory=$RemoteAppDir
ExecStart=$RemoteAppDir/GekkoLab
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=$Environment
Environment=ASPNETCORE_URLS=http://*:5050

[Install]
WantedBy=multi-user.target
"@

# Write service file
$serviceContent | ssh $SshTarget "sudo tee /etc/systemd/system/gekkolab.service > /dev/null"

# Start service
ssh $SshTarget "sudo systemctl daemon-reload; sudo systemctl enable gekkolab; sudo systemctl start gekkolab"

# Verify
Start-Sleep -Seconds 3
$status = ssh $SshTarget "sudo systemctl is-active gekkolab"

if ($status -eq "active") {
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host "Deployment complete!" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Dashboard URL: http://${PiHost}:5050" -ForegroundColor Cyan
} else {
    Write-Host "WARNING: Service may not have started correctly!" -ForegroundColor Red
    Write-Host "Checking logs..." -ForegroundColor Yellow
    ssh $SshTarget "sudo journalctl -u gekkolab --no-pager -n 30"
}

Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  View logs:     ssh $SshTarget 'sudo journalctl -u gekkolab -f'"
Write-Host "  Restart:       ssh $SshTarget 'sudo systemctl restart gekkolab'"
Write-Host "  Stop:          ssh $SshTarget 'sudo systemctl stop gekkolab'"
Write-Host "  Status:        ssh $SshTarget 'sudo systemctl status gekkolab'"
Write-Host ""
