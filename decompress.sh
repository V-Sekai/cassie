set -e
find ./ -name "*.zst" | xargs zstd --uncompress --quiet
# Alternative for windows
# zstd -d -r --keep . -q
