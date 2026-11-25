# Release notes

## 0.2.0 (2026-11-25)

### New: files panel

A collapsible panel on the left shows the folders and markdown files around the document you are reading, so you
can move between documents without following links.

- Shows only markdown files, with folder and file icons. Folders that contain no markdown, at any depth, are hidden.
  Hidden folders, `.git`, `node_modules`, `bin` and `obj` are skipped.
- Starts at the open document's folder. If a link leads to a document outside that folder, the panel switches to
  the new document's folder.
- The header buttons go up to the parent folder, open any folder (which also shows its README or file listing)
  and refresh the tree.
- Click a file to open it. Ctrl+click or middle-click opens it in a new window. Click a folder, or press Enter, to
  open or close it.
- The open document stays selected, including after Back and Forward.
- Updates on its own when markdown files or folders are added, renamed or deleted.
- **Ctrl+B** or the new toolbar button shows or hides the panel. Drag its edge to resize it. MarkViu remembers
  whether the panel is open and how wide it is.

### Fixes

- Saving the theme no longer erases other settings in `settings.json`.

### Upgrading

Run `.\install.ps1` from a clone, or the one-line install command from the README again.

## 0.1.0 (2026-11-04)

First release.

- "Open in MarkViu" in the Explorer context menu for `.md` and `.markdown` files (per user, no admin needed).
- Link navigation: relative links to markdown files open in the same window with Back and Forward. `#heading`
  anchors scroll to the heading, and folder links show the folder's README or a list of its markdown files. Web
  links open in your browser, other local files open in their default app, and executables are only revealed in
  Explorer, never run.
- Mermaid diagrams, syntax highlighting, and GitHub-style tables, task lists, footnotes and emoji.
- Live reload that keeps your scroll position.
- Light and dark themes. MarkViu follows the Windows theme until you choose one (Ctrl+Shift+L), then remembers your choice.
- Works fully offline.
- One-line install from GitHub (`web-install.ps1`), plus `install.ps1` and `uninstall.ps1`.
