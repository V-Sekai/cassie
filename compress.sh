set -e
find ./ -path ./Library/Artifacts -prune -o -name "*.dll" -print | xargs zstd --compress -q
find ./ -path ./Library/Artifacts -prune -o -name "*.dll" -print | xargs rm
