# MarkViu Sample

A tour of what MarkViu renders. Edit and save this file while it is open: the view refreshes on its own.

## Contents

- [Links](#links)
- [Code](#code)
- [Mermaid diagrams](#mermaid-diagrams)
- [Tables and task lists](#tables-and-task-lists)

## Links

| Kind | Link |
|---|---|
| Another document | [other.md](other.md) |
| Heading in another document | [other.md#second-section](other.md#second-section) |
| Document in a subfolder | [sub/page.md](sub/page.md) |
| Name with spaces | [My Notes (draft).md](<sub/My Notes (draft).md>) |
| A folder (README or listing) | [sub/](sub/) |
| Plain text file (opens in default app) | [notes.txt](notes.txt) |
| Web page (opens in browser) | [github.com](https://github.com) |
| Broken link | [missing.md](missing.md) |

![MarkViu logo](images/logo.svg)

## Code

```csharp
public static string FromPath(string path)
{
    var full = Path.GetFullPath(path);
    return $"https://doc.markviu/{full}";
}
```

```javascript
const greet = (name) => `Hello, ${name}!`;
console.log(greet('MarkViu'));
```

## Mermaid diagrams

```mermaid
flowchart LR
    A[Right-click .md] --> B{Open in MarkViu}
    B --> C[Render with Markdig]
    C --> D[Mermaid + highlight.js]
    D --> E((Read))
```

```mermaid
sequenceDiagram
    participant U as User
    participant W as WebView2
    participant S as DocumentServer
    U->>W: click other.md
    W->>S: GET https://doc.markviu/.../other.md
    S-->>W: rendered HTML
    W-->>U: page
```

```mermaid
classDiagram
    class DocumentServer {
        +Serve(url) ServedResponse
    }
    class MarkdownRenderer {
        +RenderPage(markdown, title) string
    }
    DocumentServer --> MarkdownRenderer
```

This one is intentionally invalid:

```mermaid
flowchart TD
    A --> 
    this is not valid ))
```

## Tables and task lists

- [x] Render markdown
- [x] Navigate links
- [ ] Take over the world

| Feature | Status |
|---|:---:|
| Mermaid | ✅ |
| Syntax highlighting | ✅ |
| Live reload | ✅ |

> **Tip:** Alt+← and Alt+→ go back and forward. Ctrl+F searches the page.
