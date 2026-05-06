#!/usr/bin/env bash
# 构建 opc-simulator CLI Docker 镜像（linux-x64, distroless）。
#
# 用法:
#   scripts/build-cli-image.sh                # 构建 bioflux/opc-simulator:latest
#   scripts/build-cli-image.sh v0.1.0         # 构建 :v0.1.0，并补打 :latest
#   IMAGE_NAME=mycorp/sim scripts/build-cli-image.sh   # 覆盖仓库名
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OPCSIM_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$OPCSIM_ROOT"

IMAGE_NAME="${IMAGE_NAME:-bioflux/opc-simulator}"
TAG="${1:-latest}"

echo "==> Building $IMAGE_NAME:$TAG  (linux/amd64, distroless)"
docker build \
    --platform linux/amd64 \
    -f Dockerfile.release \
    -t "$IMAGE_NAME:$TAG" \
    .

if [[ "$TAG" != "latest" ]]; then
    echo "==> Also tagging as $IMAGE_NAME:latest"
    docker tag "$IMAGE_NAME:$TAG" "$IMAGE_NAME:latest"
fi

echo
echo "Built images:"
docker images "$IMAGE_NAME" --format "  {{.Repository}}:{{.Tag}}  ({{.Size}})"
echo
cat <<EOF
Run with default nodesfile (mount your own nodesfile.json into /data):
  docker run --rm \\
      -v \$PWD/src/nodesfile.json:/data/nodesfile.json:ro \\
      -p 50000:50000 \\
      $IMAGE_NAME:$TAG
EOF
