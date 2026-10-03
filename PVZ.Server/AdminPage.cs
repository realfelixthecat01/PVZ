namespace PVZ.Server;

internal static class AdminPage
{
    public const string Content = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>PVZ Server Admin</title>
          <style>
            :root { color-scheme: dark; font: 16px system-ui, sans-serif; background: #111827; color: #e2e8f0; }
            body { max-width: 980px; margin: 40px auto; padding: 0 20px; }
            section { margin: 20px 0; padding: 20px; border: 1px solid #475569; border-radius: 12px; background: #1f2937; }
            input, button { padding: 10px; border-radius: 6px; border: 1px solid #64748b; }
            input { color: #e2e8f0; background: #182231; }
            button { color: white; background: #2563eb; cursor: pointer; }
            button.secondary { background: #334155; }
            table { width: 100%; border-collapse: collapse; margin-top: 14px; }
            th, td { text-align: left; padding: 10px; border-bottom: 1px solid #475569; }
            .status { min-height: 24px; color: #fbbf24; white-space: pre-wrap; }
            .active { color: #86efac; } .inactive { color: #fca5a5; }
            code { overflow-wrap: anywhere; }
          </style>
        </head>
        <body>
          <h1>PVZ taskbook server</h1>
          <p>Admin API key authorizes uploads, listing and revocation. Enter it only over HTTPS or on this machine.</p>
          <section>
            <label>Admin API key <input id="key" type="password" autocomplete="off" size="48"></label>
            <button id="refresh" class="secondary">Load stored taskbooks</button>
            <p id="status" class="status"></p>
          </section>
          <section>
            <h2>Upload taskbook</h2>
            <input id="file" type="file" accept=".pvz">
            <button id="upload">Upload .pvz</button>
            <p>New student token (shown only after upload; copy it now): <code id="new-token"></code></p>
          </section>
          <section>
            <h2>Active and historical tokens</h2>
            <table><thead><tr><th>Token hint</th><th>Uploaded</th><th>Expires</th><th>Status</th><th></th></tr></thead><tbody id="rows"></tbody></table>
          </section>
          <script>
            const keyInput = document.getElementById('key');
            const status = document.getElementById('status');
            const authHeaders = () => ({ Authorization: `Bearer ${keyInput.value}` });
            async function refresh() {
              status.textContent = 'Loading...';
              try {
                const response = await fetch('/api/admin/taskbooks', { headers: authHeaders() });
                if (!response.ok) throw new Error(`Request failed (${response.status})`);
                const records = await response.json();
                const rows = document.getElementById('rows');
                rows.replaceChildren();
                for (const item of records) {
                  const row = document.createElement('tr');
                  for (const [value, className] of [
                    [item.tokenHint, ''], [new Date(item.uploadedAt).toLocaleString(), ''],
                    [new Date(item.expiresAt).toLocaleString(), ''],
                    [item.isActive ? 'Active' : 'Expired / revoked', item.isActive ? 'active' : 'inactive']
                  ]) {
                    const cell = document.createElement('td');
                    cell.textContent = value;
                    cell.className = className;
                    row.append(cell);
                  }
                  const action = document.createElement('td');
                  if (item.isActive) {
                    const button = document.createElement('button');
                    button.textContent = 'Revoke';
                    button.className = 'secondary';
                    button.addEventListener('click', async () => {
                      const result = await fetch(`/api/admin/taskbooks/${encodeURIComponent(item.id)}`, {
                        method: 'DELETE', headers: authHeaders()
                      });
                      if (!result.ok) { status.textContent = `Revoke failed (${result.status})`; return; }
                      await refresh();
                    });
                    action.append(button);
                  }
                  row.append(action);
                  rows.append(row);
                }
                status.textContent = `${records.filter(item => item.isActive).length} active token(s). Full tokens are not stored and cannot be recovered here.`;
              } catch (error) { status.textContent = error.message; }
            }
            document.getElementById('refresh').addEventListener('click', refresh);
            document.getElementById('upload').addEventListener('click', async () => {
              const file = document.getElementById('file').files[0];
              if (!file) { status.textContent = 'Select a .pvz file first.'; return; }
              document.getElementById('new-token').textContent = '';
              status.textContent = 'Uploading...';
              try {
                const response = await fetch('/api/admin/taskbooks', {
                  method: 'POST',
                  headers: { ...authHeaders(), 'Content-Type': 'application/octet-stream' },
                  body: file
                });
                const result = await response.json();
                if (!response.ok) throw new Error(result.error || `Upload failed (${response.status})`);
                document.getElementById('new-token').textContent = result.token;
                status.textContent = 'Uploaded. Share this student token, not the admin API key.';
                await refresh();
              } catch (error) { status.textContent = error.message; }
            });
          </script>
        </body>
        </html>
        """;
}
