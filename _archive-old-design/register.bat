@echo off
echo Registering FileTag shell extension...
regasm .\FileTag.ShellExtension\bin\Debug\net8.0-windows\FileTag.ShellExtension.dll /codebase
echo Restarting Explorer...
taskkill /f /im explorer.exe
start explorer.exe
echo Done.
