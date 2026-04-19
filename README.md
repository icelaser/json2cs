# json2cs

`json2cs.dev` ist ein clientseitiges Blazor WebAssembly-Tool zum Erstellen von C#-Klassen aus JSON.

## Projektstruktur

- `json2cs.sln` — .NET-Lösung
- `Json2Cs.Client/Json2Cs.Client.csproj` — standalone Blazor WebAssembly-App
- `Json2Cs.Client/Pages/Index.razor` — JSON-zu-C#-Generator-Benutzeroberfläche
- `Json2Cs.Client/wwwroot/css/app.css` — App-Styling
- `Json2Cs.Client/wwwroot/js/clipboard.js` — Kopieren-in-Zwischenablage-Integration
- `NuGet.Config` — lokale Paketquelle für zuverlässige Wiederherstellung

## Lokale Entwicklung

```powershell
dotnet restore
dotnet build
dotnet run --project Json2Cs.Client
```

Öffne dann im Browser die angezeigte URL.

## Ziel

- Standalone Blazor WebAssembly (.NET 9)
- Kein Backend
- GitHub Pages-fähig
- Domain: `json2cs.dev`

## Funktionen v1

- JSON-Eingabe per Textarea
- Optionen: `class` / `record`, `System.Text.Json` / `Newtonsoft.Json`, Nullable Reference Types, `List<T>` / Array
- Automatische C#-Generierung für verschachtelte Objekte
- `snake_case`-Keys erhalten automatische JSON-Attribut-Zuordnung
- Ausgabebereich mit Code-Styling und Kopieren-Button
