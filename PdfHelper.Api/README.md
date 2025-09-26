# PDF Helper (.NET 8 • C# • Minimal API)

Futuristische Web-App zum **Zusammenführen**, **Aufteilen**, **Drehen** und (optional) **Komprimieren** von PDFs – mit modernem UI (Dark/Light-Mode, Drag & Drop, animiertes Logo, Glassmorphism).

## ✨ Features
- **Merge**: mehrere PDFs zu einem
- **Split**: Bereiche wie `1-3,5,end` (1-basiert; `end` = letzte Seite)
- **Rotate**: ausgewählte Seiten um 90°/180°/270°
- **Compress** (optional): via Ghostscript (`gswin64c.exe` im PATH)
- **UI**: Drag&Drop je Funktion, Download-Toast, Theme-Toggle

## 🚀 Quickstart (lokal)
~~~bash
dotnet run
# Konsole: Now listening on: http://localhost:<port>
# Browser: http://localhost:<port>   (ggf. /index.html)
~~~

> Optional: In `Program.cs` vor `app.UseStaticFiles()` `app.UseDefaultFiles();` ergänzen, damit `/` automatisch `index.html` lädt.

## 🔌 REST-API
- **POST** `/api/merge`  
  FormData: `files[]` (≥ 2 PDFs) → Response: `merged.pdf`
- **POST** `/api/split?ranges=1-3,5,end`  
  FormData: `file` → Response: `parts.zip`  
  Bereichssyntax (1-basiert): einzelne Seiten `2,7`, Bereiche `1-3`, gemischt `1-3,5,8-end`, Schlüsselwort `end` = letzte Seite
- **POST** `/api/rotate?pages=2,4-5&angle=90`  
  FormData: `file` → Response: `rotated.pdf`  (angle: 90/180/270)
- **POST** `/api/compress?level=standard|low|high`  
  FormData: `file` → Response: `compressed.pdf`  (Ghostscript erforderlich; Mapping: `standard→/ebook`, `low→/screen`, `high→/prepress`)

## 🗂️ Projektstruktur
~~~
PdfHelper.Api/
  Program.cs
  PdfHelper.Api.csproj
  Services/
    IPdfService.cs
    PdfService.cs
  Background/
    CleanupService.cs
  wwwroot/
    index.html
    style.css
~~~

## 🧪 Hinweise
- Uploads: nur `.pdf` (sonst HTTP 400).
- Split: ungültige Bereiche → HTTP 400 mit Fehlermeldung.
- Rotate: nur 90er-Vielfache erlaubt (90/180/270) → ansonsten HTTP 400.
- Compress ohne Ghostscript → HTTP 400 (`NO_COMPRESSOR`); übrige Funktionen bleiben nutzbar.

## 🛠️ Build / Publish
~~~bash
# Release-Build + Publish (Framework-abhängig)
dotnet publish -c Release -o publish
~~~

~~~bash
# Optional: self-contained EXE (ohne .NET-Installation)
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish
~~~
Hinweis: Statische Dateien (`wwwroot`) müssen beim Start neben der App verfügbar sein.

## 📦 Voraussetzungen
- .NET SDK **8.x**
- (optional) Ghostscript für Compress (Windows: `gswin64c.exe` im PATH)


