set -e
find ./ -name "*.zst" | xargs zstd --uncompress
find ./ -name "*.zst" | xargs rm 