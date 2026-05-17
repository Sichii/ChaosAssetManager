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

**Animation** (`Model/Animation.cs`): Holds a collection of `SKImage` frames with frame interval for animated previews. Has an optional `BlendMode` property (`SKBlendMode?`) set for EFA animations that require additive blending at draw time.

### Editor Pattern

Editors follow a two-control pattern:
1. **List Control** (e.g., `NPCEditorControl`): Shows a list of entries from an archive, handles loading/saving
2. **Content Control** (e.g., `NPCContentEditorControl`): Displays and edits the selected entry

Editors load data from `ArchiveCache`, modify in-memory objects, and save back via `archive.Patch()` followed by `archive.Save()`.

### Isometric Grid Rendering

`RenderUtil.DrawIsometricGrid()` renders an infinite isometric tile grid using a repeating shader pattern. Call `RenderUtil.Preload()` at startup to pre-create the shader.

## DALib Transparency Rendering Rules

These rules apply whenever an `EpfFrame` or `EfaFrame` is rendered. Getting them wrong produces incorrect transparency.

### EPF Frames: palette >= 1000 requires `SKAlphaType.Unpremul`

Palette IDs >= 1000 are luminance-blended palettes. `PaletteLookup.GetPaletteForId` automatically applies `WithLutAlpha()` to all palette colors (brightness-to-alpha mapping), but the resulting bitmap must be created with `SKAlphaType.Unpremul` so SkiaSharp does not re-premultiply the per-color alpha values.

**Always use the extension method instead of `GetPaletteForId` directly:**

```csharp
// Helpers/PaletteLookupExtensions.cs
var (palette, alphaType) = lookup.GetPaletteAndAlphaType(id, overrideType);
var image = Graphics.RenderImage(epfFrame, palette, alphaType);
```

Using the default (`SKAlphaType.Premul`) for a palette >= 1000 will double-premultiply the alpha, producing wrong transparency.

### EFA Frames: blend mode determined by `EfaBlendingType`

EFA files carry a `BlendingType` property. The correct SkiaSharp blend mode depends on the value:

| EfaBlendingType | SKBlendMode | Reason |
|---|---|---|
| `Additive` | `SKBlendMode.Plus` | Full alpha written by renderer; Plus makes dark pixels add nothing |
| `SelfAlpha` | `SKBlendMode.Plus` | Same as Additive |
| `SeparateAlpha` | `SKBlendMode.SrcOver` | Per-pixel alpha decoded from EFA alpha surface |
| `PerChannelAlpha` | `SKBlendMode.SrcOver` | Per-pixel alpha decoded from EFA alpha surface |

**Always use the extension method:**

```csharp
// Helpers/EfaBlendingExtensions.cs
var blendMode = efaFile.BlendingType.ToSKBlendMode();
// store on Animation.BlendMode; apply at draw time:
paint.BlendMode = animation.BlendMode ?? SKBlendMode.SrcOver;
```

**Hardcoded `SKBlendMode.SrcATop` is wrong** for Additive/SelfAlpha effects ΓÇö use `ToSKBlendMode()` via the extension.

### EPF effect centerpoints: default to `SKPoint(28, 70)`

When a `.tbl` centerpoint file is missing from the archive, or contains fewer entries than the EPF frame count, pad with `SKPoint(28, 70)` ΓÇö not `SKPoint(0, 0)`. The default is set in `EffectEditorControl.LoadCenterPoints`.

## Equipment Archive Architecture

### Letter-to-Archive Routing

Equipment EPF files are stored in gender-specific `khan*.dat` archives. The equipment type letter determines which archive pair is used:

| Letter range | Male archive | Female archive |
|---|---|---|
| a ΓÇô d | `khanmad.dat` | `khanwad.dat` |
| e ΓÇô h | `khanmeh.dat` | `khanweh.dat` |
| i ΓÇô m | `khanmim.dat` | `khanwim.dat` |
| n ΓÇô s | `khanmns.dat` | `khanwns.dat` |
| t ΓÇô z | `khanmtz.dat` | `khanwtz.dat` |

Entry names follow the pattern `{genderLetter}{equipmentLetter}{id:D3}{animationSuffix}.epf` (e.g., `mb001.epf` = male body armor, entry 1, base animation).

Palette data lives in `khanpal.dat` with entries named `pal{letter}{id:D3}.pal` and a lookup table `pal{letter}.tbl`.

### Shield Special Case

Shields (equipment letter `'s'`) are **always** loaded from and saved to `khanmns.dat` (the male archive), regardless of the gender radio button selection. The selection logic is:

```csharp
bool useMaleArchive = (typeLetter == 's') || (MaleRadio.IsChecked == true);
```

### Behind-Body Equipment

`RenderBehindBodyTypes` in `EpfEquipmentEditorControl.xaml.cs` is a `HashSet` containing `'f'` (head layer 2) and `'g'` (accessories layer 2). These equipment types render behind the character body rather than in front.

### Associated Letter Groups (ID Uniqueness)

Equipment types that share a render layer must not share the same numeric ID, or they render on top of each other. The groups are enforced by `GetAllAssociatedEntries`:

- `c` and `g` share IDs
- `e`, `f`, and `h` share IDs
- `i` and `u` share IDs
- `p` and `w` share IDs

`GetAllAssociatedEntries` calls `GetArchiveForLetter` per letter (not a single archive) so it can retrieve entries from different archives.

### Animation Suffixes

Animation variants appended to the base entry ID:

| Suffix | Animation |
|---|---|
| *(none)* | Idle / base |
| `01` | Walk |
| `02` | Assail |
| `03` | Emotes (hands up, blow kiss, wave ΓÇö frames 0-9) |
| `04` | Idle animated |
| `b` | Priest / Bard |
| `c` | Warrior (5 attack variants) |
| `d` | Monk |
| `e` | Rogue |
| `f` | Wizard / Summoner |

## Code Style

### Inline Comments
- lowercase
- no space after `//`
- example: `//this is a comment`

### XML Doc Summaries
- Keep `/// <summary>` blocks on methods
- These are separate from inline comments
