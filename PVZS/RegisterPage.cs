namespace PVZS;

internal static class RegisterPage
{
    public static string Render() => $$"""
<!DOCTYPE html>
<html lang="ru">
<head>
<title>Регистрация учителя — Пункт Выдачи Задачек</title>
{{SiteStyles.Head}}
<style>
  .auth-wrap { max-width: 520px; margin: 40px auto 0; }
  .auth-card { padding: 30px 30px 24px; }
  .auth-card h1 { font-size: 28px; margin-bottom: 8px; }
  .auth-card .lead { font-size: 14.5px; margin-bottom: 4px; }
  .auth-card .btn { width: 100%; margin-top: 16px; }
  .auth-alt { margin-top: 16px; font-size: 13.5px; color: var(--muted); }
  .hint { margin: 6px 0 0; font-size: 12.5px; color: var(--muted); }
</style>
</head>
<body>
{{SiteStyles.Nav}}

<section class="wrap auth-wrap">
  <div class="card auth-card reveal">
    <h1 data-i18n="auth.reg.title">Регистрация учителя</h1>
    <p class="lead" data-i18n="auth.reg.lead">ФИО, школа и email — этого достаточно, чтобы получить личный кабинет.</p>
    <form id="registerForm" novalidate>
      <label class="label" for="fullName" data-i18n="auth.name">ФИО</label>
      <input class="input" id="fullName" type="text" autocomplete="name" maxlength="120" placeholder="Иванова Мария Петровна" data-i18n-ph="auth.name.ph">
      <label class="label" for="school" data-i18n="auth.school">Школа или гимназия</label>
      <input class="input" id="school" type="text" autocomplete="organization" maxlength="160" placeholder="Гимназия №1, Минск" data-i18n-ph="auth.school.ph">
      <label class="label" for="username" data-i18n="auth.username">Имя аккаунта</label>
      <input class="input" id="username" type="text" autocomplete="username" maxlength="32" spellcheck="false" placeholder="masha.ivanova" data-i18n-ph="auth.username.ph">
      <p class="hint" data-i18n="auth.username.hint">Латинские буквы, цифры, точка, дефис или подчёркивание — от 3 до 32 символов.</p>
      <label class="label" for="email" data-i18n="auth.email">Email</label>
      <input class="input" id="email" type="email" autocomplete="email" maxlength="190" placeholder="teacher@school.by" data-i18n-ph="auth.email.ph">
      <label class="label" for="password" data-i18n="auth.password">Пароль</label>
      <input class="input" id="password" type="password" autocomplete="new-password" placeholder="Минимум 6 символов" data-i18n-ph="auth.password.ph">
      <div class="form-error" id="error"></div>
      <button class="btn primary" id="submit" type="submit" data-i18n="auth.reg.btn">Создать аккаунт</button>
    </form>
    <p class="auth-alt"><span data-i18n="auth.reg.have">Уже есть аккаунт?</span> <a href="/login" data-i18n="auth.reg.login">Войти</a></p>
  </div>
</section>

{{SiteStyles.Footer}}
{{SiteStyles.Scripts}}
<script>
(() => {
  const form = document.getElementById('registerForm');
  const error = document.getElementById('error');
  const submit = document.getElementById('submit');

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    error.textContent = '';
    const fullName = document.getElementById('fullName').value.trim();
    const school = document.getElementById('school').value.trim();
    const username = document.getElementById('username').value.trim();
    const email = document.getElementById('email').value.trim();
    const password = document.getElementById('password').value;
    if (!fullName || !school || !username || !email || !password) {
      error.textContent = PVZ.t('err.required');
      return;
    }
    submit.disabled = true;
    try {
      const body = await PVZ.api('/api/account/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: username, fullName: fullName, school: school, email: email, password: password }),
      });
      PVZ.setToken(body.accessToken);
      location.href = '/cabinet';
    } catch (e) {
      error.textContent = e.status === 429 ? PVZ.t('auth.err.throttle') : e.message;
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
