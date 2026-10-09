namespace PVZS;

internal static class CabinetPage
{
    public static string Render() => $$"""
<!DOCTYPE html>
<html lang="ru">
<head>
<title>Личный кабинет — Пункт Выдачи Задачек</title>
{{SiteStyles.Head}}
<style>
  .cabinet-head {
    display: flex; align-items: flex-end; justify-content: space-between;
    gap: 16px; flex-wrap: wrap; padding: 32px 0 6px;
  }
  .cabinet-head h1 { font-size: 30px; margin-bottom: 6px; }
  .cabinet-head .lead { margin: 0; font-size: 14.5px; }
  .head-side { display: flex; align-items: center; gap: 12px; }
  .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 18px; margin-top: 18px; }
  .col { display: grid; gap: 18px; align-content: start; }
  .panel { padding: 22px 22px 20px; }
  .panel h2 { font-size: 19px; margin-bottom: 10px; }
  .panel .btn { margin-top: 12px; }
  .who { margin-bottom: 8px; }
  .who .name { display: block; font-weight: 700; }
  .who .line { display: block; color: var(--muted); font-size: 13.5px; }
  .plan-line { min-height: 0; }
  .plan-line .line { display: block; color: var(--muted); font-size: 13.5px; margin-bottom: 8px; }
  .buckets, .subs { display: grid; gap: 8px; }
  .bucket, .sub {
    display: flex; justify-content: space-between; align-items: center; gap: 10px;
    padding: 10px 12px; border: 1px solid var(--border); border-radius: 10px;
    background: rgba(13, 17, 27, .5); font-size: 14px;
  }
  .keycode-input { font-family: Consolas, monospace; letter-spacing: .06em; text-transform: uppercase; }
  .upload-grid { display: grid; grid-template-columns: 2fr 1.4fr auto; gap: 12px; align-items: end; }
  .upload-grid .label { margin-top: 0; }
  input[type=file].input { padding: 8px 12px; }
  input[type=file].input::file-selector-button {
    font: inherit; font-size: 13px; margin-right: 10px; padding: 4px 10px;
    border-radius: 8px; border: 1px solid var(--border);
    background: var(--elevated); color: var(--ink); cursor: pointer;
  }
  .file-row {
    display: grid; grid-template-columns: 1.2fr 1.8fr auto; gap: 14px;
    align-items: center; padding: 12px 2px; border-bottom: 1px solid rgba(55, 65, 95, .55);
  }
  .file-row:last-child { border-bottom: 0; }
  .token-chip { font-family: Consolas, monospace; font-weight: 700; letter-spacing: .05em; }
  .file-meta { display: flex; flex-wrap: wrap; gap: 6px 14px; color: var(--muted); font-size: 13px; }
  .file-meta b { color: var(--ink); font-variant-numeric: tabular-nums; }
  .file-actions { display: flex; gap: 8px; justify-content: flex-end; }
  .empty { color: var(--muted); padding: 16px 2px; }
  .guest-card { max-width: 540px; margin: 50px auto; padding: 32px; text-align: center; }
  .guest-card h1 { font-size: 28px; }
  .guest-card .ctas { display: flex; gap: 10px; justify-content: center; margin-top: 18px; }
  @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
  @media (max-width: 760px) {
    .upload-grid { grid-template-columns: 1fr; }
    .file-row { grid-template-columns: 1fr; }
    .file-actions { justify-content: flex-start; }
  }
</style>
</head>
<body>
{{SiteStyles.Nav}}

<section class="wrap" id="guestView">
  <div class="card guest-card reveal">
    <h1 data-i18n="cabinet.guest.title">Войдите в кабинет</h1>
    <p data-i18n="cabinet.guest.desc">Кабинет хранит задачники в облаке и активирует ключи. Войдите или зарегистрируйтесь.</p>
    <div class="ctas">
      <a class="btn primary" href="/login" data-i18n="cabinet.guest.login">Войти</a>
      <a class="btn secondary" href="/register" data-i18n="cabinet.guest.reg">Регистрация</a>
    </div>
  </div>
</section>

<section class="wrap" id="cabinetView" hidden>
  <header class="cabinet-head reveal">
    <div>
      <h1 data-i18n="cabinet.title">Личный кабинет</h1>
      <p class="lead" data-i18n="cabinet.lead">Профиль, тарифы и файлы в облаке с таймером удаления.</p>
    </div>
    <div class="head-side">
      <span class="badge accent" id="planBadge">…</span>
      <button class="btn secondary small" id="logoutBtn" data-i18n="auth.logout">Выйти</button>
    </div>
  </header>

  <div class="grid">
    <div class="col">
      <div class="card panel reveal">
        <h2 data-i18n="cabinet.profile">Профиль</h2>
        <div class="who" id="who"></div>
        <label class="label" for="fullName" data-i18n="auth.name">ФИО</label>
        <input class="input" id="fullName" type="text" maxlength="120" autocomplete="name">
        <label class="label" for="school" data-i18n="auth.school">Школа или гимназия</label>
        <input class="input" id="school" type="text" maxlength="160" autocomplete="organization">
        <div class="form-ok" id="profileOk"></div>
        <div class="form-error" id="profileError"></div>
        <button class="btn primary" id="saveProfileBtn" data-i18n="cabinet.profile.save">Сохранить</button>
      </div>

      <div class="card panel reveal">
        <h2 data-i18n="cabinet.pwd">Смена пароля</h2>
        <label class="label" for="currentPassword" data-i18n="cabinet.pwd.current">Текущий пароль</label>
        <input class="input" id="currentPassword" type="password" autocomplete="current-password">
        <label class="label" for="newPassword" data-i18n="cabinet.pwd.new">Новый пароль</label>
        <input class="input" id="newPassword" type="password" autocomplete="new-password">
        <div class="form-ok" id="pwdOk"></div>
        <div class="form-error" id="pwdError"></div>
        <button class="btn secondary" id="savePwdBtn" data-i18n="cabinet.pwd.save">Обновить пароль</button>
      </div>

      <div class="card panel reveal">
        <h2 data-i18n="cabinet.key">Ключ активации</h2>
        <input class="input keycode-input" id="keyCode" type="text" placeholder="PVZ-XXXX-XXXX-XXXX" data-i18n-ph="cabinet.key.ph" spellcheck="false" autocomplete="off">
        <div class="form-ok" id="keyOk"></div>
        <div class="form-error" id="keyError"></div>
        <button class="btn primary" id="activateBtn" data-i18n="cabinet.key.btn">Активировать</button>
      </div>
    </div>

    <div class="col">
      <div class="card panel reveal">
        <h2 data-i18n="cabinet.storage">Хранилище</h2>
        <div class="plan-line" id="planLine"></div>
        <div class="buckets" id="buckets"></div>
      </div>

      <div class="card panel reveal">
        <h2 data-i18n="cabinet.subs">Подписки на каталог</h2>
        <div class="subs" id="subs"></div>
      </div>
    </div>
  </div>

  <div class="card panel reveal" style="margin-top: 18px;">
    <h2 data-i18n="cabinet.upload">Загрузить задачник</h2>
    <div class="upload-grid">
      <div>
        <label class="label" for="uploadFile">.pvz</label>
        <input class="input" id="uploadFile" type="file" accept=".pvz,application/octet-stream">
      </div>
      <div>
        <label class="label" for="uploadRetention" data-i18n="cabinet.upload.term">Срок хранения</label>
        <select class="input" id="uploadRetention"></select>
      </div>
      <button class="btn primary" id="uploadBtn" data-i18n="cabinet.upload.btn">Загрузить в облако</button>
    </div>
    <div class="form-ok" id="uploadOk"></div>
    <div class="form-error" id="uploadError"></div>
  </div>

  <div class="card panel reveal" style="margin-top: 18px;">
    <h2 data-i18n="cabinet.files">Файлы в облаке</h2>
    <div id="files"></div>
    <div class="form-error" id="filesError"></div>
  </div>
</section>

{{SiteStyles.Footer}}
{{SiteStyles.Scripts}}
<script>
(() => {
  const $ = (id) => document.getElementById(id);
  const ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
  const esc = (value) => String(value).replace(/[&<>"']/g, (c) => ESC[c]);
  const RETENTION_NAMES = { 0: 'Hours48', 1: 'Week1', 2: 'Month1', 3: 'Year1', 4: 'Forever', 5: 'TwoWeeks' };

  const guestView = $('guestView');
  const cabinetView = $('cabinetView');
  const filesEl = $('files');
  const uploadRetention = $('uploadRetention');
  let profile = null;
  let files = [];

  const fmtDate = (iso) => new Date(iso).toLocaleString(PVZ.locale(), {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  });

  const fmtLeft = (ms) => {
    if (ms <= 0) return '—';
    const total = Math.floor(ms / 1000);
    const days = Math.floor(total / 86400);
    const hours = Math.floor((total % 86400) / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    if (days > 0) return days + ' ' + PVZ.t('unit.d') + ' ' + hours + ' ' + PVZ.t('unit.h');
    if (hours > 0) return hours + ' ' + PVZ.t('unit.h') + ' ' + minutes + ' ' + PVZ.t('unit.min');
    if (minutes > 0) return minutes + ' ' + PVZ.t('unit.min');
    return PVZ.t('unit.lessMin');
  };

  const planName = () => {
    if (!profile) return '';
    if (profile.plan === 'free') return PVZ.t('plan.free');
    if (profile.plan === 'pro') return PVZ.t('plan.pro');
    if (profile.plan === 'pro_plus') return PVZ.t('plan.plus');
    return profile.planDisplayName;
  };

  const bucketCapacity = (bucket) => bucket.limit === -1
    ? bucket.used + ' / ' + PVZ.t('cabinet.storage.unlimited')
    : bucket.used + ' / ' + bucket.limit;

  const renderProfile = () => {
    if (!profile) return;
    $('planBadge').textContent = planName();
    $('who').innerHTML =
      '<span class="name">' + esc(profile.fullName) + '</span>' +
      '<span class="line">' + esc(profile.email) + '</span>' +
      '<span class="line">' + esc(PVZ.t('cabinet.username')) + ': ' + esc(profile.username) + '</span>' +
      '<span class="line">' + esc(profile.school) + '</span>';
    $('fullName').value = profile.fullName;
    $('school').value = profile.school;
    $('planLine').innerHTML = profile.planUntil
      ? '<span class="line">' + PVZ.t('plan.until') + ' ' + fmtDate(profile.planUntil) + '</span>'
      : '';
    $('buckets').innerHTML = profile.storage.map((bucket) =>
      '<div class="bucket"><b>' + esc(PVZ.retentionLabel(bucket.retention)) + '</b>' +
      '<span class="muted">' + esc(bucketCapacity(bucket)) + '</span></div>').join('');
    $('subs').innerHTML = profile.subscriptions.length
      ? profile.subscriptions.map((sub) => {
          const what = sub.kind === 'all'
            ? esc(PVZ.t('cabinet.sub.allSubjects'))
            : sub.classNumber
              ? esc(PVZ.subjectName(sub.subject)) + ' · ' + sub.classNumber + ' ' + (PVZ.lang() === 'en' ? 'grade' : 'класс')
              : esc(PVZ.subjectName(sub.subject)) + ' · ' + PVZ.t('cabinet.sub.allClasses');
          return '<div class="sub"><b>' + what + '</b><span class="muted">' +
            PVZ.t('cabinet.sub.until') + ' ' + fmtDate(sub.expiresAt) + '</span></div>';
        }).join('')
      : '<div class="empty">' + esc(PVZ.t('cabinet.subs.none')) + '</div>';
  };

  const renderRetentionSelect = () => {
    if (!profile) return;
    const previous = uploadRetention.value;
    uploadRetention.innerHTML = profile.storage.map((bucket) =>
      '<option value="' + RETENTION_NAMES[bucket.retention] + '">' +
      esc(PVZ.retentionLabel(bucket.retention)) + ' · ' + esc(bucketCapacity(bucket)) + '</option>').join('');
    if (previous && uploadRetention.querySelector('option[value="' + previous + '"]'))
      uploadRetention.value = previous;
  };

  const tick = () => {
    const now = Date.now();
    document.querySelectorAll('[data-countdown]').forEach((element) => {
      element.textContent = fmtLeft(new Date(element.getAttribute('data-countdown')).getTime() - now);
    });
  };

  const renderFiles = () => {
    if (!files.length) {
      filesEl.innerHTML = '<div class="empty">' + esc(PVZ.t('cabinet.files.none')) + '</div>';
      return;
    }
    filesEl.innerHTML = files.map((file) =>
      '<div class="file-row">' +
        '<div><span class="token-chip">' + esc(file.tokenHint) + '</span> ' +
        '<span class="badge muted">' + esc(PVZ.retentionLabel(file.retention)) + '</span></div>' +
        '<div class="file-meta">' +
          '<span>' + PVZ.t('cabinet.files.uploaded') + ': ' + fmtDate(file.uploadedAt) + '</span>' +
          '<span>' + PVZ.t('cabinet.files.expires') + ': <b data-countdown="' + esc(file.expiresAt) + '">—</b></span>' +
          '<span>' + PVZ.t('cabinet.files.tasks') + ': ' + file.totalTasks + '</span>' +
          '<span>' + PVZ.t('cabinet.files.students') + ': ' + file.studentsCount + '</span>' +
        '</div>' +
        '<div class="file-actions">' +
          '<button class="btn secondary small" data-act="download" data-id="' + esc(file.id) + '">' +
            esc(PVZ.t('cabinet.files.download')) + '</button>' +
          '<button class="btn danger small" data-act="delete" data-id="' + esc(file.id) + '">' +
            esc(PVZ.t('cabinet.files.delete')) + '</button>' +
        '</div>' +
      '</div>').join('');
    tick();
  };

  const renderAll = () => {
    renderProfile();
    renderRetentionSelect();
    renderFiles();
  };

  const refresh = async () => {
    profile = await PVZ.api('/api/account/me');
    renderProfile();
    renderRetentionSelect();
    files = await PVZ.api('/api/account/files');
    renderFiles();
  };

  const showGuest = () => {
    cabinetView.hidden = true;
    guestView.hidden = false;
  };

  const showCabinet = () => {
    guestView.hidden = true;
    cabinetView.hidden = false;
    PVZ.reveal();
  };

  const boot = async () => {
    if (!PVZ.getToken()) {
      showGuest();
      return;
    }
    showCabinet();
    try {
      await refresh();
    } catch (error) {
      if (error.status === 401) {
        PVZ.setToken('');
        showGuest();
      } else {
        $('filesError').textContent = error.message;
      }
    }
  };

  const downloadFile = async (id) => {
    $('filesError').textContent = '';
    try {
      const response = await PVZ.api('/api/account/files/' + id + '/download');
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'taskbook.pvz';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      $('filesError').textContent = error.message;
    }
  };

  const deleteFile = async (id) => {
    if (!confirm(PVZ.t('cabinet.files.confirm'))) return;
    $('filesError').textContent = '';
    try {
      await PVZ.api('/api/account/files/' + id, { method: 'DELETE' });
      await refresh();
    } catch (error) {
      $('filesError').textContent = error.message;
    }
  };

  filesEl.addEventListener('click', (event) => {
    const button = event.target.closest('button[data-act]');
    if (!button) return;
    const id = button.getAttribute('data-id');
    if (button.getAttribute('data-act') === 'download') downloadFile(id);
    else deleteFile(id);
  });

  $('saveProfileBtn').addEventListener('click', async () => {
    $('profileOk').textContent = '';
    $('profileError').textContent = '';
    try {
      profile = await PVZ.api('/api/account/me', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fullName: $('fullName').value, school: $('school').value }),
      });
      $('profileOk').textContent = PVZ.t('cabinet.profile.saved');
      renderProfile();
    } catch (error) {
      $('profileError').textContent = error.message;
    }
  });

  $('savePwdBtn').addEventListener('click', async () => {
    $('pwdOk').textContent = '';
    $('pwdError').textContent = '';
    try {
      await PVZ.api('/api/account/me/password', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          currentPassword: $('currentPassword').value,
          newPassword: $('newPassword').value,
        }),
      });
      $('pwdOk').textContent = PVZ.t('cabinet.pwd.saved');
      $('currentPassword').value = '';
      $('newPassword').value = '';
    } catch (error) {
      $('pwdError').textContent = error.message;
    }
  });

  $('activateBtn').addEventListener('click', async () => {
    $('keyOk').textContent = '';
    $('keyError').textContent = '';
    const code = $('keyCode').value.trim();
    if (!code) {
      $('keyError').textContent = PVZ.t('err.required');
      return;
    }
    $('activateBtn').disabled = true;
    try {
      const result = await PVZ.api('/api/account/keys', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: code }),
      });
      $('keyOk').textContent = result.message;
      $('keyCode').value = '';
      profile = result.profile;
      renderProfile();
      renderRetentionSelect();
      files = await PVZ.api('/api/account/files');
      renderFiles();
    } catch (error) {
      $('keyError').textContent = error.message;
    } finally {
      $('activateBtn').disabled = false;
    }
  });

  $('keyCode').addEventListener('keydown', (event) => {
    if (event.key === 'Enter') $('activateBtn').click();
  });

  $('uploadBtn').addEventListener('click', async () => {
    $('uploadOk').textContent = '';
    $('uploadError').textContent = '';
    const input = $('uploadFile');
    if (!input.files.length) {
      $('uploadError').textContent = PVZ.t('cabinet.upload.noFile');
      return;
    }
    const form = new FormData();
    form.append('file', input.files[0]);
    form.append('retention', uploadRetention.value);
    $('uploadBtn').disabled = true;
    try {
      const receipt = await PVZ.api('/api/account/files', { method: 'POST', body: form });
      $('uploadOk').textContent = PVZ.t('cabinet.upload.ok') + receipt.token;
      input.value = '';
      await refresh();
    } catch (error) {
      $('uploadError').textContent = error.message;
    } finally {
      $('uploadBtn').disabled = false;
    }
  });

  $('logoutBtn').addEventListener('click', async () => {
    try { await PVZ.api('/api/account/logout', { method: 'POST' }); }
    catch (_) { }
    PVZ.setToken('');
    showGuest();
  });

  PVZ.onChange(() => {
    if (profile) renderAll();
  });

  setInterval(tick, 1000);
  boot();
})();
</script>
</body>
</html>
""";
}
