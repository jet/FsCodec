SETLOCAL
SET X=%1
SET BLD=000000%X%
SET BLD=%BLD:~-7%
if [%2]==[] (SET V=CI%BLD%) else (SET V=pr%2-%BLD%)
  
dotnet pack src/Jet.JsonNet.Converters --configuration Release -o "%CD%\bin" --version-suffix %V%
if ERRORLEVEL 1 (echo Error building Jet.JsonNet.Converters; exit /b 1)

dotnet test tests/Jet.JsonNet.Converters.Tests --configuration Release	
if ERRORLEVEL 1 (echo Error testing Jet.JsonNet.Converters; exit /b 1)

ENDLOCAL
