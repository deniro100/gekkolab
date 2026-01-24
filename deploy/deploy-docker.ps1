# GekkoLab Docker Deployment Script for Raspberry Pi
# Builds Docker image locally and transfers to Raspberry Pi
# Requires: Docker Desktop running on Windows
# Usage: .\deploy-docker.ps1 -PiHost <hostname-or-ip> -PiUser <username> [-SkipBuild]
# Example: .\deploy-docker.ps1 -PiHost 192.168.0.168 -PiUser admin
# Example: .\deploy-docker.ps1 -PiHost 192.168.0.168 -SkipBuild  # Deploy existing image

param(
    [Parameter(Mandatory=$true)]
    [string]$PiHost,
    
    [Parameter(Mandatory=$false)]
    [string]$PiUser = "admin",

    [Parameter(Mandatory=$false)]
    [string]$Environment = "Production",

    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild
)

$ImageName = "gekkolab:latest"
$ContainerName = "gekkolab"
$ScriptDir = $PSScriptRoot
$ProjectDir = Split-Path $ScriptDir -Parent
$ImageFile = "$env:TEMP\gekkolab-image.tar"
$SshTarget = "${PiUser}@${PiHost}"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "GekkoLab Docker Deployment" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Target: $SshTarget"
Write-Host "Environment: $Environment"
if ($SkipBuild) {
    Write-Host "Mode: Deploy existing image (skip build)" -ForegroundColor Yellow
}
Write-Host ""

# Step 1: Check Docker is running locally
Write-Host "[1/5] Checking local Docker..." -ForegroundColor Yellow
$dockerCheck = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Docker is not running!" -ForegroundColor Red
    Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
    exit 1
}
Write-Host "Docker is running." -ForegroundColor Green

# Step 2: Build Docker image for ARM64 (skip if -SkipBuild)
if ($SkipBuild) {
    Write-Host "[2/5] Skipping build (using existing image)..." -ForegroundColor Yellow
    
    # Verify image exists
    $imageExists = docker images -q $ImageName 2>$null
    if (-not $imageExists) {
        Write-Host "ERROR: Image '$ImageName' not found!" -ForegroundColor Red
        Write-Host "Run without -SkipBuild to build the image first." -ForegroundColor Yellow
        exit 1
    }
    Write-Host "Using existing image: $ImageName" -ForegroundColor Green
} else {
    Write-Host "[2/5] Building Docker image for linux/arm64..." -ForegroundColor Yellow
    Push-Location $ProjectDir

    docker buildx build --platform linux/arm64 -t $ImageName --load .
    $buildResult = $LASTEXITCODE

    Pop-Location

    if ($buildResult -ne 0) {
        Write-Host "Docker build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Image built successfully." -ForegroundColor Green
}

# Step 3: Save image to file
Write-Host "[3/5] Saving image to file..." -ForegroundColor Yellow
if (Test-Path $ImageFile) { Remove-Item -Force $ImageFile }

docker save -o $ImageFile $ImageName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to save Docker image!" -ForegroundColor Red
    exit 1
}

$imageSize = [math]::Round((Get-Item $ImageFile).Length / 1MB, 2)
Write-Host "Image saved: $imageSize MB" -ForegroundColor Green

# Step 4: Transfer image to Raspberry Pi
Write-Host "[4/5] Transferring image to Raspberry Pi..." -ForegroundColor Yellow
Write-Host "  (Enter password when prompted)" -ForegroundColor Gray

scp $ImageFile "${SshTarget}:/tmp/gekkolab-image.tar"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to transfer image!" -ForegroundColor Red
    Remove-Item -Force $ImageFile
    exit 1
}

Remove-Item -Force $ImageFile
Write-Host "Transfer complete." -ForegroundColor Green

# Step 5: Load image and start container on Raspberry Pi
Write-Host "[5/5] Deploying on Raspberry Pi..." -ForegroundColor Yellow
Write-Host "  (Enter password when prompted)" -ForegroundColor Gray

# All remote commands in one SSH call - kill host process, cleanup Docker, then start
ssh $SshTarget "sudo pkill -f GekkoLab 2>/dev/null || true; sudo systemctl stop gekkolab 2>/dev/null || true; sudo docker stop $ContainerName 2>/dev/null || true; sudo docker rm -f $ContainerName 2>/dev/null || true; sudo docker container prune -f 2>/dev/null || true; sleep 2; sudo docker load -i /tmp/gekkolab-image.tar; rm /tmp/gekkolab-image.tar; mkdir -p ~/gekkolab-data; sudo docker run -d --name $ContainerName --restart unless-stopped -p 5050:5050 -e ASPNETCORE_ENVIRONMENT=$Environment -v ~/gekkolab-data:/app/gekkodata $ImageName"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment may have had issues. Checking status..." -ForegroundColor Yellow
}

# Verify container is running
Start-Sleep -Seconds 2
Write-Host "Verifying deployment..." -ForegroundColor Yellow
$containerStatus = ssh $SshTarget "sudo docker ps --filter 'name=$ContainerName' --format '{{.Status}}'"

if ($containerStatus -match "Up") {
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host "Deployment complete!" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Dashboard URL: http://${PiHost}:5050" -ForegroundColor Cyan
} else {
    Write-Host "WARNING: Container may not have started correctly!" -ForegroundColor Red
    Write-Host "Checking logs..." -ForegroundColor Yellow
    ssh $SshTarget "sudo docker logs $ContainerName --tail 20"
}

Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  View logs:     ssh $SshTarget 'sudo docker logs -f $ContainerName'"
Write-Host "  Restart:       ssh $SshTarget 'sudo docker restart $ContainerName'"
Write-Host "  Stop:          ssh $SshTarget 'sudo docker stop $ContainerName'"
Write-Host ""
