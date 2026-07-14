@echo off
echo Unregistering FileTag...
regasm .\FileTag.ShellExtension\bin\Debug\net8.0-windows\FileTag.ShellExtension.dll /unregister
taskkill /f /im explorer.exe
start explorer.exe
echo Done.
