# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.1]

### Fixed
- Coloring a folder (without any text style customization) could enlarge its label font, due to
  an internal "not customized" placeholder value being applied directly instead of falling back
  to the correct default size
- Colored folders' label text was noticeably brighter than Unity's native row text, in both list
  and grid view
- Grid view: colored folders' icon was rendered larger and positioned differently than native
  (uncolored) folder icons, leaving inconsistent spacing to the label below

## [0.2.0]

### Added
- **Project window**: per-folder text color, font style, and font size, set from the same popup
  as the background color, in a collapsible "Text Style" section
- **Main toolbar** (Unity 6.3+): a Time Scale control docked next to Play/Pause/Step, with a
  slider, exact numeric entry, and speed presets

### Changed
- **Project window**: the pin bar now anchors pinned folders to the right edge instead of the
  left; the color picker popup's settings button moved to a fixed top-right position

## [0.1.2]

### Fixed
- Colored empty folders showing the full folder icon instead of the empty one in the Project
  window's list/tree view. Grid view already handled this correctly.

## [0.1.1]

### Fixed
- Stale Sprite import metadata on a handful of icon textures, which produced a spurious
  "rect lies (partially) outside of texture" console warning on import. No behavioral change.

## [0.1.0]

Initial public release.

### Added
- **Project window**: per-folder background color with optional inheritance for subfolders, a
  folder size / item-count stats badge, an empty-folder icon, folder tree lines, a single-column
  coloring mode, a bookmarks navigation bar for pinning frequently used folders, and a shared
  color palette asset with a custom swatch/icon picker
- **Hierarchy window**: per-GameObject row coloring with optional inheritance for children, a
  restored active-state checkbox and scene-visibility icon on every row, divider/header organizer
  rows for large scenes (stripped from player builds automatically), and a pin bar for quickly
  jumping to frequently used GameObjects
- **Scenes**: scene favorites, custom scene ordering, and a quick-switch popup
- An in-editor update checker (**Tools > LenzDev > Editor Customizer > Check for Updates...**)
