# Unity Editor Enhancer

Quality-of-life extensions for the Unity Project and Hierarchy windows: folder/GameObject coloring, folder stats, bookmarks, scene favorites, and more.

## Features

**Project window**
- Per-folder background color, with optional color inheritance for subfolders
- Folder size / item-count stats badge
- Empty-folder icon, tree lines, single-column coloring mode
- Bookmarks navigation bar (pin frequently used folders above the browser)
- Shared color palette asset with a custom swatch/icon picker

**Hierarchy window**
- Per-GameObject row coloring, with optional color inheritance for children
- Restored active-state checkbox and scene-visibility ("eye") icon on every row
- Divider and header rows for organizing large scenes (stripped from player builds automatically)
- Pin bar for quickly jumping to frequently used GameObjects

**Main toolbar** (Unity 6.3+)
- Time Scale control docked next to Play/Pause/Step, with a slider, exact numeric entry, and
  speed presets

**Scenes**
- Scene favorites, custom scene ordering, and a quick-switch popup

## Installation

In Unity, open **Window > Package Manager > + > Add package from git URL** and enter:

```
https://github.com/KenanKndl/unity-editor-enhancer.git
```

To pin a specific version, append the release tag:

```
https://github.com/KenanKndl/unity-editor-enhancer.git#v0.3.0
```

## Updating

Use **Tools > LenzDev > Editor Enhancer > Check for Updates...** inside Unity to check for and install newer releases without leaving the editor.

## Reporting Issues

Use **Tools > LenzDev > Editor Enhancer > Report a Bug or Send Feedback...**, or the same button
in the Color Palette asset's Inspector, to open a pre-filled GitHub issue.

## Requirements

- Unity 6000.0 or newer

## License

MIT — see [LICENSE.md](LICENSE.md). Bundles [Harmony](https://github.com/pardeike/Harmony) (MIT) — see [Third Party Notices.md](Third%20Party%20Notices.md).
