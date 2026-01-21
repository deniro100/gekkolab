# GekkoLab Docker Deployment Script for Raspberry Pi
# Usage: .\deploy-docker.ps1 -PiHost <hostname-or-ip> -PiUser <username>
# Example: .\deploy-docker.ps1 -PiHost gekkopi.local -PiUser admin

param(
    [Parameter(Mandatory=$true)]
    [string]$PiHost,
    
    [Parameter(Mandatory=$false)]
    [string]$PiUser = "admin",

    [Parameter(Mandatory=$false)]
    [string]$Environment = "Development"
)

$AppName = "gekkolab"
$ImageName = "gekkolab:latest"
$ContainerName = "gekkolab"
$ScriptDir = $PSScriptRoot
$ProjectDir = Split-Path $ScriptDir -Parent

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "GekkoLab Docker Deployment" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Target: $PiUser@$PiHost"
Write-Host "Environment: $Environment"
Write-Host ""

# Step 1: Check Docker version on Pi
Write-Host "[1/6] Checking Docker version on Raspberry Pi..." -ForegroundColor Yellow
$dockerVersion = ssh "$PiUser@$PiHost" "docker --version 2>/dev/null || echo 'NOT_INSTALLED'"

if ($dockerVersion -match "NOT_INSTALLED" -or $dockerVersion -match "Docker version 1\." -or $dockerVersion -match "Docker version 2[0-3]\.") {
    Write-Host ""
    Write-Host "Docker needs to be installed or upgraded on your Raspberry Pi!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run these commands on your Raspberry Pi to install/upgrade Docker:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  # Remove old Docker (if installed)" -ForegroundColor Gray
    Write-Host "  sudo apt-get remove docker docker-engine docker.io containerd runc" -ForegroundColor White
    Write-Host ""
    Write-Host "  # Install Docker using the convenience script" -ForegroundColor Gray
    Write-Host "  curl -fsSL https://get.docker.com -o get-docker.sh" -ForegroundColor White
    Write-Host "  sudo sh get-docker.sh" -ForegroundColor White
    Write-Host ""
    Write-Host "  # Add your user to docker group (so you don't need sudo)" -ForegroundColor Gray
    Write-Host "  sudo usermod -aG docker $PiUser" -ForegroundColor White
    Write-Host ""
    Write-Host "  # Reboot to apply changes" -ForegroundColor Gray
    Write-Host "  sudo reboot" -ForegroundColor White
    Write-Host ""
    Write-Host "After upgrading, run this script again." -ForegroundColor Yellow
    exit 1
}

Write-Host "Docker version: $dockerVersion" -ForegroundColor Green

# Step 2: Build Docker image locally for ARM64
Write-Host "[2/6] Building Docker image for linux/arm64..." -ForegroundColor Yellow
Push-Location $ProjectDir

# Build for ARM64 architecture (Raspberry Pi)
docker buildx build --platform linux/arm64 -t $ImageName --load .
$buildResult = $LASTEXITCODE

Pop-Location

if ($buildResult -ne 0) {
    Write-Host "Docker build failed!" -ForegroundColor Red
    exit 1
}

# Step 3: Save and transfer the image
Write-Host "[3/6] Saving Docker image..." -ForegroundColor Yellow
$TempImageFile = "$env:TEMP\gekkolab-image.tar"
docker save -o $TempImageFile $ImageName

Write-Host "[4/6] Transferring image to Raspberry Pi (this may take a few minutes)..." -ForegroundColor Yellow
scp $TempImageFile "${PiUser}@${PiHost}:/tmp/gekkolab-image.tar"

# Clean up local temp file
Remove-Item $TempImageFile -Force

# Step 5: Load image and run container on Pi
Write-Host "[5/6] Loading image on Raspberry Pi..." -ForegroundColor Yellow
ssh "$PiUser@$PiHost" "docker load -i /tmp/gekkolab-image.tar && rm /tmp/gekkolab-image.tar"

# Step 6: Stop old container and start new one
Write-Host "[6/6] Starting container..." -ForegroundColor Yellow

# Create data directory on Pi
ssh "$PiUser@$PiHost" "mkdir -p ~/gekkolab-data"

# Stop and remove existing container if any
ssh "$PiUser@$PiHost" "docker stop $ContainerName 2>/dev/null; docker rm $ContainerName 2>/dev/null"

# Run new container
ssh "$PiUser@$PiHost" "docker run -d --name $ContainerName --restart unless-stopped -p 5050:5050 -e ASPNETCORE_ENVIRONMENT=$Environment -v ~/gekkolab-data:/app/gekkodata $ImageName"

Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Docker Deployment complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Dashboard URL: http://${PiHost}:5050" -ForegroundColor Cyan
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  Check status:  ssh $PiUser@$PiHost 'docker ps'"
Write-Host "  View logs:     ssh $PiUser@$PiHost 'docker logs -f $ContainerName'"
Write-Host "  Restart:       ssh $PiUser@$PiHost 'docker restart $ContainerName'"
Write-Host "  Stop:          ssh $PiUser@$PiHost 'docker stop $ContainerName'"
Write-Host ""
