# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run --project ChaosAssetManager/ChaosAssetManager.csproj
```

## Project Overview

ChaosAssetManager is a WPF GUI application for viewing and editing Dark Ages game assets. It's built on top of [DALib](https://github.com/eriscorp/dalib), a library for working with Dark Ages file formats.

### Key Features
- Archive viewer/editor for .dat files (viewing, patching, compiling, extracting)
- Format conversion between image types and DA formats (.efa, .epf, .hpf, .mpf, .spf)
- Effect Editor for .efa/.epf files
- Equipment Editor for .epf files
- NPC Editor for .mpf files
- Map Editor for .map files
- MetaFile Editor for metadata files

## Architecture

### External Dependencies
- **DALib** (local project reference at `../../dalib/DALib/DALib.csproj`): Core library for DA file format parsing and rendering
- **SkiaSharp**: GPU-accelerated 2D rendering via `SKGLElement`
- **MaterialDesign**: WPF UI framework

### Core Helpers

**ArchiveCache** (`Helpers/ArchiveCache.cs`): Singleton cache for loaded .dat archives. Access archives via static properties like `ArchiveCache.Hades`, `ArchiveCache.Legend`, etc.

**PathHelper** (`Helpers/PathHelper.cs`): Persisted settings for file paths. The `ArchivesPath` property points to the DA installation's data directory containing .dat files.

**RenderUtil** (`Helpers/RenderUtil*.cs`): Split across multiple partial class files by archive type. Handles rendering DA file formats to SkiaSharp images, including palette lookups and format-specific rendering logic.

### Preview System

**SKGLElementPlus** (`Controls/PreviewControls/SKGLElementPlus.xaml.cs`): Wrapper around SkiaSharp's `SKGLElement` that adds pan/zoom functionality via matrix transforms. Used by all editors for sprite/frame preview.

**Animation** (`Model/Animation.cs`): Holds a collection of `SKImage` frames with frame interval for animated previews.

### Editor Pattern

Editors follow a two-control pattern:
1. **List Control** (e.g., `NPCEditorControl`): Shows a list of entries from an archive, handles loading/saving
2. **Content Control** (e.g., `NPCContentEditorControl`): Displays and edits the selected entry

Editors load data from `ArchiveCache`, modify in-memory objects, and save back via `archive.Patch()` followed by `archive.Save()`.

### Isometric Grid Rendering

`RenderUtil.DrawIsometricGrid()` renders an infinite isometric tile grid using a repeating shader pattern. Call `RenderUtil.Preload()` at startup to pre-create the shader.

## Code Style

### Inline Comments
- lowercase
- no space after `//`
- example: `//this is a comment`

### XML Doc Summaries
- Keep `/// <summary>` blocks on methods
- These are separate from inline comments
