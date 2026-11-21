#!/usr/bin/env bash
# Build the My Morning News Kokoro text-to-speech image on the Docker cloud builder for amd64 and arm64, push it to Docker Hub as
# jchristn77/mymorningnews-tts:latest and jchristn77/mymorningnews-tts:<tag>, and pull both back. (c) 2026 Lattice AI LLC.
set -euo pipefail

if [ -z "${1:-}" ]; then
    echo "Usage: build-tts.sh <tag>"
    echo "Example: build-tts.sh v0.1.0"
    exit 1
fi

TAG="$1"
IMAGE="jchristn77/mymorningnews-tts"

cd "$(dirname "${BASH_SOURCE[0]}")"

# Build on the cloud builder for both architectures and push the multi-arch manifest to Docker Hub.
echo "Building ${IMAGE}:latest and ${IMAGE}:${TAG}..."
docker buildx build \
    --builder cloud-jchristn77-jchristn77 \
    --platform linux/amd64,linux/arm64/v8 \
    -t "${IMAGE}:latest" \
    -t "${IMAGE}:${TAG}" \
    -f docker/tts/Dockerfile \
    --push \
    docker/tts

# Pull the pushed tags back from Docker Hub to update the local copy.
echo "Pulling ${IMAGE}:latest..."
docker pull "${IMAGE}:latest"

echo "Pulling ${IMAGE}:${TAG}..."
docker pull "${IMAGE}:${TAG}"

echo "Done."
