set -e
find ./ -name "*.zst" | xargs zstd --uncompress
find ./ -name "*.zst" | xargs rm 
# Alternative for windows
# zstd -d -r --rm ./sdk || true