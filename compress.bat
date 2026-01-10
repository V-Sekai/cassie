@echo off
for /r %%f in (*.dll) do (
    zstd --compress -q "%%f"
    if %errorlevel% equ 0 del "%%f"
)