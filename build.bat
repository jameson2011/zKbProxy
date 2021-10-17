@echo off
cls

dotnet tool restore


SET TARGET="All"

IF NOT [%1]==[] (set TARGET="%1")

dotnet fake run "build.fsx" -t %TARGET%

