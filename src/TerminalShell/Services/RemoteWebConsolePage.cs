namespace TerminalShell.Services;

internal static class RemoteWebConsolePage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>TerminalShell Remote</title>
  <style>
    :root {
      color-scheme: dark;
      --bg: #091218;
      --bg2: #10202a;
      --card: rgba(16, 28, 35, 0.94);
      --text: #eef7fb;
      --muted: #8ca2ae;
      --accent: #70cfff;
      --waiting: #ffd777;
      --danger: #ff9292;
      --success: #61de8a;
      --border: rgba(128, 165, 182, 0.18);
      --shadow: 0 18px 44px rgba(0, 0, 0, 0.28);
    }
    * { box-sizing: border-box; }
    html, body {
      margin: 0;
      height: 100%;
      min-height: 100%;
      background:
        radial-gradient(circle at top right, rgba(86, 209, 200, 0.14), transparent 26%),
        radial-gradient(circle at top left, rgba(64, 161, 255, 0.12), transparent 22%),
        linear-gradient(180deg, var(--bg) 0%, var(--bg2) 100%);
      color: var(--text);
      font-family: "Aptos", "Segoe UI Variable Display", "Bahnschrift", "Segoe UI", sans-serif;
      -webkit-tap-highlight-color: transparent;
      overscroll-behavior-y: none;
      overflow-x: hidden;
    }
    body.fullscreen-open { overflow: hidden; }
    button, input, textarea { font: inherit; }
    .hidden { display: none !important; }
    .page { min-height: 100vh; min-height: 100dvh; padding: 12px 12px calc(20px + env(safe-area-inset-bottom, 0px)); overscroll-behavior-y: none; }
    .card { background: var(--card); border: 1px solid var(--border); border-radius: 16px; box-shadow: var(--shadow); backdrop-filter: blur(16px); }
    .muted { color: var(--muted); }
    .eyebrow { color: var(--success); font-size: 12px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; }
    .msg { min-height: 20px; font-size: 13px; color: var(--muted); }
    input, textarea {
      width: 100%;
      border: 1px solid rgba(128, 165, 182, 0.22);
      background: rgba(8, 19, 24, 0.9);
      color: var(--text);
      border-radius: 12px;
      padding: 11px 12px;
      outline: none;
    }
    input:focus, textarea:focus { border-color: rgba(86, 209, 200, 0.72); box-shadow: 0 0 0 3px rgba(86, 209, 200, 0.14); }
    textarea { min-height: 62px; max-height: 180px; resize: vertical; }
    .btn {
      border: 0;
      border-radius: 12px;
      min-height: 40px;
      padding: 10px 14px;
      font-weight: 700;
      cursor: pointer;
      background: linear-gradient(135deg, #6de4da 0%, #39bfb5 100%);
      color: #051216;
      touch-action: manipulation;
    }
    .btn.secondary { background: #334855; color: var(--text); }
    .btn.danger { background: #7d3940; color: var(--text); }
    .login {
      max-width: 420px;
      margin: min(11vh, 82px) auto 0;
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .login h1, .empty-title { margin: 0; line-height: 1.08; }
    .login h1 { font-size: 28px; }
    .topbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 12px 14px;
      margin-bottom: 10px;
    }
    .current-terminal {
      margin: 0;
      font-size: 22px;
      font-weight: 800;
      line-height: 1.15;
      word-break: break-word;
      touch-action: pan-y;
      user-select: none;
    }
    .tone-idle { color: var(--text); }
    .tone-active { color: var(--accent); }
    .tone-waiting { color: var(--waiting); }
    .tone-danger { color: var(--danger); }
    .more-button {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      flex: 0 0 auto;
      border: 1px solid rgba(128, 165, 182, 0.2);
      border-radius: 12px;
      padding: 10px 12px;
      background: rgba(10, 20, 26, 0.9);
      color: var(--text);
      cursor: pointer;
      font-weight: 700;
    }
    .more-button.has-alert { border-color: rgba(97, 222, 138, 0.35); }
    .more-pulse {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: var(--success);
      box-shadow: 0 0 0 rgba(97, 222, 138, 0.35);
    }
    .more-pulse.active { animation: morePulse 2.1s ease-in-out infinite; }
    @keyframes morePulse {
      0% { opacity: 0.42; transform: scale(0.92); box-shadow: 0 0 0 0 rgba(97, 222, 138, 0.10); }
      50% { opacity: 1; transform: scale(1); box-shadow: 0 0 0 8px rgba(97, 222, 138, 0.00); }
      100% { opacity: 0.42; transform: scale(0.92); box-shadow: 0 0 0 0 rgba(97, 222, 138, 0.00); }
    }
    .more-panel { margin-bottom: 10px; padding: 10px; }
    .more-list { display: flex; flex-direction: column; gap: 8px; }
    .more-item {
      border: 1px solid rgba(128, 165, 182, 0.14);
      border-radius: 14px;
      background: rgba(15, 27, 33, 0.84);
      overflow: hidden;
    }
    .more-item.current {
      border-color: rgba(112, 207, 255, 0.34);
      box-shadow: inset 0 0 0 1px rgba(112, 207, 255, 0.22);
    }
    .more-trigger {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 10px;
      border: 0;
      background: transparent;
      color: inherit;
      cursor: pointer;
      padding: 12px;
      text-align: left;
    }
    .more-name { font-size: 15px; font-weight: 700; line-height: 1.25; word-break: break-word; }
    .more-state {
      color: var(--muted);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.05em;
      text-transform: uppercase;
      white-space: nowrap;
    }
    .more-details {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 0 12px 12px;
      border-top: 1px solid rgba(128, 165, 182, 0.12);
    }
    .detail-row { display: flex; flex-direction: column; gap: 4px; }
    .detail-label {
      color: var(--muted);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    .detail-value { font-size: 13px; line-height: 1.45; word-break: break-word; }
    .detail-value.mono, .output, .draft-text { font-family: Consolas, "Cascadia Mono", "SFMono-Regular", monospace; }
    .detail-tags { display: flex; flex-wrap: wrap; gap: 6px; }
    .tag {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 9px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 700;
      border: 1px solid rgba(255, 255, 255, 0.08);
      background: rgba(58, 78, 90, 0.72);
      color: var(--text);
      white-space: nowrap;
    }
    .inline-switch {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 10px;
      padding-top: 4px;
    }
    .switch { position: relative; width: 52px; height: 30px; flex: 0 0 auto; }
    .switch input { position: absolute; inset: 0; opacity: 0; margin: 0; cursor: pointer; }
    .switch-track { width: 52px; height: 30px; border-radius: 999px; background: #374a56; border: 1px solid rgba(255, 255, 255, 0.08); position: relative; }
    .switch-thumb { position: absolute; top: 4px; left: 4px; width: 20px; height: 20px; border-radius: 50%; background: #f5fbff; transition: transform 140ms ease; box-shadow: 0 3px 9px rgba(0, 0, 0, 0.24); }
    .switch input:checked + .switch-track { background: rgba(31, 185, 175, 0.9); border-color: rgba(86, 209, 200, 0.6); }
    .switch input:checked + .switch-track .switch-thumb { transform: translateX(22px); }
    .notice {
      margin-bottom: 10px;
      padding: 10px 12px;
      border-radius: 12px;
      border: 1px solid rgba(128, 165, 182, 0.18);
      background: rgba(22, 40, 48, 0.9);
      color: var(--text);
    }
    .notice.error { border-color: rgba(255, 141, 141, 0.4); background: rgba(91, 33, 39, 0.88); color: #ffd5d5; }
    .empty-state, .output-card, .command-card, .draft-panel { margin-bottom: 10px; }
    .empty-state { padding: 14px; }
    .empty-title { font-size: 22px; }
    .output-card { overflow: hidden; padding: 0; position: relative; }
    .output {
      margin: 0;
      min-height: 280px;
      max-height: 44vh;
      overflow: auto;
      padding: 14px;
      white-space: pre-wrap;
      word-break: break-word;
      line-height: 1.45;
      font-size: 12px;
      background: linear-gradient(180deg, rgba(7, 16, 21, 0.96) 0%, rgba(11, 21, 26, 1) 100%);
      overscroll-behavior: contain;
      -webkit-overflow-scrolling: touch;
      touch-action: manipulation;
    }
    .fullscreen-overlay {
      position: fixed;
      inset: 0;
      z-index: 1000;
      width: 100vw;
      height: 100vh;
      height: 100dvh;
      padding: max(10px, env(safe-area-inset-top, 0px)) max(10px, env(safe-area-inset-right, 0px)) max(10px, env(safe-area-inset-bottom, 0px)) max(10px, env(safe-area-inset-left, 0px));
      background: rgba(4, 10, 14, 0.78);
      backdrop-filter: blur(10px);
    }
    .fullscreen-dialog {
      display: flex;
      flex-direction: column;
      gap: 10px;
      width: min(100%, 980px);
      height: 100%;
      min-height: 100%;
      margin: 0 auto;
      padding: 12px;
    }
    .fullscreen-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
    }
    .fullscreen-eyebrow {
      display: block;
      margin-bottom: 2px;
      color: rgba(97, 222, 138, 0.88);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.08em;
      line-height: 1;
      text-transform: uppercase;
    }
    .fullscreen-title {
      margin: 0;
      font-size: 20px;
      font-weight: 800;
      line-height: 1.15;
      word-break: break-word;
    }
    .fullscreen-close { min-width: 88px; flex: 0 0 auto; }
    .fullscreen-body {
      position: relative;
      display: flex;
      flex: 1 1 0;
      min-height: 0;
    }
    .fullscreen-output {
      flex: 1 1 0;
      min-height: 0;
      height: auto;
      max-height: none;
      padding: 14px 58px 14px 14px;
      border-radius: 14px;
      border: 1px solid rgba(128, 165, 182, 0.14);
    }
    .fullscreen-page-controls {
      position: absolute;
      top: 50%;
      right: 8px;
      transform: translateY(-50%);
      display: flex;
      flex-direction: column;
      gap: 8px;
      z-index: 2;
      pointer-events: none;
    }
    .fullscreen-page-button {
      width: 38px;
      min-width: 38px;
      min-height: 38px;
      height: 38px;
      padding: 0;
      border-radius: 999px;
      font-size: 18px;
      line-height: 1;
      box-shadow: 0 6px 18px rgba(0, 0, 0, 0.28);
      pointer-events: auto;
    }
    .command-card { padding: 10px; }
    .command-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 10px; align-items: stretch; }
    .command-row .btn { min-width: 86px; }
    .draft-panel { padding: 10px; }
    .draft-head { font-size: 14px; font-weight: 700; margin-bottom: 10px; }
    .draft-list { display: flex; flex-direction: column; gap: 8px; }
    .draft-item {
      padding: 12px;
      border-radius: 14px;
      border: 1px solid rgba(128, 165, 182, 0.14);
      background: rgba(13, 24, 30, 0.82);
    }
    .draft-top { display: flex; justify-content: space-between; align-items: flex-start; gap: 10px; margin-bottom: 8px; }
    .draft-preview { font-size: 14px; font-weight: 700; line-height: 1.35; }
    .draft-time { color: var(--muted); font-size: 12px; white-space: nowrap; }
    .draft-text { white-space: pre-wrap; margin-bottom: 10px; color: #dce9f0; max-height: 132px; overflow: auto; }
    .draft-actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .footer-credit {
      width: 100%;
      margin: 10px 0 8px;
      display: grid;
      grid-template-columns: 44px minmax(0, 1fr) 44px;
      align-items: center;
      gap: 10px;
      padding: 10px 12px;
      border: 1px solid rgba(128, 165, 182, 0.16);
      border-radius: 14px;
      background: rgba(10, 20, 26, 0.78);
      color: var(--muted);
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: none;
    }
    .footer-credit-text {
      text-align: center;
    }
    .footer-actions {
      display: flex;
      justify-content: center;
      margin-top: 18px;
    }
    .footer-nav-button {
      width: 44px;
      min-width: 44px;
      min-height: 44px;
      height: 44px;
      padding: 0;
      border: 1px solid rgba(128, 165, 182, 0.2);
      border-radius: 999px;
      background: rgba(10, 20, 26, 0.9);
      color: var(--text);
      font-size: 24px;
      line-height: 1;
      font-weight: 700;
      cursor: pointer;
    }
    .logout-button {
      width: auto;
      min-width: 104px;
      border: 1px solid rgba(128, 165, 182, 0.2);
      border-radius: 14px;
      padding: 12px 18px;
      background: rgba(10, 20, 26, 0.9);
      color: var(--text);
      font-weight: 800;
      cursor: pointer;
    }
    @media (max-width: 520px) {
      .page { padding: 10px 10px calc(16px + env(safe-area-inset-bottom, 0px)); }
      .current-terminal { font-size: 20px; }
      .output { min-height: 220px; max-height: 38vh; }
      .fullscreen-dialog { padding: 10px; }
      .fullscreen-title { font-size: 18px; }
      .fullscreen-output {
        flex: 1 1 auto;
        min-height: 0;
        height: auto;
        max-height: none;
        padding: 10px 48px 10px 10px;
      }
      .command-row { grid-template-columns: minmax(0, 1fr) 84px; }
    }
    @media (max-width: 759px) {
      .fullscreen-overlay {
        padding: 0;
        background: rgba(5, 12, 17, 0.96);
        backdrop-filter: none;
      }
      .fullscreen-dialog {
        width: 100vw;
        height: 100vh;
        height: 100dvh;
        min-height: 100dvh;
        margin: 0;
        gap: 8px;
        padding: max(4px, env(safe-area-inset-top, 0px)) max(6px, env(safe-area-inset-right, 0px)) max(6px, env(safe-area-inset-bottom, 0px)) max(6px, env(safe-area-inset-left, 0px));
        border: 0;
        border-radius: 0;
        box-shadow: none;
        overflow: hidden;
      }
      .fullscreen-header {
        gap: 8px;
      }
      .fullscreen-eyebrow {
        margin-bottom: 1px;
        font-size: 9px;
        letter-spacing: 0.06em;
        opacity: 0.7;
      }
      .fullscreen-title {
        font-size: 17px;
        line-height: 1.08;
      }
      .fullscreen-close {
        min-width: 72px;
        min-height: 36px;
        padding: 8px 12px;
        border-radius: 10px;
      }
      .fullscreen-output {
        flex: 1 1 auto;
        min-height: 0;
        height: auto;
        max-height: none;
        border-radius: 10px;
        padding: 10px 48px 10px 10px;
      }
      .fullscreen-page-controls {
        right: 6px;
        gap: 6px;
      }
      .fullscreen-page-button {
        width: 34px;
        min-width: 34px;
        min-height: 34px;
        height: 34px;
        font-size: 16px;
      }
    }
    @media (min-width: 760px) {
      .page { max-width: 900px; margin: 0 auto; }
      .output { min-height: 360px; max-height: 52vh; font-size: 13px; }
    }
  </style>
</head>
<body>
  <div class="page">
    <section id="loginView" class="card login">
      <div class="eyebrow">Remote Control</div>
      <h1>TerminalShell Remote</h1>
      <p class="muted">Sign in with the remote access password from Global Settings.</p>
      <input id="passwordInput" type="password" autocomplete="current-password" placeholder="Remote password">
      <button id="loginButton" class="btn" type="button">Login</button>
      <div id="messageBar" class="msg"></div>
    </section>

    <section id="appView" class="hidden">
      <header class="card topbar">
        <h1 id="currentTerminalName" class="current-terminal tone-idle">No terminal selected</h1>
        <button id="moreButton" class="more-button" type="button">
          <span>More</span>
          <span id="morePulse" class="more-pulse hidden" aria-hidden="true"></span>
        </button>
      </header>

      <section id="morePanel" class="card more-panel hidden">
        <div id="moreList" class="more-list"></div>
      </section>

      <div id="appNotice" class="notice hidden"></div>

      <div id="emptyState" class="card empty-state">
        <h2 id="emptyTitle" class="empty-title">No terminals available</h2>
        <p id="emptyBody" class="muted">The remote page only shows terminals that are already visible and running in the main window.</p>
      </div>

      <section id="terminalView" class="hidden">
        <article class="card output-card">
          <pre id="terminalOutput" class="output"></pre>
        </article>

        <form id="commandForm" class="card command-card">
          <div class="command-row">
            <textarea id="commandInput" placeholder="Type a command for the current terminal."></textarea>
            <button class="btn" type="submit">Send</button>
          </div>
        </form>

        <div class="footer-credit">
          <button id="prevTerminalButton" class="footer-nav-button" type="button" aria-label="Previous terminal">&larr;</button>
          <div class="footer-credit-text">Power by TerminalShell</div>
          <button id="nextTerminalButton" class="footer-nav-button" type="button" aria-label="Next terminal">&rarr;</button>
        </div>

        <section id="draftPanel" class="card draft-panel hidden">
          <div class="draft-head">Drafts</div>
          <div id="draftList" class="draft-list"></div>
        </section>
      </section>

      <div id="outputFullscreen" class="fullscreen-overlay hidden">
        <div class="fullscreen-dialog card">
          <div class="fullscreen-header">
            <div>
              <div class="fullscreen-eyebrow">Fullscreen Output</div>
              <div id="fullscreenTerminalName" class="fullscreen-title tone-idle">No terminal selected</div>
            </div>
            <button id="fullscreenCloseButton" class="btn secondary fullscreen-close" type="button">Close</button>
          </div>
          <div class="fullscreen-body">
            <pre id="fullscreenTerminalOutput" class="output fullscreen-output"></pre>
            <div class="fullscreen-page-controls" aria-label="Fullscreen paging controls">
              <button id="fullscreenPageUpButton" class="btn secondary fullscreen-page-button" type="button" aria-label="Page up">&uarr;</button>
              <button id="fullscreenPageDownButton" class="btn secondary fullscreen-page-button" type="button" aria-label="Page down">&darr;</button>
            </div>
          </div>
        </div>
      </div>

      <div class="footer-actions">
        <button id="logoutButton" class="logout-button" type="button">Logout</button>
      </div>
    </section>
  </div>

  <script>
    const selectedSessionStorageKey = 'terminalshell.remote.selectedSessionName';
    const state = {
      selectedSessionName: null,
      selectedSession: null,
      sessions: [],
      dashboardSocket: null,
      detailSocket: null,
      isMorePanelOpen: false,
      expandedSessionName: null,
      swipeTrackingEnabled: false,
      swipeStartX: 0,
      swipeStartY: 0,
      swipeLastX: 0,
      swipeLastY: 0,
      swipeAxis: 'none',
      lastSwipeTriggeredAt: 0,
      isOutputFullscreen: false,
      lastOutputTapAt: 0,
      lastOutputTapX: 0,
      lastOutputTapY: 0,
      lastOutputTouchStartX: 0,
      lastOutputTouchStartY: 0,
      outputScrollTopBySession: {},
      fullscreenScrollTopBySession: {},
      outputRenderedSessionName: null,
      fullscreenRenderedSessionName: null
    };

    const swipeMinimumDistance = 48;
    const swipeHorizontalDominanceRatio = 1.35;
    const swipeSurface = document.querySelector('.page');
    const loginView = document.getElementById('loginView');
    const appView = document.getElementById('appView');
    const passwordInput = document.getElementById('passwordInput');
    const loginButton = document.getElementById('loginButton');
    const logoutButton = document.getElementById('logoutButton');
    const prevTerminalButton = document.getElementById('prevTerminalButton');
    const nextTerminalButton = document.getElementById('nextTerminalButton');
    const moreButton = document.getElementById('moreButton');
    const morePanel = document.getElementById('morePanel');
    const moreList = document.getElementById('moreList');
    const morePulse = document.getElementById('morePulse');
    const currentTerminalName = document.getElementById('currentTerminalName');
    const messageBar = document.getElementById('messageBar');
    const appNotice = document.getElementById('appNotice');
    const emptyState = document.getElementById('emptyState');
    const emptyTitle = document.getElementById('emptyTitle');
    const emptyBody = document.getElementById('emptyBody');
    const terminalView = document.getElementById('terminalView');
    const terminalOutput = document.getElementById('terminalOutput');
    const outputFullscreen = document.getElementById('outputFullscreen');
    const fullscreenCloseButton = document.getElementById('fullscreenCloseButton');
    const fullscreenPageUpButton = document.getElementById('fullscreenPageUpButton');
    const fullscreenPageDownButton = document.getElementById('fullscreenPageDownButton');
    const fullscreenTerminalName = document.getElementById('fullscreenTerminalName');
    const fullscreenTerminalOutput = document.getElementById('fullscreenTerminalOutput');
    const commandForm = document.getElementById('commandForm');
    const commandInput = document.getElementById('commandInput');
    const draftPanel = document.getElementById('draftPanel');
    const draftList = document.getElementById('draftList');

    loginButton.addEventListener('click', handleLogin);
    logoutButton.addEventListener('click', handleLogout);
    prevTerminalButton.addEventListener('click', () => selectRelativeSession(-1));
    nextTerminalButton.addEventListener('click', () => selectRelativeSession(1));
    moreButton.addEventListener('click', toggleMorePanel);
    commandForm.addEventListener('submit', sendCommand);
    commandInput.addEventListener('input', autoResizeCommandInput);
    terminalOutput.addEventListener('scroll', handleTerminalOutputScroll, { passive: true });
    fullscreenTerminalOutput.addEventListener('scroll', handleFullscreenOutputScroll, { passive: true });
    terminalOutput.addEventListener('touchstart', handleOutputTouchStart, { passive: true });
    terminalOutput.addEventListener('touchend', handleOutputTouchEnd, { passive: true });
    terminalOutput.addEventListener('dblclick', openOutputFullscreen);
    fullscreenCloseButton.addEventListener('click', closeOutputFullscreen);
    fullscreenPageUpButton.addEventListener('click', () => scrollFullscreenByPage(-1));
    fullscreenPageDownButton.addEventListener('click', () => scrollFullscreenByPage(1));
    passwordInput.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        handleLogin();
      }
    });
    swipeSurface.addEventListener('touchstart', handleSwipeTouchStart, { passive: true });
    swipeSurface.addEventListener('touchmove', handleSwipeTouchMove, { passive: false });
    swipeSurface.addEventListener('touchend', handleSwipeTouchEnd, { passive: true });
    swipeSurface.addEventListener('touchcancel', resetSwipeTracking, { passive: true });
    document.addEventListener('keydown', handleDocumentKeyDown);

    function loadRememberedSelectedSessionName() {
      try {
        const value = window.sessionStorage.getItem(selectedSessionStorageKey);
        return value && value.trim() ? value.trim() : null;
      } catch (_) {
        return null;
      }
    }

    function saveRememberedSelectedSessionName(sessionName) {
      try {
        if (sessionName && sessionName.trim()) {
          window.sessionStorage.setItem(selectedSessionStorageKey, sessionName.trim());
        } else {
          window.sessionStorage.removeItem(selectedSessionStorageKey);
        }
      } catch (_) {
      }
    }

    function setLoginMessage(text, isError = false) {
      messageBar.textContent = text || '';
      messageBar.style.color = isError ? '#ffb5b5' : '#8ca2ae';
    }

    function setAppNotice(text, isError = false) {
      if (!text) {
        appNotice.textContent = '';
        appNotice.classList.add('hidden');
        appNotice.classList.remove('error');
        return;
      }

      appNotice.textContent = text;
      appNotice.classList.remove('hidden');
      appNotice.classList.toggle('error', !!isError);
    }

    function normalizeStatus(status) {
      return String(status || 'Idle');
    }

    function getStatusKey(status) {
      return normalizeStatus(status).replace(/\s+/g, '').toLowerCase();
    }

    function getStatusTone(status) {
      switch (getStatusKey(status)) {
        case 'busy':
        case 'submitted':
          return 'active';
        case 'waitingforinput':
          return 'waiting';
        case 'failed':
        case 'exited':
          return 'danger';
        default:
          return 'idle';
      }
    }

    function getQueueText(session) {
      if (session?.isAutoDraftQueueWaitingForUserInput) {
        return 'Queue paused';
      }
      if (session?.isAutoDraftQueueActive) {
        return 'Queue active';
      }
      if (session?.isAutoDraftQueueEnabled) {
        return 'Queue armed';
      }
      return 'Queue off';
    }

    function autoResizeCommandInput() {
      commandInput.style.height = 'auto';
      commandInput.style.height = `${Math.min(Math.max(commandInput.scrollHeight, 62), 180)}px`;
    }

    function formatStableSeconds(value) {
      const total = Number(value || 0);
      if (!Number.isFinite(total) || total <= 0) { return '0s'; }
      if (total < 60) { return `${Math.floor(total)}s`; }
      const minutes = Math.floor(total / 60);
      const seconds = Math.floor(total % 60);
      if (minutes < 60) { return seconds > 0 ? `${minutes}m ${seconds}s` : `${minutes}m`; }
      const hours = Math.floor(minutes / 60);
      const restMinutes = minutes % 60;
      return restMinutes > 0 ? `${hours}h ${restMinutes}m` : `${hours}h`;
    }

    function formatUtc(value) {
      if (!value) { return ''; }
      const date = new Date(value);
      return Number.isNaN(date.getTime()) ? '' : date.toLocaleString();
    }

    function escapeHtml(value) {
      return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;');
    }

    function confirmDraftAction(actionLabel, draft) {
      const preview = String(draft?.previewText || draft?.text || '[Draft]')
        .replace(/\s+/g, ' ')
        .trim()
        .slice(0, 120);
      return window.confirm(`${actionLabel}\n\n${preview || '[Draft]'}`);
    }

    function getSelectedSummary() {
      return getSessionByName(state.selectedSessionName);
    }

    function getSessionByName(sessionName) {
      return state.sessions.find(session => session.name === sessionName) || null;
    }

    function mergeSessionSnapshot(session) {
      if (!session?.name) {
        return;
      }

      const index = state.sessions.findIndex(item => item.name === session.name);
      const draftCount = session.draftCount ?? (Array.isArray(session.drafts) ? session.drafts.length : 0);
      const merged = index >= 0
        ? {
            ...state.sessions[index],
            ...session,
            draftCount,
            hasDrafts: session.hasDrafts ?? draftCount > 0
          }
        : {
            ...session,
            draftCount,
            hasDrafts: session.hasDrafts ?? draftCount > 0
          };

      if (index >= 0) {
        state.sessions[index] = merged;
      } else {
        state.sessions.push(merged);
      }

      if (state.selectedSessionName === merged.name) {
        state.selectedSession = {
          ...(state.selectedSession || {}),
          ...merged
        };
      }
    }

    function renderCurrentHeader() {
      const session = state.selectedSession || getSelectedSummary();
      if (!session) {
        currentTerminalName.textContent = 'No terminal selected';
        currentTerminalName.className = 'current-terminal tone-idle';
        renderFullscreenHeader();
        return;
      }

      currentTerminalName.textContent = session.name || 'No terminal selected';
      currentTerminalName.className = `current-terminal tone-${getStatusTone(session.status)}`;
      renderFullscreenHeader();
    }

    function renderFullscreenHeader() {
      const session = state.selectedSession || getSelectedSummary();
      if (!session) {
        fullscreenTerminalName.textContent = 'No terminal selected';
        fullscreenTerminalName.className = 'fullscreen-title tone-idle';
        return;
      }

      fullscreenTerminalName.textContent = session.name || 'No terminal selected';
      fullscreenTerminalName.className = `fullscreen-title tone-${getStatusTone(session.status)}`;
    }

    function shouldShowMoreAttention() {
      return state.sessions.some(session => {
        if (session.name === state.selectedSessionName) {
          return false;
        }

        const key = getStatusKey(session.status);
        return key === 'completed' || key === 'waitingforinput' || key === 'failed';
      });
    }

    function updateMorePulse() {
      const isActive = shouldShowMoreAttention();
      morePulse.classList.toggle('hidden', !isActive);
      morePulse.classList.toggle('active', isActive);
      moreButton.classList.toggle('has-alert', isActive);
    }

    function showEmptyState(title, body) {
      setOutputFullscreenVisible(false);
      emptyTitle.textContent = title;
      emptyBody.textContent = body;
      emptyState.classList.remove('hidden');
      terminalView.classList.add('hidden');
      commandInput.disabled = true;
    }

    function showTerminalView() {
      emptyState.classList.add('hidden');
      terminalView.classList.remove('hidden');
      commandInput.disabled = false;
    }

    function showLogin(message) {
      closeSockets();
      setOutputFullscreenVisible(false);
      state.selectedSessionName = null;
      state.selectedSession = null;
      state.sessions = [];
      state.isMorePanelOpen = false;
      state.expandedSessionName = null;
      morePanel.classList.add('hidden');
      moreList.innerHTML = '';
      terminalOutput.textContent = '';
      fullscreenTerminalOutput.textContent = '';
      draftList.innerHTML = '';
      draftPanel.classList.add('hidden');
      renderCurrentHeader();
      updateMorePulse();
      loginView.classList.remove('hidden');
      appView.classList.add('hidden');
      setLoginMessage(message || '');
      setAppNotice('');
    }

    function showApp() {
      loginView.classList.add('hidden');
      appView.classList.remove('hidden');
      setLoginMessage('');
    }

    function handleDocumentKeyDown(event) {
      if (event.key === 'Escape' && state.isOutputFullscreen) {
        event.preventDefault();
        closeOutputFullscreen();
      }
    }

    function setOutputFullscreenVisible(isVisible) {
      state.isOutputFullscreen = !!isVisible;
      outputFullscreen.classList.toggle('hidden', !state.isOutputFullscreen);
      document.body.classList.toggle('fullscreen-open', state.isOutputFullscreen);
      renderFullscreenHeader();
    }

    function getBrowserFullscreenElement() {
      return document.fullscreenElement || document.webkitFullscreenElement || null;
    }

    function isPhoneSizedViewport() {
      const width = Math.min(window.innerWidth || 0, window.innerHeight || 0);
      return width > 0 && width <= 759;
    }

    function isTouchCapableDevice() {
      return navigator.maxTouchPoints > 0 || window.matchMedia('(pointer: coarse)').matches;
    }

    function shouldUseBrowserFullscreen() {
      return !(isTouchCapableDevice() && isPhoneSizedViewport());
    }

    function swallowFullscreenPromise(result) {
      if (result && typeof result.catch === 'function') {
        result.catch(() => {});
      }
    }

    function requestBrowserFullscreen(element) {
      if (!element || getBrowserFullscreenElement()) {
        return;
      }

      const request = element.requestFullscreen || element.webkitRequestFullscreen;
      if (!request) {
        return;
      }

      try {
        swallowFullscreenPromise(request.call(element, { navigationUI: 'hide' }));
      } catch (_) {
        try {
          swallowFullscreenPromise(request.call(element));
        } catch (_) {
        }
      }
    }

    function exitBrowserFullscreen() {
      if (!getBrowserFullscreenElement()) {
        return;
      }

      const exit = document.exitFullscreen || document.webkitExitFullscreen;
      if (!exit) {
        return;
      }

      try {
        swallowFullscreenPromise(exit.call(document));
      } catch (_) {
      }
    }

    function getScrollBucket(isFullscreen) {
      return isFullscreen ? state.fullscreenScrollTopBySession : state.outputScrollTopBySession;
    }

    function getRememberedScrollTop(sessionName, isFullscreen) {
      if (!sessionName) {
        return null;
      }

      const bucket = getScrollBucket(isFullscreen);
      return Object.prototype.hasOwnProperty.call(bucket, sessionName)
        ? Number(bucket[sessionName] || 0)
        : null;
    }

    function rememberScrollTop(sessionName, scrollTop, isFullscreen) {
      if (!sessionName) {
        return;
      }

      getScrollBucket(isFullscreen)[sessionName] = Math.max(0, Number(scrollTop || 0));
    }

    function clampScrollTop(element, value) {
      const max = Math.max(0, element.scrollHeight - element.clientHeight);
      return Math.min(Math.max(0, Number(value || 0)), max);
    }

    function isNearBottom(element) {
      return element.scrollHeight - element.scrollTop - element.clientHeight < 28;
    }

    function scrollToBottom(element) {
      element.scrollTop = element.scrollHeight;
    }

    function renderOutputSnapshot(element, snapshot, sessionName, isFullscreen) {
      const renderedKey = isFullscreen ? 'fullscreenRenderedSessionName' : 'outputRenderedSessionName';
      const previousSessionName = state[renderedKey];
      const isSameSession = previousSessionName === sessionName;
      const shouldStick = isSameSession && isNearBottom(element);
      const rememberedScrollTop = getRememberedScrollTop(sessionName, isFullscreen);

      element.textContent = snapshot || '';
      state[renderedKey] = sessionName;

      window.requestAnimationFrame(() => {
        if (shouldStick) {
          scrollToBottom(element);
        } else if (rememberedScrollTop != null) {
          element.scrollTop = clampScrollTop(element, rememberedScrollTop);
        } else {
          scrollToBottom(element);
        }

        rememberScrollTop(sessionName, element.scrollTop, isFullscreen);
      });
    }

    function syncScrollPosition(sourceElement, targetElement, sessionName, targetIsFullscreen) {
      if (!sessionName) {
        return;
      }

      const sourceMax = Math.max(0, sourceElement.scrollHeight - sourceElement.clientHeight);
      const ratio = sourceMax > 0 ? sourceElement.scrollTop / sourceMax : 1;

      window.requestAnimationFrame(() => {
        const targetMax = Math.max(0, targetElement.scrollHeight - targetElement.clientHeight);
        targetElement.scrollTop = targetMax > 0 ? ratio * targetMax : 0;
        rememberScrollTop(sessionName, targetElement.scrollTop, targetIsFullscreen);
      });
    }

    function getFullscreenPageStep() {
      return Math.max(Math.round(fullscreenTerminalOutput.clientHeight * 0.85), 120);
    }

    function scrollFullscreenByPage(direction) {
      if (!state.isOutputFullscreen) {
        return;
      }

      const delta = getFullscreenPageStep() * direction;
      const nextScrollTop = clampScrollTop(fullscreenTerminalOutput, fullscreenTerminalOutput.scrollTop + delta);
      fullscreenTerminalOutput.scrollTop = nextScrollTop;

      if (state.selectedSessionName) {
        rememberScrollTop(state.selectedSessionName, nextScrollTop, true);
      }
    }

    function openOutputFullscreen() {
      const session = state.selectedSession || getSelectedSummary();
      if (!session) {
        return;
      }

      setOutputFullscreenVisible(true);
      if (shouldUseBrowserFullscreen()) {
        requestBrowserFullscreen(outputFullscreen);
      }
      renderOutputSnapshot(fullscreenTerminalOutput, session.tailSnapshot || '', session.name, true);
      syncScrollPosition(terminalOutput, fullscreenTerminalOutput, session.name, true);
    }

    function closeOutputFullscreen() {
      if (!state.isOutputFullscreen) {
        return;
      }

      const sessionName = state.selectedSessionName;
      if (sessionName) {
        syncScrollPosition(fullscreenTerminalOutput, terminalOutput, sessionName, false);
      }

      setOutputFullscreenVisible(false);
      exitBrowserFullscreen();
    }

    function handleTerminalOutputScroll() {
      if (!state.selectedSessionName) {
        return;
      }

      rememberScrollTop(state.selectedSessionName, terminalOutput.scrollTop, false);
    }

    function handleFullscreenOutputScroll() {
      if (!state.selectedSessionName) {
        return;
      }

      rememberScrollTop(state.selectedSessionName, fullscreenTerminalOutput.scrollTop, true);
    }

    function handleOutputTouchStart(event) {
      const touch = event.touches?.[0];
      if (!touch) {
        return;
      }

      state.lastOutputTouchStartX = touch.clientX;
      state.lastOutputTouchStartY = touch.clientY;
    }

    function handleOutputTouchEnd(event) {
      if (state.isOutputFullscreen || Date.now() - state.lastSwipeTriggeredAt < 360) {
        return;
      }

      const touch = event.changedTouches?.[0];
      if (!touch) {
        return;
      }

      const moveX = Math.abs(touch.clientX - state.lastOutputTouchStartX);
      const moveY = Math.abs(touch.clientY - state.lastOutputTouchStartY);
      if (moveX > 16 || moveY > 16) {
        return;
      }

      const now = Date.now();
      const isDoubleTap = state.lastOutputTapAt > 0
        && now - state.lastOutputTapAt <= 320
        && Math.abs(touch.clientX - state.lastOutputTapX) <= 20
        && Math.abs(touch.clientY - state.lastOutputTapY) <= 20;

      state.lastOutputTapAt = now;
      state.lastOutputTapX = touch.clientX;
      state.lastOutputTapY = touch.clientY;

      if (!isDoubleTap) {
        return;
      }

      state.lastOutputTapAt = 0;
      openOutputFullscreen();
    }

    function closeDetailSocket() {
      if (!state.detailSocket) {
        return;
      }

      const socket = state.detailSocket;
      state.detailSocket = null;
      socket.onclose = null;
      socket.close();
    }

    function closeSockets() {
      if (state.dashboardSocket) {
        const socket = state.dashboardSocket;
        state.dashboardSocket = null;
        socket.onclose = null;
        socket.close();
      }

      closeDetailSocket();
    }

    function toggleMorePanel() {
      if (!state.sessions.length) {
        return;
      }

      state.isMorePanelOpen = !state.isMorePanelOpen;
      if (state.isMorePanelOpen && !state.expandedSessionName) {
        state.expandedSessionName = state.selectedSessionName || state.sessions[0]?.name || null;
      }

      renderMorePanel();
    }

    function renderMorePanel() {
      updateMorePulse();

      if (!state.isMorePanelOpen) {
        morePanel.classList.add('hidden');
        moreList.innerHTML = '';
        return;
      }

      morePanel.classList.remove('hidden');
      moreList.innerHTML = '';

      if (!state.sessions.length) {
        moreList.innerHTML = '<div class="muted">No visible terminals are available right now.</div>';
        return;
      }

      for (const session of state.sessions) {
        const draftCount = Number(session.draftCount || 0);
        const isCurrent = session.name === state.selectedSessionName;
        const isExpanded = state.expandedSessionName === session.name;
        const tone = getStatusTone(session.status);
        const voiceName = session.terminalVoiceName && session.terminalVoiceName !== session.name
          ? `<div class="muted">${escapeHtml(session.terminalVoiceName)}</div>`
          : '';

        const item = document.createElement('div');
        item.className = `more-item${isCurrent ? ' current' : ''}`;
        item.innerHTML = `
          <button class="more-trigger" type="button">
            <div>
              <div class="more-name tone-${tone}">${escapeHtml(session.name)}</div>
              ${voiceName}
            </div>
            <div class="more-state tone-${tone}">${escapeHtml(normalizeStatus(session.status))}</div>
          </button>
          ${isExpanded ? `
            <div class="more-details">
              <div class="detail-tags">
                <span class="tag">${escapeHtml(session.shellType || 'Unknown shell')}</span>
                <span class="tag">Stable ${escapeHtml(formatStableSeconds(session.stableSeconds))}</span>
                <span class="tag">${escapeHtml(`${draftCount} draft${draftCount === 1 ? '' : 's'}`)}</span>
                <span class="tag">${escapeHtml(getQueueText(session))}</span>
              </div>
              <div class="detail-row">
                <div class="detail-label">Directory</div>
                <div class="detail-value mono">${escapeHtml(session.workingDirectory || '[No working directory]')}</div>
              </div>
              <div class="detail-row">
                <div class="detail-label">Status note</div>
                <div class="detail-value">${escapeHtml(session.statusReason || 'No extra status note is available.')}</div>
              </div>
              <div class="inline-switch">
                <div>
                  <div class="detail-label">Auto Draft Queue</div>
                  <div class="detail-value">${escapeHtml(getQueueText(session))}</div>
                </div>
                <label class="switch" aria-label="Auto Draft Queue">
                  <input type="checkbox" ${session.isAutoDraftQueueEnabled ? 'checked' : ''}>
                  <span class="switch-track"><span class="switch-thumb"></span></span>
                </label>
              </div>
            </div>
          ` : ''}
        `;

        const trigger = item.querySelector('.more-trigger');
        trigger.addEventListener('click', () => handleMoreSessionPress(session.name));

        if (isExpanded) {
          const switchInput = item.querySelector('input[type="checkbox"]');
          switchInput.addEventListener('click', event => event.stopPropagation());
          switchInput.addEventListener('change', async event => {
            event.stopPropagation();
            await toggleAutoDraftForSession(session.name, switchInput.checked);
          });
        }

        moreList.appendChild(item);
      }
    }

    function handleMoreSessionPress(sessionName) {
      const isCurrent = state.selectedSessionName === sessionName;
      const isExpanded = state.expandedSessionName === sessionName;

      if (!isCurrent) {
        state.expandedSessionName = sessionName;
        selectSession(sessionName);
        return;
      }

      state.expandedSessionName = isExpanded ? null : sessionName;
      renderMorePanel();
    }

    function isSwipeProtectedTarget(target) {
      if (!(target instanceof Element)) {
        return false;
      }

      return !!target.closest([
        '#loginView',
        '#moreButton',
        '#morePanel',
        '#commandForm',
        '#draftPanel',
        '#logoutButton',
        'textarea',
        'button',
        'input',
        'label.switch',
        '.switch',
        '.draft-actions',
        '.more-trigger',
        '.more-details'
      ].join(', '));
    }

    function resetSwipeTracking() {
      state.swipeTrackingEnabled = false;
      state.swipeStartX = 0;
      state.swipeStartY = 0;
      state.swipeLastX = 0;
      state.swipeLastY = 0;
      state.swipeAxis = 'none';
    }

    function handleSwipeTouchStart(event) {
      if ((event.touches?.length || 0) > 1) {
        resetSwipeTracking();
        return;
      }

      const touch = event.changedTouches?.[0];
      if (!touch) {
        resetSwipeTracking();
        return;
      }

      state.swipeTrackingEnabled = !isSwipeProtectedTarget(event.target);
      state.swipeStartX = touch.clientX;
      state.swipeStartY = touch.clientY;
      state.swipeLastX = touch.clientX;
      state.swipeLastY = touch.clientY;
      state.swipeAxis = 'none';
    }

    function handleSwipeTouchMove(event) {
      if (!state.swipeTrackingEnabled) {
        return;
      }

      const touch = event.changedTouches?.[0] || event.touches?.[0];
      if (!touch) {
        return;
      }

      state.swipeLastX = touch.clientX;
      state.swipeLastY = touch.clientY;

      const deltaX = touch.clientX - state.swipeStartX;
      const deltaY = touch.clientY - state.swipeStartY;
      const absX = Math.abs(deltaX);
      const absY = Math.abs(deltaY);

      if (state.swipeAxis === 'none' && (absX > 10 || absY > 10)) {
        if (absX > absY * swipeHorizontalDominanceRatio) {
          state.swipeAxis = 'horizontal';
        } else if (absY > absX) {
          state.swipeAxis = 'vertical';
        }
      }

      if (state.swipeAxis === 'horizontal') {
        event.preventDefault();
      }
    }

    function handleSwipeTouchEnd(event) {
      if (!state.swipeTrackingEnabled) {
        resetSwipeTracking();
        return;
      }

      const touch = event.changedTouches?.[0];
      if (!touch) {
        resetSwipeTracking();
        return;
      }

      const deltaX = touch.clientX - state.swipeStartX;
      const deltaY = touch.clientY - state.swipeStartY;
      const absX = Math.abs(deltaX);
      const absY = Math.abs(deltaY);
      const axis = state.swipeAxis;
      resetSwipeTracking();

      if (axis !== 'horizontal' || absX < swipeMinimumDistance || absX <= absY * swipeHorizontalDominanceRatio) {
        return;
      }

      state.lastSwipeTriggeredAt = Date.now();
      selectRelativeSession(deltaX < 0 ? 1 : -1);
    }

    function selectRelativeSession(offset) {
      if (state.sessions.length < 2) {
        return;
      }

      const currentIndex = state.sessions.findIndex(session => session.name === state.selectedSessionName);
      const baseIndex = currentIndex >= 0 ? currentIndex : 0;
      const nextIndex = (baseIndex + offset + state.sessions.length) % state.sessions.length;
      const target = state.sessions[nextIndex];
      if (target) {
        selectSession(target.name);
      }
    }

    async function handleLogin() {
      const password = passwordInput.value.trim();
      if (!password) {
        setLoginMessage('Password is required.', true);
        return;
      }

      loginButton.disabled = true;
      setLoginMessage('Signing in...');

      try {
        const response = await fetch('/remote/auth/login', {
          method: 'POST',
          credentials: 'same-origin',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ password })
        });
        const payload = await response.json();
        if (!response.ok) {
          setLoginMessage(payload.message || 'Login failed.', true);
          return;
        }

        passwordInput.value = '';
        await bootstrap('Session expired. Please login again.');
        showApp();
      } catch (error) {
        setLoginMessage(error.message || 'Login failed.', true);
      } finally {
        loginButton.disabled = false;
      }
    }

    async function handleLogout() {
      if (!window.confirm('Logout from TerminalShell Remote?')) {
        return;
      }

      try {
        await fetch('/remote/auth/logout', { method: 'POST', credentials: 'same-origin' });
      } catch (_) {
      }

      showLogin('Logged out.');
    }

    async function api(path, options = {}, unauthorizedMessage = 'Session expired. Please login again.') {
      const response = await fetch(path, {
        credentials: 'same-origin',
        ...options,
        headers: {
          'Content-Type': 'application/json',
          ...(options.headers || {})
        }
      });

      if (response.status === 401) {
        showLogin(unauthorizedMessage);
        throw new Error('Unauthorized');
      }

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload.message || 'Request failed.');
      }

      return payload;
    }

    async function bootstrap(unauthorizedMessage) {
      const payload = await api('/remote/api/bootstrap', { method: 'GET' }, unauthorizedMessage || 'Session expired. Please login again.');
      state.sessions = payload.sessions || [];
      if (!state.selectedSessionName) {
        state.selectedSessionName = loadRememberedSelectedSessionName();
      }

      if (!state.sessions.length) {
        state.selectedSessionName = null;
        state.selectedSession = null;
        state.expandedSessionName = null;
        renderCurrentHeader();
        renderMorePanel();
        showEmptyState('No terminals available', 'The remote page only shows terminals that are already visible and running in the main window.');
      } else {
        const selectedName = state.selectedSessionName && state.sessions.some(session => session.name === state.selectedSessionName)
          ? state.selectedSessionName
          : state.sessions[0].name;
        selectSession(selectedName);
      }

      connectDashboardSocket();
    }

    function connectDashboardSocket() {
      if (state.dashboardSocket) {
        const socket = state.dashboardSocket;
        state.dashboardSocket = null;
        socket.onclose = null;
        socket.close();
      }

      const wsProtocol = location.protocol === 'https:' ? 'wss' : 'ws';
      const socket = new WebSocket(`${wsProtocol}://${location.host}/remote/ws/dashboard`);
      state.dashboardSocket = socket;

      socket.onmessage = event => {
        const payload = JSON.parse(event.data);
        if (payload.type !== 'dashboard') {
          return;
        }

        state.sessions = payload.sessions || [];
        if (state.expandedSessionName && !state.sessions.some(session => session.name === state.expandedSessionName)) {
          state.expandedSessionName = state.selectedSessionName || state.sessions[0]?.name || null;
        }

        if (state.selectedSessionName && !state.sessions.some(session => session.name === state.selectedSessionName)) {
          state.selectedSessionName = null;
          state.selectedSession = null;
          closeDetailSocket();
          renderCurrentHeader();
          renderMorePanel();
          showEmptyState('Terminal removed', 'The selected terminal is no longer available in the main window.');
        }

        if (!state.selectedSessionName && state.sessions.length > 0) {
          selectSession(state.sessions[0].name);
          return;
        }

        const summary = getSelectedSummary();
        if (summary && state.selectedSessionName === summary.name) {
          state.selectedSession = {
            ...(state.selectedSession || {}),
            ...summary
          };
        }

        renderCurrentHeader();
        renderMorePanel();
        updateMorePulse();
      };

      socket.onclose = () => {
        if (!loginView.classList.contains('hidden')) {
          return;
        }

        window.setTimeout(() => {
          if (!loginView.classList.contains('hidden')) {
            return;
          }

          connectDashboardSocket();
        }, 1500);
      };
    }

    function connectDetailSocket(sessionName) {
      closeDetailSocket();
      if (!sessionName) {
        return;
      }

      showEmptyState('Loading terminal', `Opening live detail for ${sessionName}.`);

      const wsProtocol = location.protocol === 'https:' ? 'wss' : 'ws';
      const socket = new WebSocket(`${wsProtocol}://${location.host}/remote/ws/session/${encodeURIComponent(sessionName)}`);
      state.detailSocket = socket;

      socket.onmessage = event => {
        const payload = JSON.parse(event.data);
        if (payload.type === 'terminalMissing') {
          if (state.selectedSessionName === sessionName) {
            state.selectedSession = null;
            renderCurrentHeader();
            renderMorePanel();
            showEmptyState('Terminal missing', 'The selected terminal is no longer available.');
          }
          return;
        }

        if (payload.type !== 'terminal') {
          return;
        }

        renderTerminal(payload.session);
      };

      socket.onclose = () => {
        if (!state.selectedSessionName || state.selectedSessionName !== sessionName || !loginView.classList.contains('hidden')) {
          return;
        }

        window.setTimeout(() => {
          if (state.selectedSessionName === sessionName && loginView.classList.contains('hidden')) {
            connectDetailSocket(sessionName);
          }
        }, 1500);
      };
    }

    function selectSession(sessionName) {
      if (!sessionName) {
        return;
      }

      const summary = getSessionByName(sessionName);
      const isSameSession = state.selectedSessionName === sessionName;
      state.selectedSessionName = sessionName;
      saveRememberedSelectedSessionName(sessionName);
      state.selectedSession = summary ? { ...(state.selectedSession || {}), ...summary } : null;
      state.expandedSessionName = sessionName;

      renderCurrentHeader();
      renderMorePanel();
      updateMorePulse();

      if (isSameSession && state.detailSocket) {
        return;
      }

      connectDetailSocket(sessionName);
    }

    function renderTerminal(session) {
      if (!session || session.name !== state.selectedSessionName) {
        return;
      }

      mergeSessionSnapshot(session);
      showTerminalView();
      renderCurrentHeader();
      renderMorePanel();
      updateMorePulse();

      renderOutputSnapshot(terminalOutput, session.tailSnapshot || '', session.name, false);
      renderOutputSnapshot(fullscreenTerminalOutput, session.tailSnapshot || '', session.name, true);

      renderDrafts(session);
    }

    function renderDrafts(session) {
      const drafts = Array.isArray(session?.drafts) ? session.drafts : [];
      draftList.innerHTML = '';

      if (!drafts.length) {
        draftPanel.classList.add('hidden');
        return;
      }

      draftPanel.classList.remove('hidden');
      for (const draft of drafts) {
        const item = document.createElement('div');
        item.className = 'draft-item';
        item.innerHTML = `
          <div class="draft-top">
            <div class="draft-preview">${escapeHtml(draft.previewText || '[Draft]')}</div>
            <div class="draft-time">${escapeHtml(formatUtc(draft.updatedAtUtc))}</div>
          </div>
          <div class="draft-text">${escapeHtml(draft.text || '')}</div>
          <div class="draft-actions">
            <button class="btn secondary" type="button" data-action="send">Send Draft</button>
            <button class="btn danger" type="button" data-action="delete">Delete Draft</button>
          </div>
        `;

        item.querySelector('[data-action="send"]').addEventListener('click', async () => {
          if (!confirmDraftAction('Send this draft?', draft)) {
            return;
          }

          try {
            const payload = await api(`/remote/api/sessions/${encodeURIComponent(session.name)}/drafts/${encodeURIComponent(draft.id)}/send`, { method: 'POST' });
            renderTerminal(payload.session);
            setAppNotice('');
          } catch (error) {
            setAppNotice(error.message || 'Failed to send draft.', true);
          }
        });

        item.querySelector('[data-action="delete"]').addEventListener('click', async () => {
          if (!confirmDraftAction('Delete this draft?', draft)) {
            return;
          }

          try {
            const payload = await api(`/remote/api/sessions/${encodeURIComponent(session.name)}/drafts/${encodeURIComponent(draft.id)}`, { method: 'DELETE' });
            renderTerminal(payload.session);
            setAppNotice('');
          } catch (error) {
            setAppNotice(error.message || 'Failed to delete draft.', true);
          }
        });

        draftList.appendChild(item);
      }
    }

    async function toggleAutoDraftForSession(sessionName, enabled) {
      if (!sessionName) {
        return;
      }

      try {
        const payload = await api(`/remote/api/sessions/${encodeURIComponent(sessionName)}/auto-draft`, {
          method: 'POST',
          body: JSON.stringify({ enabled })
        });
        mergeSessionSnapshot(payload.session);
        if (state.selectedSessionName === sessionName) {
          renderTerminal(payload.session);
        } else {
          renderCurrentHeader();
          renderMorePanel();
          updateMorePulse();
        }
        setAppNotice('');
      } catch (error) {
        renderMorePanel();
        setAppNotice(error.message || 'Failed to update auto draft queue.', true);
      }
    }

    async function sendCommand(event) {
      event.preventDefault();
      if (!state.selectedSessionName) {
        return;
      }

      const command = commandInput.value;
      try {
        const payload = await api(`/remote/api/sessions/${encodeURIComponent(state.selectedSessionName)}/send`, {
          method: 'POST',
          body: JSON.stringify({ command })
        });
        commandInput.value = '';
        autoResizeCommandInput();
        renderTerminal(payload.session);
        setAppNotice('');
      } catch (error) {
        setAppNotice(error.message || 'Failed to send command.', true);
      }
    }

    autoResizeCommandInput();
    setLoginMessage('Checking saved login...');
    bootstrap('Login required.')
      .then(() => showApp())
      .catch(error => {
        if (error.message !== 'Unauthorized') {
          showLogin('Login required.');
        }
      });
  </script>
</body>
</html>
""";
}
