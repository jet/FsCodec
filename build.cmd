dotnet pack src/Newtonsoft.Json.Converters.FSharp --configuration Release -o "%CD%\bin"
if ERRORLEVEL 1 (echo Error building Newtonsoft.Json.Converters.FSharp; exit /b 1)