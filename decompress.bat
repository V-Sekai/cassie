@echo off
for /r %%f in (*.zst) do (
    echo %%f | findstr /c:"Library\Artifacts" >nul || zstd -d "%%f" --keep -q
)
