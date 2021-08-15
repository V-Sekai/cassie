set -e
find ./ -name "*.dll" | xargs zstd --compress -q
find ./ -name "*.dll" | xargs rm 
