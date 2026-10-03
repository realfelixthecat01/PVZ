namespace PVZ.Server;

internal static class AdminPage
{
    public const string Content = """
        <!doctype html>
        <html lang="ru">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Пункт Выдачи Задачек — учитель</title>
          <style>
            :root { color-scheme: dark; font: 16px system-ui, sans-serif; background: #111827; color: #e2e8f0; }
            body { max-width: 1100px; margin: 32px auto; padding: 0 20px; }
            section { margin: 18px 0; padding: 20px; border: 1px solid #475569; border-radius: 12px; background: #1f2937; }
            input, button { padding: 10px; border-radius: 6px; border: 1px solid #64748b; }
            input { color: #e2e8f0; background: #182231; }
            button { color: white; background: #2563eb; cursor: pointer; }
            button.secondary { background: #334155; }
            label { display: inline-flex; gap: 8px; align-items: center; margin: 6px 8px 6px 0; }
            table { width: 100%; border-collapse: collapse; margin-top: 14px; }
            th, td { text-align: left; padding: 10px; border-bottom: 1px solid #475569; }
            .status { min-height: 24px; color: #fbbf24; white-space: pre-wrap; }
            .active { color: #86efac; } .inactive { color: #fca5a5; }
            code { overflow-wrap: anywhere; }
            .warning { color: #fbbf24; }
          </style>
        </head>
        <body>
          <h1>Пункт Выдачи Задачек</h1>
          <p>Личный кабинет учителя: задачники и прохождения видны только владельцу API-ключа.</p>
          <section>
            <h2>Регистрация учителя</h2>
            <label>Имя <input id="teacher-name" maxlength="80" autocomplete="name"></label>
            <button id="register">Создать аккаунт учителя</button>
            <p id="registration-result" class="status"></p>
          </section>
          <section>
            <label>Личный API-ключ (8 символов) <input id="key" type="password" maxlength="8" autocomplete="off"></label>
            <button id="refresh" class="secondary">Обновить кабинет</button>
            <p id="status" class="status">Зарегистрируйтесь или введите личный ключ.</p>
            <p class="warning">Ключ показывается один раз при регистрации. Не сообщайте его ученикам и не используйте вместо пароля панели преподавателя.</p>
          </section>
          <section>
            <h2>Загрузить задачник</h2>
            <input id="file" type="file" accept=".pvz">
            <button id="upload">Отправить .pvz</button>
            <p>Токен ученика после загрузки: <code id="new-token"></code></p>
          </section>
          <section>
            <h2>Мои задачники</h2>
            <table><thead><tr><th>Подсказка токена</th><th>Загружен</th><th>Истекает</th><th>Статус</th><th></th></tr></thead><tbody id="books"></tbody></table>
          </section>
          <section>
            <h2>Прохождения учеников</h2>
            <p>Данные обновляются каждые 3 секунды. Статус «в сети» определяется по последнему сигналу от приложения ученика.</p>
            <p class="warning">Баллы отправляет приложение ученика; без серверного выполнения решений они не защищены от подмены.</p>
            <table><thead><tr><th>Ученик</th><th>Класс</th><th>Баллы</th><th>Задачи</th><th>Время</th><th>Статус</th></tr></thead><tbody id="students"></tbody></table>
          </section>
          <script>
            const keyInput = document.getElementById('key');
            const status = document.getElementById('status');
            const authHeaders = () => ({ Authorization: 'Bearer ' + keyInput.value.trim() });
            async function responseError(response) {
              if (response.status === 401 || response.status === 403)
                return new Error('Введите корректный личный API-ключ учителя из 8 символов.');
              let details = '';
              const contentType = response.headers.get('content-type') || '';
              if (contentType.includes('application/json')) {
                try {
                  const body = await response.json();
                  details = typeof body.error === 'string' ? body.error : '';
                } catch { }
              } else {
                try { details = (await response.text()).trim(); } catch { }
              }
              return new Error(details || `Ошибка сервера (${response.status}).`);
            }
            async function requireSuccess(response) {
              if (!response.ok) throw await responseError(response);
            }
            function cell(row, value, className = '') {
              const item = document.createElement('td');
              item.textContent = value;
              item.className = className;
              row.append(item);
            }
            async function refresh() {
              if (!keyInput.value.trim()) { status.textContent = 'Сначала введите личный API-ключ учителя.'; return; }
              status.textContent = 'Синхронизация кабинета...';
              try {
                const [bookResponse, sessionsResponse] = await Promise.all([
                  fetch('/api/teacher/taskbooks', { headers: authHeaders() }),
                  fetch('/api/teacher/sessions', { headers: authHeaders() })
                ]);
                await requireSuccess(bookResponse);
                await requireSuccess(sessionsResponse);
                const [books, sessions] = await Promise.all([bookResponse.json(), sessionsResponse.json()]);
                const bookRows = document.getElementById('books');
                bookRows.replaceChildren();
                for (const item of books) {
                  const row = document.createElement('tr');
                  cell(row, item.tokenHint);
                  cell(row, new Date(item.uploadedAt).toLocaleString());
                  cell(row, new Date(item.expiresAt).toLocaleString());
                  cell(row, item.isActive ? 'Активен' : 'Истёк / отозван', item.isActive ? 'active' : 'inactive');
                  const action = document.createElement('td');
                  if (item.isActive) {
                    const button = document.createElement('button');
                    button.textContent = 'Отозвать';
                    button.className = 'secondary';
                    button.addEventListener('click', async () => {
                      try {
                        const response = await fetch(`/api/teacher/taskbooks/${encodeURIComponent(item.id)}`, {
                          method: 'DELETE', headers: authHeaders()
                        });
                        await requireSuccess(response);
                        await refresh();
                      } catch (error) { status.textContent = error.message; }
                    });
                    action.append(button);
                  }
                  row.append(action);
                  bookRows.append(row);
                }
                const studentRows = document.getElementById('students');
                studentRows.replaceChildren();
                for (const item of sessions) {
                  const row = document.createElement('tr');
                  cell(row, `${item.firstName} ${item.lastName}`);
                  cell(row, item.className);
                  cell(row, `${item.earnedPoints}/${item.maximumPoints}`);
                  cell(row, `${item.completedTasks}/${item.totalTasks}`);
                  cell(row, formatDuration(item.elapsedSeconds));
                  cell(row, item.isOnline ? 'В сети' : 'Не в сети', item.isOnline ? 'active' : 'inactive');
                  studentRows.append(row);
                }
                status.textContent = `Мои задачники: ${books.length}; прохождения: ${sessions.length}. Обновлено ${new Date().toLocaleTimeString()}.`;
              } catch (error) { status.textContent = error.message || 'Не удалось обновить кабинет.'; }
            }
            function formatDuration(seconds) {
              const total = Math.max(0, Math.floor(seconds));
              return `${Math.floor(total / 60)} мин ${total % 60} сек`;
            }
            document.getElementById('refresh').addEventListener('click', refresh);
            document.getElementById('register').addEventListener('click', async () => {
              const name = document.getElementById('teacher-name').value.trim();
              const result = document.getElementById('registration-result');
              result.textContent = '';
              try {
                const response = await fetch('/api/teachers/register', {
                  method: 'POST', headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ displayName: name })
                });
                await requireSuccess(response);
                const account = await response.json();
                keyInput.value = account.apiKey;
                keyInput.type = 'text';
                result.textContent = `Сохраните личный ключ сейчас — повторно он не показывается: ${account.apiKey}`;
                await refresh();
              } catch (error) { result.textContent = error.message || 'Не удалось создать аккаунт.'; }
            });
            document.getElementById('upload').addEventListener('click', async () => {
              const file = document.getElementById('file').files[0];
              if (!file) { status.textContent = 'Сначала выберите файл .pvz.'; return; }
              document.getElementById('new-token').textContent = '';
              status.textContent = 'Загрузка задачника...';
              try {
                const response = await fetch('/api/teacher/taskbooks', {
                  method: 'POST',
                  headers: { ...authHeaders(), 'Content-Type': 'application/octet-stream' },
                  body: file
                });
                await requireSuccess(response);
                const result = await response.json();
                document.getElementById('new-token').textContent = result.token;
                status.textContent = 'Задачник загружен. Передайте ученику только этот токен.';
                await refresh();
              } catch (error) { status.textContent = error.message || 'Не удалось загрузить задачник.'; }
            });
            setInterval(() => {
              if (keyInput.value.trim()) refresh();
            }, 3000);
          </script>
        </body>
        </html>
        """;
}
