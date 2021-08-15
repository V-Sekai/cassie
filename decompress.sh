set -e
find ./ -name "*.zst" | xargs zstd --uncompress -q
find ./ -name "*.zst" | xargs rm 
# Alternative for windows
# zstd -d -r --rm . -q