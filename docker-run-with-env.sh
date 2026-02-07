#!/bin/bash
# Script để chạy Docker container với environment variables từ .env file
# Sử dụng: ./docker-run-with-env.sh hoặc bash docker-run-with-env.sh

# Load .env file
if [ ! -f .env ]; then
    echo "❌ Error: .env file not found!"
    echo "📝 Please copy .env.example to .env and fill in your actual values"
    exit 1
fi

echo "🐳 Starting Docker container with environment from .env file..."

# Run container with all environment variables from .env
docker run -it \
  --rm \
  --name amkcollective-api \
  --env-file .env \
  -p "${API_PORT:-8080}:80" \
  "${IMAGE:-tinht610/fptu-capstone-amkcollective:latest}"
