#!/bin/bash
# Install GekkoLab as a systemd service on Raspberry Pi
# Usage: ./install-service.sh <pi-hostname-or-ip> <username>

PI_HOST=${1:-"raspberrypi"}
PI_USER=${2:-"pi"}
REMOTE_DIR="/home/$PI_USER/gekkolab"

echo "Installing GekkoLab as a systemd service..."

# Create service file
ssh $PI_USER@$PI_HOST "cat > /tmp/gekkolab.service << 'EOF'
[Unit]
Description=GekkoLab Sensor Monitor
After=network.target

[Service]
Type=exec
WorkingDirectory=/home/$PI_USER/gekkolab
ExecStart=/home/$PI_USER/gekkolab/GekkoLab
Restart=always
RestartSec=10
User=$PI_USER
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://*:5050

[Install]
WantedBy=multi-user.target
EOF"

# Install and enable service
ssh $PI_USER@$PI_HOST "sudo mv /tmp/gekkolab.service /etc/systemd/system/ && \
    sudo systemctl daemon-reload && \
    sudo systemctl enable gekkolab && \
    sudo systemctl start gekkolab"

echo ""
echo "Service installed and started!"
echo ""
echo "Useful commands:"
echo "  Check status:  ssh $PI_USER@$PI_HOST 'sudo systemctl status gekkolab'"
echo "  View logs:     ssh $PI_USER@$PI_HOST 'sudo journalctl -u gekkolab -f'"
echo "  Restart:       ssh $PI_USER@$PI_HOST 'sudo systemctl restart gekkolab'"
echo "  Stop:          ssh $PI_USER@$PI_HOST 'sudo systemctl stop gekkolab'"
echo ""
echo "Access the dashboard at: http://$PI_HOST:5050"
