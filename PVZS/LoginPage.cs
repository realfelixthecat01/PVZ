namespace PVZS;

internal static class LoginPage
{
    public static string Render() => $$"""
<!DOCTYPE html>
<html lang="ru">
<head>
<title>Вход — Пункт Выдачи Задачек</title>
{{SiteStyles.Head}}
<style>
  .auth-wrap { max-width: 480px; margin: 40px auto 0; }
  .auth-card { padding: 30px 30px 24px; }
  .auth-card h1 { font-size: 28px; margin-bottom: 8px; }
  .auth-card .lead { font-size: 14.5px; margin-bottom: 4px; }
  .auth-card .btn { width: 100%; margin-top: 16px; }
  .auth-alt { margin-top: 16px; font-size: 13.5px; color: var(--muted); }
</style>
</head>
<body>
{{SiteStyles.Nav}}

<section class="wrap auth-wrap">
  <div class="card auth-card reveal">
    <h1 data-i18n="auth.login.title">Вход для учителя</h1>
    <p class="lead" data-i18n="auth.login.lead">Вход по имени аккаунта и паролю. Кабинет хранит задачники в облаке и включает тарифы по ключам активации.</p>
    <form id="loginForm" novalidate>
      <label class="label" for="identifier" data-i18n="auth.username">Имя аккаунта</label>
      <input class="input" id="identifier" type="text" autocomplete="username" placeholder="masha.ivanova или teacher@school.by" data-i18n-ph="auth.login.ph">
      <label class="label" for="password" data-i18n="auth.password">Пароль</label>
      <input class="input" id="password" type="password" autocomplete="current-password" placeholder="Минимум 6 символов" data-i18n-ph="auth.password.ph">
      <div class="form-error" id="error"></div>
      <button class="btn primary" id="submit" type="submit" data-i18n="auth.login.btn">Войти</button>
    </form>
    <p class="auth-alt"><span data-i18n="auth.login.no">Нет аккаунта?</span> <a href="/register" data-i18n="auth.login.reg">Зарегистрироваться</a></p>
  </div>
</section>

{{SiteStyles.Footer}}
{{SiteStyles.Scripts}}
<script>
(() => {
  const form = document.getElementById('loginForm');
  const error = document.getElementById('error');
  const submit = document.getElementById('submit');

  const goToCabinet = () => { location.href = '/cabinet'; };

  if (PVZ.getToken()) {
    PVZ.api('/api/account/me').then(goToCabinet).catch(() => PVZ.setToken(''));
  }

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    error.textContent = '';
    const identifier = document.getElementById('identifier').value.trim();
    const password = document.getElementById('password').value;
    if (!identifier || !password) {
      error.textContent = PVZ.t('err.required');
      return;
    }
    submit.disabled = true;
    try {
      const body = await PVZ.api('/api/account/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier: identifier, password: password }),
      });
      PVZ.setToken(body.accessToken);
      goToCabinet();
    } catch (e) {
      error.textContent = e.status === 401 ? PVZ.t('auth.err.login')
        : e.status === 429 ? PVZ.t('auth.err.throttle')
        : e.message;
    } finally {
      submit.disabled = false;
    }
  });
})();
</script>
</body>
</html>
""";
}
