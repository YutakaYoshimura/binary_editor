# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

This is a pre-development research and planning repository for a custom binary editor targeting EDS (Electronic Data Sheet) file editing. The repository currently contains:

- `docs/` — Japanese documentation covering requirements, feature analysis, and NuGet package comparisons
- `sample/HexEditorComparison/` — C# proof-of-concept comparing hex editor NuGet packages
- `Develop/` — The intended working directory for the actual product implementation (currently empty)

## Sample Project (HexEditorComparison)

**Environment**: Visual Studio 2019+, .NET Framework 4.8, Windows

**Build & Run**:
1. Open `sample/HexEditorComparison/HexEditorComparison.sln` in Visual Studio
2. Restore NuGet packages: menu → Build → "Restore NuGet Packages for Solution"
3. Right-click the target project → "Set as Startup Project"
4. Press **F5**

**Projects in the solution**:
| Project | NuGet Package | Notes |
|---------|--------------|-------|
| `01_BeHexBox` | Be.Windows.Forms.HexBox 1.6.1 | WinForms native, no longer maintained |
| `02_WPFHexaEditor` | WPFHexaEditor 2.1.7 | Recommended; WPF via ElementHost |
| `03_HexEditorWpf` | HexEditor.Wpf 2.1.8 | Same codebase as 02, different package name |
| `04_HexViewWpf` | HexView.Wpf 0.1.0 | View-only, no editing |
| `05_SpooksoftHexEditor` | Spooksoft.HexEditor 1.0.3 | Minimal docs; verify API names against installed package |
| `08_ByteViewer` | (none — .NET standard) | View-only; requires `System.Design` reference |

WPF packages (02–05) are embedded in WinForms via `ElementHost`. Target framework must be `net48`.

## Architecture & Design Context

### Primary Use Case
The tool is intended to edit **EDS files** whose structure (offsets and parameter positions) is known to the operator in advance. Two editing modes are defined:

- **Full view mode** — display the entire file with offset/header, hex+ASCII, search, and endian-aware value editing
- **Parameter view mode** — display only specific extracted parameters defined via a structure template; no offset header or search needed

### Key Technical Requirements
- **Little-endian value interpretation**: read/write N bytes at an offset, interpret as little-endian integer, allow direct decimal/hex editing, then write back as little-endian bytes
- **Structure template**: pre-define which offsets/parameters to extract and display
- **Undo/Redo and change highlighting**: highlight modified bytes vs. original; support undo
- **Backup on save**: auto-create a backup before writing changes to the EDS file

### Recommended Base Package
**WPFHexaEditor** (② in the sample) is the recommended starting point — it has active maintenance, MIT license with explicit commercial use permission, WinForms compatibility via ElementHost, and built-in Undo/Redo and hex/decimal display switching.

### Feature Priority Matrix (from `docs/バイナリエディタ機能整理.md`)
| Feature | Full view | Parameter view |
|---------|-----------|---------------|
| Read-only mode | Required | Required |
| Offset/header display | Required | Not needed |
| Search | Required | Not needed |
| Hex/Dec switching | Required | Required |
| Little-endian value read/write | Required | Required |
| Structure template | Optional | Required |
| Byte insert/delete | Confirm with user | Not needed |
| ASCII display | Confirm with user | Not needed |
| Undo/Redo | Proposed | Proposed |
| Change highlighting | Proposed | Proposed |
| Auto backup | Proposed | Proposed |
