namespace PVZS;

internal static class AdminPage
{
    /// <summary>Ночная панель учителя: тёмная тема, одна страница, живые данные.</summary>
    public const string Content = ContentTemplate;

    public static string Render() => ContentTemplate;

    private const string ContentTemplate = """
<!DOCTYPE html>
<html lang="ru">
<head>
<title>Панель учителя — Пункт Выдачи Задачек</title>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="dark">
<style>
  :root {
    --canvas: #0d111b;
    --surface: #151b2a;
    --elevated: #1e2638;
    --border: #37415f;
    --ink: #e2e8f0;
    --muted: #94a3b8;
    --subtle: #64748b;
    --accent: #6366f1;
    --accent-strong: #818cf8;
    --success: #22c55e;
    --warning: #eab308;
    --danger: #ef4444;
    --radius: 14px;
    --shadow: 0 10px 30px rgba(0, 0, 0, .35);
  }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body {
    font-family: "Segoe UI", "Inter", system-ui, -apple-system, sans-serif;
    background: radial-gradient(1200px 600px at 15% -10%, #1e2a4a 0%, transparent 60%),
                radial-gradient(900px 500px at 100% 0%, #2a1e4a 0%, transparent 55%),
                var(--canvas);
    color: var(--ink); min-height: 100vh; line-height: 1.55;
    -webkit-font-smoothing: antialiased;
  }
  a { color: var(--accent-strong); text-decoration: none; }
  a:hover { text-decoration: underline; }
  .wrap { max-width: 1100px; margin: 0 auto; padding: 0 24px; }
  nav.top {
    display: flex; align-items: center; justify-content: space-between;
    padding: 22px 24px; max-width: 1100px; margin: 0 auto;
  }
  nav.top .brand { display: flex; align-items: center; gap: 10px; font-weight: 700; font-size: 17px; color: var(--ink); }
  nav.top .brand .dot {
    width: 10px; height: 10px; border-radius: 3px;
    background: linear-gradient(135deg, var(--accent), var(--accent-strong));
    box-shadow: 0 0 20px rgba(129, 140, 248, .6);
  }
  nav.top .links { display: flex; gap: 18px; font-size: 14px; align-items: center; }
  nav.top .links a { color: var(--muted); }
  nav.top .links a:hover { color: var(--ink); text-decoration: none; }
  nav.top .whoami { color: var(--subtle); font-size: 13px; }
  .btn {
    display: inline-flex; align-items: center; justify-content: center; gap: 8px;
    padding: 10px 18px; border-radius: 10px; border: 1px solid transparent;
    font: inherit; font-weight: 600; font-size: 14px; cursor: pointer; transition: all .15s ease;
  }
  .btn.primary { background: var(--accent); color: #fff; border-color: var(--accent); }
  .btn.primary:hover { background: var(--accent-strong); border-color: var(--accent-strong); }
  .btn.secondary { background: var(--elevated); color: var(--ink); border-color: var(--border); }
  .btn.secondary:hover { background: #2a3450; }
  .btn.danger { background: transparent; color: #fca5a5; border-color: rgba(239, 68, 68, .4); }
  .btn.danger:hover { background: rgba(239, 68, 68, .1); }
  .btn.small { padding: 6px 12px; font-size: 12.5px; border-radius: 8px; }
  .btn:disabled { opacity: .5; cursor: not-allowed; }
  .badge {
    display: inline-flex; align-items: center; padding: 3px 10px; border-radius: 999px;
    font-size: 12px; font-weight: 600; letter-spacing: .2px; white-space: nowrap;
  }
  .badge.accent { background: rgba(99, 102, 241, .15); color: var(--accent-strong); border: 1px solid rgba(99, 102, 241, .35); }
  .badge.gold { background: rgba(234, 179, 8, .13); color: #fde047; border: 1px solid rgba(234, 179, 8, .35); }
  .badge.success { background: rgba(34, 197, 94, .13); color: #86efac; border: 1px solid rgba(34, 197, 94, .35); }
  .badge.muted { background: rgba(148, 163, 184, .12); color: var(--muted); border: 1px solid rgba(148, 163, 184, .3); }
  .card {
    background: linear-gradient(180deg, rgba(30, 38, 56, .9), rgba(21, 27, 42, .9));
    border: 1px solid var(--border); border-radius: var(--radius); box-shadow: var(--shadow);
  }
  h1 { font-size: 30px; letter-spacing: -.01em; margin: 0 0 6px; }
  h2 { font-size: 20px; margin: 0 0 6px; }
  p { color: var(--muted); margin: 0 0 12px; }

  header.page { padding: 34px 0 20px; display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
  header.page .sub { color: var(--muted); font-size: 14.5px; margin: 0; }
  .statusline { display: flex; align-items: center; gap: 8px; font-size: 13px; color: var(--muted); }
  .pulse { width: 9px; height: 9px; border-radius: 50%; background: var(--warning); box-shadow: 0 0 0 0 rgba(234, 179, 8, .5); animation: pulse 2s infinite; }
  .pulse.ok { background: var(--success); box-shadow: 0 0 0 0 rgba(34, 197, 94, .5); }
  .pulse.bad { background: var(--danger); animation: none; }
  @keyframes pulse { 0% { box-shadow: 0 0 0 0 rgba(148, 163, 184, .5); } 70% { box-shadow: 0 0 0 8px rgba(148, 163, 184, 0); } 100% { box-shadow: 0 0 0 0 rgba(148, 163, 184, 0); } }

  .login-card { max-width: 460px; margin: 40px auto; padding: 34px; text-align: center; }
  .login-card h2 { margin-bottom: 10px; }
  .login-card p { font-size: 14px; }
  .field { display: flex; flex-direction: column; gap: 10px; margin-top: 18px; text-align: left; }
  input[type=text], input[type=password] {
    flex: 1; font: inherit; font-size: 15px; padding: 11px 14px;
    border: 1px solid var(--border); border-radius: 10px;
    background: rgba(13, 17, 27, .7); color: var(--ink); outline: none; transition: border-color .15s;
  }
  input[type=text]:focus, input[type=password]:focus { border-color: var(--accent); }
  .error { color: #fca5a5; font-size: 13px; margin-top: 12px; min-height: 18px; }

  .toolbar { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin: 8px 0 14px; }
  .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 14px; margin: 0 0 18px; }
  .stat { padding: 16px 18px; }
  .stat .value { font-size: 26px; font-weight: 700; }
  .stat .label { font-size: 12.5px; color: var(--muted); }

  .table-card { padding: 8px 10px 14px; overflow-x: auto; }
  table { width: 100%; border-collapse: collapse; font-size: 13.5px; min-width: 820px; }
  th, td { text-align: left; padding: 11px 12px; border-bottom: 1px solid rgba(55, 65, 95, .55); vertical-align: top; }
  th { color: var(--muted); font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: .04em; white-space: nowrap; }
  tbody tr:hover { background: rgba(99, 102, 241, .05); }
  td.token { font-family: Consolas, monospace; font-size: 14px; color: var(--ink); }
  td.actions { white-space: nowrap; }
  td.actions .btn { margin-right: 6px; }
  .empty { color: var(--muted); padding: 26px 12px; text-align: center; }
  .students { color: var(--muted); font-size: 13px; max-width: 260px; }
  .students .online { color: var(--success); }
  .expiring { color: var(--warning); }
  .hint { color: var(--subtle); font-size: 12.5px; margin-top: 14px; }
  .keys-card { padding: 22px 24px 18px; margin-top: 20px; overflow-x: auto; }
  .keys-card h2 { margin-bottom: 4px; }
  .keys-card > p { font-size: 13.5px; margin-bottom: 0; }
  .keyform { display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-end; margin: 16px 0 6px; }
  .kf { display: flex; flex-direction: column; gap: 6px; min-width: 150px; }
  .kf label { font-size: 12px; color: var(--muted); text-transform: uppercase; letter-spacing: .04em; }
  .kf select, .kf input {
    font: inherit; font-size: 14px; padding: 9px 12px;
    border: 1px solid var(--border); border-radius: 10px;
    background: rgba(13, 17, 27, .7); color: var(--ink); outline: none;
  }
  .kf select:focus, .kf input:focus { border-color: var(--accent); }
  .newkey {
    margin: 14px 0 4px; padding: 14px 16px; border-radius: 12px;
    border: 1px solid rgba(234, 179, 8, .35); background: rgba(234, 179, 8, .08);
  }
  .newkey-label { font-size: 12.5px; color: var(--muted); }
  .keycode {
    font-family: Consolas, monospace; font-size: 20px; font-weight: 700;
    color: #fde047; letter-spacing: .06em; margin: 6px 0 4px; user-select: all;
  }
  .newkey-meta { font-size: 12.5px; color: var(--muted); }
  .keys-table { margin-top: 14px; min-width: 640px; }
  .build-list { display: grid; gap: 10px; margin-top: 16px; }
  .build-row {
    display: flex; align-items: center; justify-content: space-between; gap: 14px;
    padding: 12px 14px; border: 1px solid var(--border); border-radius: 10px;
    background: rgba(13, 17, 27, .5);
  }
  .build-row b { color: var(--ink); font-size: 14px; }
  .build-row .build-meta { color: var(--muted); font-size: 12.5px; margin-top: 2px; }
  .build-row code { color: var(--accent-strong); font-size: 12.5px; }
  .server-paths { display: grid; gap: 4px; margin-top: 10px; font-size: 12.5px; color: var(--muted); }
  .server-paths code { color: var(--accent-strong); font-family: Consolas, monospace; }
  .exec-output {
    margin-top: 14px; padding: 12px 14px; border-radius: 10px; max-height: 320px; overflow: auto;
    background: rgba(13, 17, 27, .7); border: 1px solid var(--border); color: #cbd5e1;
    font-family: Consolas, monospace; font-size: 12.5px; white-space: pre-wrap; word-break: break-word;
  }
  .keys-card > p code {
    font-family: Consolas, monospace; font-size: 12.5px;
    background: rgba(13, 17, 27, .6); padding: 1px 5px; border-radius: 5px; color: var(--accent-strong);
  }
  footer.site { text-align: center; color: var(--subtle); font-size: 13px; padding: 40px 20px 30px; margin-top: 30px; }
  @media (max-width: 700px) { header.page h1 { font-size: 24px; } }
</style>
</head>
<body>

<nav class="top">
  <a class="brand" href="/"><span class="dot"></span>Пункт Выдачи Задачек</a>
  <div class="links">
    <a href="/">Главная</a>
    <a href="/pricing">Тарифы</a>
    <a href="/cabinet">Кабинет</a>
    <span class="whoami" id="whoami"></span>
  </div>
</nav>

<div class="wrap">
  <header class="page">
    <div>
      <h1>Панель учителя</h1>
      <p class="sub">Каждый задачник хранится в облаке по тарифу. «Продлить» добавляет ещё один срок хранения.</p>
    </div>
    <div class="statusline"><span class="pulse" id="serverPulse"></span><span id="serverStatus">Проверка сервера…</span></div>
  </header>
</div>

<section class="wrap" id="loginView">
  <div class="card login-card">
    <h2>Вход для учителя</h2>
    <p>Введите имя аккаунта и пароль. Администратор видит всех учителей и их задачники, учитель — только свои.</p>
    <div class="field">
      <input id="identifier" type="text" placeholder="Имя аккаунта или email" autocomplete="username">
      <input id="password" type="password" placeholder="Пароль" autocomplete="current-password">
      <button class="btn primary" id="loginBtn">Войти</button>
    </div>
    <div class="error" id="loginError"></div>
    <p class="hint">Нет аккаунта? <a href="/register">Зарегистрируйтесь</a> — это займёт минуту.</p>
  </div>
</section>

<section class="wrap" id="mainView" hidden>
  <div class="cards" id="stats"></div>

  <div class="toolbar">
    <div class="statusline"><span class="pulse ok" id="livePulse"></span><span id="liveStatus">Обновлено только что</span></div>
    <div>
      <button class="btn secondary small" id="refreshBtn">Обновить сейчас</button>
      <button class="btn secondary small" id="logoutBtn">Выйти</button>
    </div>
  </div>

  <div class="card table-card">
    <table>
      <thead>
        <tr>
          <th>Токен</th><th>Учитель</th><th>План</th><th>Загружен</th><th>Истекает</th>
          <th>Задач</th><th>Баллы</th><th>Ученики</th><th></th>
        </tr>
      </thead>
      <tbody id="rows"><tr><td colspan="9" class="empty">Загрузка…</td></tr></tbody>
    </table>
    <div class="error" id="listError" style="padding: 0 12px;"></div>
  </div>
  <p class="hint">Страница обновляется автоматически каждые 5 секунд, пока вкладка открыта. Ученик считается «в сети», если он активен последние 30 секунд.</p>

  <div class="card keys-card" id="teachersCard" hidden>
    <h2>Учителя</h2>
    <p>Все зарегистрированные аккаунты и их активные задачники. Раздел виден только администратору.</p>
    <table class="keys-table">
      <thead>
        <tr><th>Учитель</th><th>Имя аккаунта</th><th>Email</th><th>Школа</th><th>Тариф</th><th>Активные сессии</th></tr>
      </thead>
      <tbody id="teacherRows"><tr><td colspan="6" class="empty">Загрузка…</td></tr></tbody>
    </table>
    <div class="error" id="teacherError"></div>
  </div>

  <div class="card keys-card" id="keysCard" hidden>
    <h2>Ключи активации</h2>
    <p>Ключ передаётся учителю: он вводит код в личном кабинете и получает тариф хранения или подписку на каталог. Оплата вне сервера — ключ и есть подтверждение.</p>
    <div class="keyform">
      <div class="kf">
        <label for="keyKind">Тип ключа</label>
        <select id="keyKind">
          <option value="storage">Тариф хранения</option>
          <option value="subject">Подписка на предмет</option>
          <option value="class">Подписка на класс</option>
          <option value="all">Подписка на все предметы</option>
        </select>
      </div>
      <div class="kf" id="kfPlan">
        <label for="keyPlan">Тариф</label>
        <select id="keyPlan">
          <option value="pro">Про</option>
          <option value="pro_plus">Про+</option>
        </select>
      </div>
      <div class="kf" id="kfSubject" hidden>
        <label for="keySubject">Предмет</label>
        <select id="keySubject">
          <option value="informatics">Информатика</option>
          <option value="english">Английский язык</option>
          <option value="math">Математика</option>
        </select>
      </div>
      <div class="kf" id="kfClass" hidden>
        <label for="keyClass">Класс</label>
        <select id="keyClass"></select>
      </div>
      <div class="kf">
        <label for="keyDays">Срок действия, дней</label>
        <input id="keyDays" type="number" min="1" max="3650" value="30">
      </div>
      <button class="btn primary" id="keyCreateBtn">Создать ключ</button>
    </div>
    <div class="error" id="keyError"></div>
    <div class="newkey" id="newKey" hidden></div>
    <table class="keys-table">
      <thead>
        <tr><th>Код</th><th>Тип</th><th>Что включает</th><th>Дней</th><th>Создан</th><th>Статус</th></tr>
      </thead>
      <tbody id="keyRows"><tr><td colspan="6" class="empty">Ключей пока нет.</td></tr></tbody>
    </table>
  </div>

  <div class="card keys-card" id="buildsCard" hidden>
    <h2>Сборки приложений</h2>
    <p>Скачайте клиент ученика (PVZP), клиент учителя (PVZT) и сам сервер (PVZS). Файлы берутся из папки <code>downloads</code> рядом с сервером (или из переменной <code>PVZ_DOWNLOAD_DIR</code>). Если <code>PVZS.exe</code> рядом нет, администратору отдаётся запущенный сервер.</p>
    <div class="build-list" id="buildRows"><div class="empty">Загрузка…</div></div>
    <div class="error" id="buildError"></div>
  </div>

  <div class="card keys-card" id="serverCard" hidden>
    <h2>Сервер и обновление</h2>
    <p>Версия: <b id="serverVersion">…</b></p>
    <div class="server-paths" id="serverPaths"></div>
    <p style="margin-top:16px"><b>Запустить .exe на сервере.</b> Загрузите файл, при необходимости укажите аргументы. Процесс запускается от имени сервера — используйте только доверенные файлы.</p>
    <div class="keyform">
      <div class="kf"><label for="execFile">Файл .exe</label><input id="execFile" type="file"></div>
      <div class="kf"><label for="execArgs">Аргументы</label><input id="execArgs" type="text" placeholder="--foo bar"></div>
      <button class="btn primary" id="execBtn">Запустить</button>
    </div>
    <p style="margin-top:16px"><b>Обновить с сайта.</b> Скачать файл по ссылке (необязательная проверка SHA-256) и запустить.</p>
    <div class="keyform">
      <div class="kf" style="flex:1"><label for="updateUrl">URL файла</label><input id="updateUrl" type="text" placeholder="https://…/PVZS.exe"></div>
      <div class="kf"><label for="updateSha">SHA-256</label><input id="updateSha" type="text" placeholder="hex (необязательно)"></div>
      <button class="btn secondary" id="updateBtn">Скачать и запустить</button>
    </div>
    <div class="error" id="serverError"></div>
    <pre class="exec-output" id="execOutput" hidden></pre>
  </div>
</section>

<footer class="site">© Пункт Выдачи Задачек · <a href="/pricing">Тарифы</a></footer>

<script>
(() => {
  const $ = (id) => document.getElementById(id);
  let accessToken = sessionStorage.getItem('pvzAccessToken') || localStorage.getItem('pvzAccountToken') || '';
  let isAdmin = false;
  let timer = null;
  let healthTimer = null;

  const fmt = (iso) => {
    if (!iso) return '—';
    const d = new Date(iso);
    return d.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
  };

  const fmtDate = (iso) => new Date(iso).toLocaleDateString('ru-RU');

  const daysLeft = (iso) => {
    const ms = new Date(iso).getTime() - Date.now();
    return Math.max(0, Math.ceil(ms / 86400000));
  };

  const api = async (path, options = {}) => {
    const response = await fetch(path, {
      ...options,
      headers: {
        ...(options.headers || {}),
        ...(accessToken ? { 'Authorization': 'Bearer ' + accessToken } : {}),
      },
    });
    if (response.status === 401) {
      logout('Сессия истекла, войдите снова.');
      throw new Error('Сессия истекла, войдите снова.');
    }
    if (!response.ok) {
      let message = 'Ошибка сервера (' + response.status + ').';
      try {
        const body = await response.json();
        if (body && body.error) message = body.error;
      } catch (_) {}
      throw new Error(message);
    }
    return response;
  };

  const escapeHtml = (value) => String(value).replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[c]));

  const checkHealth = async () => {
    try {
      const response = await fetch('/health', { cache: 'no-store' });
      const ok = response.ok;
      $('serverPulse').className = 'pulse ' + (ok ? 'ok' : 'bad');
      $('serverStatus').textContent = ok ? 'Сервер работает' : 'Сервер не отвечает';
    } catch (_) {
      $('serverPulse').className = 'pulse bad';
      $('serverStatus').textContent = 'Сервер не отвечает';
    }
  };

  const renderStats = (sessions) => {
    const students = sessions.reduce((sum, s) => sum + s.students.length, 0);
    const online = sessions.reduce((sum, s) => sum + s.students.filter((st) => st.isOnline).length, 0);
    const tasks = sessions.reduce((sum, s) => sum + s.totalTasks, 0);
    const stats = [
      { label: 'Активных задачников', value: sessions.length },
      { label: 'Учеников', value: students },
      { label: 'Сейчас в сети', value: online },
      { label: 'Задач на сервере', value: tasks },
    ];
    $('stats').innerHTML = stats.map((s) =>
      '<div class="card stat"><div class="value">' + s.value + '</div><div class="label">' + s.label + '</div></div>'
    ).join('');
  };

  const RETENTION = {
    0: '48 ч',
    1: '1 нед',
    2: '1 мес',
    3: '1 год',
    4: '∞',
    5: '2 нед',
  };
  const RETENTION_TITLE = {
    0: 'Бесплатный тариф: 48 часов',
    1: 'Хранение: 1 неделя',
    2: 'Хранение: 1 месяц',
    3: 'Хранение: 1 год',
    4: 'Постоянное хранение',
    5: 'Бесплатный тариф: 2 недели',
  };

  const planBadge = (retention) => {
    const paid = retention !== 0 && retention !== 5;
    const label = RETENTION[retention] || '2 нед';
    const title = RETENTION_TITLE[retention] || 'Бесплатный тариф';
    return '<span class="badge ' + (paid ? 'gold' : 'muted') + '" title="' + title + '">' + label + '</span>';
  };

  const render = (sessions) => {
    renderStats(sessions);
    const rows = $('rows');
    if (!sessions.length) {
      rows.innerHTML = '<tr><td colspan="9" class="empty">Активных задачников нет. Загрузите задачник из PVZT — он появится здесь.</td></tr>';
      return;
    }
    rows.innerHTML = '';
    for (const s of sessions) {
      const tr = document.createElement('tr');
      const students = s.students.length
        ? s.students.map((st) => {
            const name = escapeHtml(st.lastName + ' ' + st.firstName);
            const score = st.earnedPoints + '/' + st.maximumPoints;
            const mark = st.isOnline ? '<span class="online">●</span> ' : '';
            return mark + name + ' — ' + score;
          }).join('<br>')
        : '—';
      const left = daysLeft(s.expiresAt);
      const expires = fmt(s.expiresAt) + (left <= 2 ? ' <span class="expiring">(скоро)</span>' : '');
      tr.innerHTML =
        '<td class="token">' + escapeHtml(s.tokenHint) + '</td>' +
        '<td>' + (s.ownerName ? escapeHtml(s.ownerName) : '—') + '</td>' +
        '<td>' + planBadge(s.retention) + '</td>' +
        '<td>' + fmt(s.uploadedAt) + '</td>' +
        '<td>' + expires + '</td>' +
        '<td>' + s.totalTasks + '</td>' +
        '<td>' + s.maximumPoints + '</td>' +
        '<td class="students">' + students + '</td>' +
        '<td class="actions">' +
          '<button class="btn secondary small" data-act="extend">Продлить</button>' +
          '<button class="btn secondary small" data-act="download">Скачать</button>' +
          '<button class="btn danger small" data-act="close">Закрыть</button>' +
        '</td>';
      const [extendBtn, downloadBtn, closeBtn] = tr.querySelectorAll('button');
      extendBtn.onclick = () => act(extendBtn, () =>
        api('/api/teacher/sessions/' + s.id + '/extend', { method: 'POST' }).then(load));
      downloadBtn.onclick = () => act(downloadBtn, async () => {
        const response = await api('/api/teacher/sessions/' + s.id + '/download');
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'taskbook.pvz';
        link.click();
        URL.revokeObjectURL(url);
      });
      closeBtn.onclick = () => act(closeBtn, async () => {
        if (!confirm('Закрыть задачник ' + s.tokenHint + '? Ученики потеряют доступ, файл будет удалён.')) return;
        await api('/api/teacher/sessions/' + s.id, { method: 'DELETE' });
        load();
      });
      rows.appendChild(tr);
    }
  };

  const act = async (button, fn) => {
    button.disabled = true;
    try { await fn(); }
    catch (e) { $('listError').textContent = e.message; }
    finally { button.disabled = false; }
  };

  const load = async () => {
    try {
      const response = await api('/api/teacher/sessions');
      $('listError').textContent = '';
      render(await response.json());
      $('liveStatus').textContent = 'Обновлено в ' + new Date().toLocaleTimeString('ru-RU');
    } catch (e) {
      $('listError').textContent = e.message;
    }
  };

  const KIND_LABELS = { storage: 'Тариф хранения', subject: 'Подписка на предмет', class: 'Подписка на класс', all: 'Подписка на все предметы' };
  const PLAN_LABELS = { pro: 'Про', pro_plus: 'Про+' };
  const SUBJECT_LABELS = { informatics: 'Информатика', english: 'Английский язык', math: 'Математика' };
  const SUBJECT_MIN_CLASS = { informatics: 6, english: 3, math: 1 };

  const fillClasses = () => {
    const subject = $('keySubject').value;
    const min = SUBJECT_MIN_CLASS[subject] || 1;
    const select = $('keyClass');
    select.innerHTML = '';
    for (let value = min; value <= 11; value++) {
      const option = document.createElement('option');
      option.value = String(value);
      option.textContent = value + ' класс';
      select.appendChild(option);
    }
  };

  const syncKeyForm = () => {
    const kind = $('keyKind').value;
    $('kfPlan').hidden = kind !== 'storage';
    $('kfSubject').hidden = kind === 'storage' || kind === 'all';
    $('kfClass').hidden = kind !== 'class';
    if (kind === 'subject' || kind === 'class') fillClasses();
  };

  const keyTarget = (key) => {
    if (key.kind === 'storage')
      return 'Тариф «' + (PLAN_LABELS[key.storagePlan] || key.storagePlan) + '»';
    if (key.kind === 'all')
      return 'Все предметы';
    const subject = SUBJECT_LABELS[key.subject] || key.subject;
    return key.kind === 'class'
      ? subject + ' · ' + key.classNumber + ' класс'
      : subject + ' · все классы';
  };

  const loadKeys = async () => {
    try {
      const response = await api('/api/admin/keys');
      const keys = await response.json();
      const rows = $('keyRows');
      if (!keys.length) {
        rows.innerHTML = '<tr><td colspan="6" class="empty">Ключей пока нет.</td></tr>';
        return;
      }
      rows.innerHTML = keys.map((key) =>
        '<tr>' +
          '<td class="token">' + escapeHtml(key.code) + '</td>' +
          '<td>' + escapeHtml(KIND_LABELS[key.kind] || key.kind) + '</td>' +
          '<td>' + escapeHtml(keyTarget(key)) + '</td>' +
          '<td>' + key.durationDays + '</td>' +
          '<td>' + fmt(key.createdAt) + '</td>' +
          '<td>' + (key.used
            ? '<span class="badge muted">Использован</span>'
            : '<span class="badge success">Готов к выдаче</span>') + '</td>' +
        '</tr>').join('');
    } catch (e) {
      $('keyError').textContent = e.message;
    }
  };

  const PLAN_BADGES = {
    pro: '<span class="badge accent">Про</span>',
    pro_plus: '<span class="badge gold">Про+</span>',
    free: '<span class="badge muted">Бесплатный</span>',
  };

  const loadTeachers = async () => {
    try {
      const response = await api('/api/admin/teachers');
      const teachers = await response.json();
      const rows = $('teacherRows');
      if (!teachers.length) {
        rows.innerHTML = '<tr><td colspan="6" class="empty">Учителей пока нет.</td></tr>';
        return;
      }
      rows.innerHTML = teachers.map((t) => {
        const until = t.planUntil ? ' <span class="hint">до ' + fmtDate(t.planUntil) + '</span>' : '';
        const sessions = t.sessions.length
          ? t.sessions.map((s) =>
              escapeHtml(s.tokenHint) + ' · ' + (RETENTION[s.retention] || '—') + ' · ' + s.totalTasks + ' задач'
            ).join('<br>')
          : '—';
        return '<tr>' +
          '<td>' + escapeHtml(t.fullName || '—') + '</td>' +
          '<td class="token">' + escapeHtml(t.username) + '</td>' +
          '<td>' + escapeHtml(t.email) + '</td>' +
          '<td>' + escapeHtml(t.school || '—') + '</td>' +
          '<td>' + (PLAN_BADGES[t.plan] || escapeHtml(t.plan)) + until + '</td>' +
          '<td class="students">' + sessions + '</td>' +
        '</tr>';
      }).join('');
      $('teacherError').textContent = '';
    } catch (e) {
      $('teacherError').textContent = e.message;
    }
  };

  const BUILD_LABELS = { pvzp: 'PVZP — клиент ученика', pvzt: 'PVZT — клиент учителя', pvzs: 'PVZS — сервер' };
  const fmtSize = (bytes) => {
    if (!bytes) return '';
    return bytes >= 1048576
      ? (bytes / 1048576).toFixed(1).replace('.', ',') + ' МБ'
      : Math.max(1, Math.round(bytes / 1024)) + ' КБ';
  };

  const loadBuilds = async () => {
    try {
      const response = await api('/api/admin/downloads');
      const items = await response.json();
      const host = $('buildRows');
      if (!items.length) {
        host.innerHTML = '<div class="empty">Сборок нет.</div>';
        return;
      }
      host.innerHTML = items.map((item) =>
        '<div class="build-row">' +
          '<div><b>' + escapeHtml(BUILD_LABELS[item.id] || item.id) + '</b>' +
          '<div class="build-meta">' + (item.available
            ? escapeHtml(item.fileName) + (fmtSize(item.sizeBytes) ? ' · ' + fmtSize(item.sizeBytes) : '')
            : 'файл не найден') + '</div></div>' +
          (item.available
            ? '<button class="btn secondary small" data-build="' + item.id + '" data-file="' + escapeHtml(item.fileName) + '">Скачать</button>'
            : '<span class="badge muted">недоступно</span>') +
        '</div>').join('');
      host.querySelectorAll('button[data-build]').forEach((button) => {
        button.onclick = async () => {
          button.disabled = true;
          $('buildError').textContent = '';
          try {
            const fileResponse = await api('/api/admin/downloads/' + button.getAttribute('data-build'));
            const blob = await fileResponse.blob();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = button.getAttribute('data-file') || 'pvz.exe';
            link.click();
            URL.revokeObjectURL(url);
          } catch (e) {
            $('buildError').textContent = e.message;
          } finally {
            button.disabled = false;
          }
        };
      });
      $('buildError').textContent = '';
    } catch (e) {
      $('buildError').textContent = e.message;
    }
  };

  const showExec = (result) => {
    const output = $('execOutput');
    output.hidden = false;
    output.textContent = (result.timedOut ? '[прервано по времени]\n' : '') +
      'Код выхода: ' + result.exitCode + '\n\n' + (result.output || '(нет вывода)');
  };

  const loadServer = async () => {
    try {
      const response = await api('/api/admin/server');
      const info = await response.json();
      $('serverVersion').textContent = info.version || '—';
      $('serverPaths').innerHTML =
        '<div>Процесс: <code>' + escapeHtml(info.processPath) + '</code></div>' +
        '<div>Данные: <code>' + escapeHtml(info.dataDirectory) + '</code></div>' +
        '<div>Каталог: <code>' + escapeHtml(info.catalogDirectory) + '</code> · сборников: ' + info.catalogCollections + '</div>' +
        '<div>Сборки: <code>' + escapeHtml(info.downloadsDirectory) + '</code></div>';
      $('serverError').textContent = '';
    } catch (e) {
      $('serverError').textContent = e.message;
    }
  };

  const runExec = async () => {
    $('serverError').textContent = '';
    const input = $('execFile');
    if (!input.files || !input.files.length) {
      $('serverError').textContent = 'Выберите .exe файл.';
      return;
    }
    const form = new FormData();
    form.append('file', input.files[0]);
    form.append('args', $('execArgs').value);
    $('execBtn').disabled = true;
    try {
      const response = await api('/api/admin/exec', { method: 'POST', body: form });
      showExec(await response.json());
    } catch (e) {
      $('serverError').textContent = e.message;
    } finally {
      $('execBtn').disabled = false;
    }
  };

  const runUpdate = async () => {
    $('serverError').textContent = '';
    const url = $('updateUrl').value.trim();
    if (!url) {
      $('serverError').textContent = 'Укажите URL файла.';
      return;
    }
    $('updateBtn').disabled = true;
    try {
      const response = await api('/api/admin/update', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url, sha256: $('updateSha').value.trim() || null, run: true }),
      });
      const body = await response.json();
      showExec(body.run || { exitCode: -1, output: 'Файл скачан: ' + body.path, timedOut: false });
    } catch (e) {
      $('serverError').textContent = e.message;
    } finally {
      $('updateBtn').disabled = false;
    }
  };

  const createKey = async () => {
    $('keyError').textContent = '';
    const kind = $('keyKind').value;
    const request = {
      kind,
      storagePlan: kind === 'storage' ? $('keyPlan').value : null,
      durationDays: Number($('keyDays').value) || 0,
      subject: kind === 'subject' || kind === 'class' ? $('keySubject').value : null,
      classNumber: kind === 'class' ? Number($('keyClass').value) : null,
    };
    $('keyCreateBtn').disabled = true;
    try {
      const response = await api('/api/admin/keys', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });
      const key = await response.json();
      $('newKey').hidden = false;
      $('newKey').innerHTML =
        '<div class="newkey-label">Новый ключ — передайте учителю:</div>' +
        '<div class="keycode">' + escapeHtml(key.code) + '</div>' +
        '<div class="newkey-meta">' + escapeHtml(keyTarget(key)) + ' · действует ' + key.durationDays + ' дней</div>';
      await loadKeys();
    } catch (e) {
      $('keyError').textContent = e.message;
    } finally {
      $('keyCreateBtn').disabled = false;
    }
  };

  const start = async () => {
    try {
      const response = await api('/api/account/me');
      const me = await response.json();
      isAdmin = !!me.isAdmin;
      $('whoami').textContent = (me.username || '') + (me.isAdmin ? ' · администратор' : '');
    } catch (_) {
      return;
    }
    $('loginView').hidden = true;
    $('mainView').hidden = false;
    $('teachersCard').hidden = !isAdmin;
    $('keysCard').hidden = !isAdmin;
    $('buildsCard').hidden = !isAdmin;
    $('serverCard').hidden = !isAdmin;
    load();
    if (isAdmin) {
      loadKeys();
      loadTeachers();
      loadBuilds();
      loadServer();
    }
    if (timer) clearInterval(timer);
    timer = setInterval(() => {
      load();
      if (isAdmin) {
        loadKeys();
        loadTeachers();
        loadBuilds();
      }
    }, 5000);
  };

  const logout = (message) => {
    accessToken = '';
    isAdmin = false;
    sessionStorage.removeItem('pvzAccessToken');
    localStorage.removeItem('pvzAccountToken');
    if (timer) { clearInterval(timer); timer = null; }
    $('mainView').hidden = true;
    $('loginView').hidden = false;
    $('whoami').textContent = '';
    $('teachersCard').hidden = true;
    $('keysCard').hidden = true;
    $('buildsCard').hidden = true;
    $('serverCard').hidden = true;
    $('identifier').value = '';
    $('password').value = '';
    if (message) $('loginError').textContent = message;
  };

  $('loginBtn').onclick = async () => {
    $('loginError').textContent = '';
    $('loginBtn').disabled = true;
    try {
      const response = await fetch('/api/account/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier: $('identifier').value, password: $('password').value }),
      });
      if (!response.ok) {
        let message = 'Ошибка сервера (' + response.status + ').';
        if (response.status === 429) message = 'Слишком много попыток. Подождите минуту.';
        try {
          const body = await response.json();
          if (body && body.error) message = body.error;
        } catch (_) {}
        $('loginError').textContent = message;
        return;
      }
      const body = await response.json();
      accessToken = body.accessToken;
      sessionStorage.setItem('pvzAccessToken', accessToken);
      localStorage.setItem('pvzAccountToken', accessToken);
      $('password').value = '';
      await start();
    } catch (e) {
      $('loginError').textContent = e.message;
    } finally {
      $('loginBtn').disabled = false;
    }
  };

  $('identifier').addEventListener('keydown', (e) => {
    if (e.key === 'Enter') $('loginBtn').click();
  });
  $('password').addEventListener('keydown', (e) => {
    if (e.key === 'Enter') $('loginBtn').click();
  });
  $('logoutBtn').onclick = () => logout('');
  $('refreshBtn').onclick = () => load();
  $('keyKind').addEventListener('change', syncKeyForm);
  $('keySubject').addEventListener('change', fillClasses);
  $('keyCreateBtn').onclick = createKey;
  $('execBtn').onclick = runExec;
  $('updateBtn').onclick = runUpdate;
  syncKeyForm();
  fillClasses();

  checkHealth();
  healthTimer = setInterval(checkHealth, 30000);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') checkHealth();
  });

  if (accessToken) start();
})();
</script>
</body>
</html>
""";
}
