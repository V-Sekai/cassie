set -e
find ./ -name "*.zst" | xargs zstd --uncompress --quiet