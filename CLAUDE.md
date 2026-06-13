# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

`json2cs.dev` is a standalone **Blazor WebAssembly** (.NET 9) tool that generates C# classes from JSON, entirely client-side. There is **no backend** — it is designed to be served as static files (GitHub Pages, domain `json2cs.dev`). The app is already live in production.

## Commands

```powershell
dotnet restore
dotnet build
dotnet run --project Json2Cs.Client      # dev server at http://localhost:5284
dotnet publish Json2Cs.Client -c Release  # static output for deployment
```

There is **no test project** and **no linter configured**. The `tests/` folder (`Json2Cs.Client/tests/*.json`) contains sample JSON payloads used for manual verification in the browser, not an automated test suite.

`NuGet.Config` pins the package source to nuget.org only (clears inherited sources) — keep it when adding packages.

## Architecture

The application is effectively a **single page**. The UI lives in [Index.razor](Json2Cs.Client/Pages/Index.razor); the generation logic was extracted into a stateless **`Json2Cs.Client.Generation`** namespace ([Json2Cs.Client/Generation/](Json2Cs.Client/Generation/)). The component holds only UI state, cookie persistence, and Monaco interop, and delegates to the generator service.

### Generation layer ([Generation/](Json2Cs.Client/Generation/))

- **`CSharpClassGenerator`** (DI singleton, injected into `Index.razor`) — entry point `GenerationResult Generate(string json, GeneratorOptions options)`. Parses with `JsonDocument` and walks it via `DetermineType` → `BuildObjectType` / `BuildArrayType` / `MapPrimitiveType`, accumulating a `List<ClassDefinition>`, then renders to source (`RenderCode`: usings, optional `#nullable enable`, optional namespace wrapping). Throws on invalid JSON; the component catches and shows the message.
- **`SmartModeTransformer`** (static) — `Apply(definitions, options)` runs in Smart mode (default on), a 5-phase post-processing pass: ① deduplicate structurally-identical classes (by signature) ② merge classes with similar base names + ≥50% shared properties ③ re-point `List<object>` array element types to the merged class ④ remove orphaned (unreferenced, non-`Root`) classes ⑤ clean up now-inaccurate comments. **Strict mode skips this entirely** and emits types exactly as discovered.
- **`NameUtilities`** (static) — shared pure helpers: `ToPascalCase`, `ToSingular`, `IsReferenceType`, reserved-word handling, numeric-suffix stripping.
- **`GeneratorOptions`** (input DTO mapped from the settings panel via `Index.BuildOptions()`), **`ClassDefinition`/`PropertyDefinition`** (intermediate model), **`GenerationResult`** (rendered code + class/property/line counts for the status line).

### Type inference conventions

- ISO date strings → `DateOnly` (date-only) or `DateTimeOffset` (with time); detected by regex in `IsIsoDate` / `IsIsoDateTime`.
- Numbers widen `int` → `long` → `double`; mixed object+primitive arrays become `List<object>` flagged internally with a `__MIXED__` sentinel that is stripped before output.
- Empty objects → `Dictionary<string, object>`; empty arrays → `object` with a comment.
- Property names are PascalCased; reserved words get an `@` prefix, leading digits get `_`. A `[JsonPropertyName]`/`[JsonProperty]` attribute is emitted whenever the JSON key differs from the C# name (or contains `_`).
- Generated comments are in **German** (e.g. "Typ konnte nicht ermittelt werden"); the UI and user-facing strings are a mix of German and English.

### Frontend / JS interop

- The two editors are **Monaco**, loaded from a CDN at runtime via an inline `eval` in `InitializeMonacoEditors` (with a hard `Task.Delay(1000)` wait). JS interop functions live in [clipboard.js](Json2Cs.Client/wwwroot/js/clipboard.js): `initializeMonacoEditor`, `updateMonacoEditor`, `copyToClipboard`, `updateStatusDisplay`, `setCookie`, `getCookie`. The JSON editor change → `OnJsonChanged` [JSInvokable] is debounced 300ms before regenerating.
- User settings (declaration type, attribute mode, nullable, list vs array, namespace, etc.) persist to a `json2cs_settings` cookie (`SaveSettingsToCookie`), restored in `OnAfterRenderAsync`.

### Analytics (currently NOT active)

Analytics is **half-wired and effectively dead code** — relevant context if asked to add/fix it:
- [utils.js](Json2Cs.Client/wwwroot/js/utils.js) defines `initGoogleAnalytics(measurementId)`, but `utils.js` is **never loaded** by [index.html](Json2Cs.Client/wwwroot/index.html) (only `clipboard.js` is) and `initGoogleAnalytics` is **never called** from C#.
- [wwwroot/appsettings.json](Json2Cs.Client/wwwroot/appsettings.json) holds a real `Analytics:GoogleAnalyticsId`, but nothing reads it (`Program.cs` does not load config or call the GA init). This file is **gitignored** (`**/wwwroot/appsettings.json`) because it contains the real ID — do not commit it.

## Notes

- Leftover Blazor template scaffolding still exists and is unused by the app: `Pages/Counter.razor`, `Pages/Weather.razor`, `wwwroot/sample-data/weather.json`. Real pages are `Index`, `Impressum`, `Datenschutz`, `Disclaimer` (linked from `Layout/NavMenu.razor`).
- `bin/`, `obj/`, and `publish/` are committed-adjacent build artifacts visible in the tree but gitignored — ignore them when searching; source lives under `Json2Cs.Client/` (`*.razor`, `*.cs`, `wwwroot/`).
