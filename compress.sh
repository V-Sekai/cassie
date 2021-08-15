set -e
find ./ -name "*.dll" | xargs zstd --compress
find ./ -name "*.dll" | xargs rm 
