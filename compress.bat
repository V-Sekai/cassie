@echo off
for /r %%f in (*.dll) do (
    echo %%f | findstr /c:"Library\Artifacts" >nul || (
        zstd --compress -q "%%f"
        if %errorlevel% equ 0 del "%%f"
    )
)
