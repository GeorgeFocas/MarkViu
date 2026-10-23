# MarkViu

A small Windows markdown viewer. Right-click a `.md` file, choose **Open in MarkViu**, and read it
rendered, with clickable links between documents, Mermaid diagrams and syntax-highlighted code.

![MarkViu](src/MarkViu/Assets/markviu.ico)

## Features

- **Explorer context menu**: "Open in MarkViu" on `.md` and `.markdown` files (per user, no admin needed).
- **Link navigation**:
  - Relative links to other markdown files open in the same window, with Back/Forward.
  - `#heading` anchors scroll to the heading.
  - Links to folders show their `README.md`, or a list of the markdown files in them.
  - Web links open in your default browser, and other local files (PDF, images…) open in their default app.
  - Executables are only revealed in Explorer, never run.
  - Ctrl+click or middle-click opens a link in a new window.
- **Mermaid diagrams** from ` ```mermaid ` blocks (flowchart, sequence, class, state, ER, Gantt…). Invalid diagrams show the parse error inline.
- **Syntax highlighting** with highlight.js.
- **Live reload**: the page re-renders when the file is saved and keeps your scroll position.
- GitHub-style rendering: tables, task lists, footnotes, emoji, front matter is hidden.
- Follows the Windows light/dark theme.
- Works fully offline: all scripts and styles are bundled.

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (to build)
- Microsoft Edge WebView2 Runtime (included with Windows 11)

## Install

```powershell
.\install.ps1
```

This publishes a build to `%LOCALAPPDATA%\Programs\MarkViu` and registers the context-menu entry.
On Windows 11 the entry is under **Show more options** (or Shift+Right-click).

The build uses the .NET 10 Desktop Runtime that comes with the SDK. To get a portable folder that runs on
machines without .NET, use `.\install.ps1 -SelfContained` (about 150 MB instead of about 10 MB).

To remove MarkViu:

```powershell
.\uninstall.ps1
```

You can also manage the menu entry yourself: `MarkViu.exe --register` / `MarkViu.exe --unregister` (add `--quiet` to skip the message box).

## Keyboard shortcuts

| Keys | Action |
|---|---|
| Alt+← / Alt+→ | Back / Forward |
| F5 | Reload |
| Ctrl+O | Open a file |
| Ctrl+F | Find in page |
| Ctrl+P | Print |
| Ctrl+mouse wheel | Zoom |

## Development

```powershell
dotnet test                                                  # unit tests
dotnet run --project src\MarkViu -- samples\index.md         # run against the sample docs
```

### How it works

The app is a WPF window hosting WebView2. Each document gets a virtual URL, for example
`D:\docs\a b.md` ⇄ `https://doc.markviu/D:/docs/a%20b.md`.
A `WebResourceRequested` handler (`Hosting/DocumentServer.cs`) renders markdown files with
[Markdig](https://github.com/xoofx/markdig) and streams every other file as-is. Because every document has a real URL,
the browser engine resolves relative links and images and keeps the navigation history on its own.
`Hosting/LinkRouter.cs` decides which links stay in the app and which go to the browser or the shell.
The page scripts and styles (`wwwroot/`) are served from `https://app.markviu/`.

Bundled third-party assets in `src/MarkViu/wwwroot/vendor`:

| Library | Version | License |
|---|---|---|
| [Mermaid](https://github.com/mermaid-js/mermaid) | 11.17.2 | MIT |
| [highlight.js](https://github.com/highlightjs/highlight.js) | 11.12.0 | BSD-3-Clause |
| [github-markdown-css](https://github.com/sindresorhus/github-markdown-css) | 5.9.0 | MIT |
