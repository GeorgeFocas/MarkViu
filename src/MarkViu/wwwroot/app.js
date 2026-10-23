(() => {
  'use strict';

  const darkScheme = window.matchMedia('(prefers-color-scheme: dark)');
  const MERMAID_SRC = 'https://app.markviu/vendor/mermaid.min.js';

  // Diagrams render asynchronously and shift the layout, so the browser's own scroll
  // restoration lands in the wrong place. Keep the position in history.state instead,
  // which survives both reloads and Back/Forward.
  history.scrollRestoration = 'manual';

  function saveScroll() {
    history.replaceState({ ...(history.state || {}), markviuScroll: window.scrollY }, '');
  }

  function restoreScroll() {
    const y = history.state && history.state.markviuScroll;
    if (typeof y === 'number') {
      window.scrollTo(0, y);
    } else if (location.hash) {
      const target = document.getElementById(decodeURIComponent(location.hash.slice(1)));
      if (target) target.scrollIntoView();
    }
  }

  function reloadKeepingScroll() {
    saveScroll();
    location.reload();
  }

  function highlightCode() {
    if (!window.hljs) return;
    hljs.configure({ ignoreUnescapedHTML: true });
    document.querySelectorAll('pre code').forEach((el) => hljs.highlightElement(el));
  }

  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = src;
      script.onload = resolve;
      script.onerror = () => reject(new Error('Could not load ' + src));
      document.head.appendChild(script);
    });
  }

  function showDiagramError(node, source, error) {
    const box = document.createElement('div');
    box.className = 'mermaid-error';
    const title = document.createElement('strong');
    title.textContent = 'Diagram error';
    const message = document.createElement('div');
    message.textContent = (error && error.message) || String(error);
    const code = document.createElement('pre');
    code.textContent = source;
    box.append(title, message, code);
    node.replaceChildren(box);
    node.setAttribute('data-processed', 'error');
  }

  async function renderDiagrams() {
    const nodes = [...document.querySelectorAll('.mermaid')];
    if (nodes.length === 0) return;

    try {
      await loadScript(MERMAID_SRC); // ~3 MB, so only pages with diagrams pay for it
    } catch (error) {
      nodes.forEach((node) => showDiagramError(node, node.textContent, error));
      return;
    }

    mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'strict',
      theme: darkScheme.matches ? 'dark' : 'default',
    });

    for (const node of nodes) {
      const source = node.textContent.trim();
      try {
        await mermaid.parse(source); // throws a readable syntax error
        await mermaid.run({ nodes: [node] });
      } catch (error) {
        showDiagramError(node, source, error);
      }
    }
  }

  document.addEventListener('click', (e) => {
    if (e.target.closest('[data-action="back"]')) {
      e.preventDefault();
      history.back();
    }
  });

  let scrollTimer = 0;
  window.addEventListener('scroll', () => {
    clearTimeout(scrollTimer);
    scrollTimer = setTimeout(saveScroll, 150);
  }, { passive: true });
  window.addEventListener('pagehide', saveScroll);

  // The host posts "reload" when the file changes on disk.
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (e) => {
      if (e.data === 'reload') reloadKeepingScroll();
    });
  }
  // Diagrams are themed at render time, so re-render when the system theme flips.
  darkScheme.addEventListener('change', reloadKeepingScroll);

  highlightCode();
  renderDiagrams().finally(() => {
    restoreScroll();
    document.documentElement.setAttribute('data-markviu-ready', '');
  });
})();
