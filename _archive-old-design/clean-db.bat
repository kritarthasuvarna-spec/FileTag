@echo off
echo WARNING: This will delete all your FileTag notes.
pause
del /f "%APPDATA%\FileTag\notes.db"
echo Database deleted.
