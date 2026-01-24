#!/bin/bash
# GekkoLab Deployment Script for Raspberry Pi
# Usage: ./deploy.sh <pi-hostname-or-ip> <username>
# Example: ./deploy.sh 192.168.1.100 pi

PI_HOST=${1:-"raspberrypi"}
PI_USER=${2:-"pi"}
APP_NAME="GekkoLab"
REMOTE_DIR="/home/$PI_USER/gekkolab"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../GekkoLab"
PUBLISH_DIR="$PROJECT_DIR/publish"

echo "========================================="
echo "GekkoLab Deployment to Raspberry Pi"
echo "========================================="
echo "Target: $PI_USER@$PI_HOST"
echo "Remote directory: $REMOTE_DIR"
echo ""

# Step 1: Publish the application for ARM64 (Raspberry Pi 4/5) or ARM32 (older Pi)
echo "[1/4] Publishing application for linux-arm64..."
cd "$PROJECT_DIR"
dotnet publish -c Release -r linux-arm64 --self-contained true -o "$PUBLISH_DIR"

# Step 2: Create remote directory
echo "[2/4] Creating remote directory..."
ssh $PI_USER@$PI_HOST "mkdir -p $REMOTE_DIR/gekkodata"

# Step 3: Copy files to Pi
echo "[3/4] Copying files to Raspberry Pi..."
scp -r "$PUBLISH_DIR"/* $PI_USER@$PI_HOST:$REMOTE_DIR/

# Step 4: Set permissions and create service
echo "[4/4] Setting permissions..."
ssh $PI_USER@$PI_HOST "chmod +x $REMOTE_DIR/GekkoLab"

echo ""
echo "========================================="
echo "Deployment complete!"
echo "========================================="
echo ""
echo "To run manually:"
echo "  ssh $PI_USER@$PI_HOST"
echo "  cd $REMOTE_DIR && ./GekkoLab"
echo ""
echo "To install as a service, run:"
echo "  ./install-service.sh $PI_HOST $PI_USER"
echo ""
