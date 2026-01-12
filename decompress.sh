set -e
find ./ -path ./Library/Artifacts -prune -o -name "*.zst" -print | xargs zstd --uncompress --quiet
