dotnet pack src/Jet.JsonNet.Converters --configuration Release -o "%CD%\bin" --version-suffix CI%1
if ERRORLEVEL 1 (echo Error building Jet.JsonNet.Converters; exit /b 1)

dotnet test tests/Jet.JsonNet.Converters.Tests --configuration Release	
if ERRORLEVEL 1 (echo Error testing Jet.JsonNet.Converters; exit /b 1)