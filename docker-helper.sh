#!/bin/bash

# FPTU Capstone AMKCollective - Docker Helper Script
# Usage: ./docker-helper.sh [command]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
DOCKER_USERNAME="${DOCKERHUB_USERNAME:-your-dockerhub-username}"
IMAGE_NAME="fptu-capstone-amkcollective"
ENV_FILE=".env"

# Functions
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

check_env_file() {
    if [ ! -f "$ENV_FILE" ]; then
        print_error ".env file not found!"
        print_info "Creating from .env.example..."
        cp .env.example .env
        print_info "Please edit .env file with your configuration"
        exit 1
    fi
}

# Command handlers
cmd_setup() {
    print_info "Setting up environment..."
    
    if [ ! -f ".env" ]; then
        cp .env.example .env
        print_success ".env file created from .env.example"
        print_info "Please edit .env with your actual values"
    else
        print_info ".env already exists"
    fi
    
    print_info "Checking Docker installation..."
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed!"
        exit 1
    fi
    
    if ! command -v docker-compose &> /dev/null; then
        print_error "Docker Compose is not installed!"
        exit 1
    fi
    
    print_success "Docker setup complete!"
}

cmd_pull() {
    check_env_file
    print_info "Pulling latest image from Docker Hub..."
    docker-compose pull
    print_success "Image pulled successfully!"
}

cmd_start() {
    check_env_file
    print_info "Starting containers..."
    docker-compose up -d
    print_success "Containers started!"
    print_info "API running at http://localhost:8080"
    print_info "Run './docker-helper.sh logs' to view logs"
}

cmd_stop() {
    print_info "Stopping containers..."
    docker-compose down
    print_success "Containers stopped!"
}

cmd_restart() {
    print_info "Restarting containers..."
    docker-compose restart
    print_success "Containers restarted!"
}

cmd_logs() {
    docker-compose logs -f --tail=100 api
}

cmd_build() {
    print_info "Building Docker image locally..."
    docker-compose build --no-cache
    print_success "Build complete!"
}

cmd_clean() {
    print_info "Cleaning up Docker resources..."
    docker-compose down -v --rmi local
    print_success "Cleanup complete!"
}

cmd_status() {
    print_info "Container status:"
    docker-compose ps
    echo ""
    print_info "Resource usage:"
    docker stats --no-stream
}

cmd_health() {
    print_info "Checking health status..."
    if curl -f http://localhost:8080/health 2>/dev/null; then
        print_success "API is healthy!"
    else
        print_error "API health check failed!"
        exit 1
    fi
}

cmd_shell() {
    print_info "Opening shell in API container..."
    docker-compose exec api /bin/bash
}

cmd_update() {
    print_info "Updating to latest version..."
    cmd_pull
    cmd_stop
    cmd_start
    print_success "Update complete!"
}

cmd_help() {
    cat << EOF
FPTU Capstone AMKCollective - Docker Helper

Usage: ./docker-helper.sh [command]

Commands:
  setup       Setup environment (.env file)
  pull        Pull latest image from Docker Hub
  start       Start containers
  stop        Stop containers
  restart     Restart containers
  logs        View container logs (follow mode)
  build       Build Docker image locally
  clean       Clean up containers, volumes, and images
  status      Show container status and resource usage
  health      Check API health
  shell       Open shell in API container
  update      Pull latest image and restart
  help        Show this help message

Examples:
  ./docker-helper.sh setup
  ./docker-helper.sh start
  ./docker-helper.sh logs

EOF
}

# Main script logic
case "${1:-help}" in
    setup)   cmd_setup ;;
    pull)    cmd_pull ;;
    start)   cmd_start ;;
    stop)    cmd_stop ;;
    restart) cmd_restart ;;
    logs)    cmd_logs ;;
    build)   cmd_build ;;
    clean)   cmd_clean ;;
    status)  cmd_status ;;
    health)  cmd_health ;;
    shell)   cmd_shell ;;
    update)  cmd_update ;;
    help)    cmd_help ;;
    *)
        print_error "Unknown command: $1"
        cmd_help
        exit 1
        ;;
esac
