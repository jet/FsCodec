dotnet pack src/Newtonsoft.Json.Converters.FSharp --configuration Release -o "%CD%\bin" --version-suffix CI%1
if ERRORLEVEL 1 (echo Error building Newtonsoft.Json.Converters.FSharp; exit /b 1)

dotnet test tests/Newtonsoft.Json.Converters.FSharp.Tests --configuration Release	
if ERRORLEVEL 1 (echo Error testing Newtonsoft.Json.Converters.FSharp; exit /b 1)