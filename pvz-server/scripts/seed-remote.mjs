import fs from "node:fs";
import path from "node:path";

// Наполняет Blob развёрнутого сервера каталогом и сборками:
//   node scripts/seed-remote.mjs --server https://pvz-server.vercel.app --password <пароль> \
//     --catalog <папка zadachniki> --downloads <папка downloads>
// Сервер выдаёт presigned-ссылку, файл уходит в Blob напрямую (без лимита 4,5 МБ).

function parseArgs(argv) {
  const args = {};
  for (let index = 0; index < argv.length; index++) {
    const arg = argv[index];
    if (!arg.startsWith("--")) continue;
    const key = arg.slice(2);
    const value = argv[index + 1];
    if (!value || value.startsWith("--")) args[key] = true;
    else {
      args[key] = value;
      index++;
    }
  }
  return args;
}

function walk(directory) {
  const result = [];
  if (!fs.existsSync(directory)) return result;
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) result.push(...walk(full));
    else result.push(full);
  }
  return result;
}

function collectCatalog(directory) {
  const files = [];
  for (const file of walk(directory)) {
    if (!file.toLowerCase().endsWith(".pvz")) continue;
    const relative = path.relative(directory, file).split(path.sep);
    if (relative.length !== 3) continue;
    files.push({ file, pathname: `pvz/catalog/${relative[0]}/${Number(relative[1])}/${relative[2]}` });
  }
  return files;
}

function collectDownloads(directory) {
  const files = [];
  for (const name of ["PVZP.exe", "PVZT.exe"]) {
    const file = path.join(directory, name);
    if (fs.existsSync(file)) files.push({ file, pathname: `pvz/downloads/${name}` });
  }
  return files;
}

async function login(server, password) {
  const response = await fetch(`${server}/api/teacher/login`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ password }),
  });
  if (!response.ok) throw new Error(`Вход не удался: HTTP ${response.status}.`);
  const { accessToken } = await response.json();
  return accessToken;
}

async function upload(server, token, { file, pathname }, index, total) {
  const size = fs.statSync(file).size;
  const ticket = await fetch(`${server}/api/admin/seed`, {
    method: "POST",
    headers: { "content-type": "application/json", authorization: `Bearer ${token}` },
    body: JSON.stringify({ pathname, size }),
  });
  if (!ticket.ok) {
    throw new Error(`${pathname}: сервер не выдал ссылку (HTTP ${ticket.status}: ${await ticket.text()}).`);
  }
  const { uploadUrl, headers } = await ticket.json();
  const put = await fetch(new URL(uploadUrl, server), { method: "PUT", headers, body: fs.readFileSync(file) });
  if (!put.ok) throw new Error(`${pathname}: загрузка в Blob не удалась (HTTP ${put.status}).`);
  console.log(`[${index}/${total}] ${pathname} — ${(size / 1024 / 1024).toFixed(2)} МиБ`);
}

const args = parseArgs(process.argv.slice(2));
if (!args.server || !args.password || (!args.catalog && !args.downloads)) {
  console.error("Укажите --server <URL> --password <пароль> и --catalog <папка> и/или --downloads <папка>.");
  process.exit(1);
}

const server = String(args.server).replace(/\/+$/, "");
const files = [
  ...(args.catalog ? collectCatalog(path.resolve(String(args.catalog))) : []),
  ...(args.downloads ? collectDownloads(path.resolve(String(args.downloads))) : []),
];
if (files.length === 0) {
  console.error("Не найдено файлов для загрузки.");
  process.exit(1);
}

console.log(`Сервер: ${server}, файлов: ${files.length}.`);
const token = await login(server, String(args.password));

if (args.reset) {
  for (const area of [args.catalog ? "catalog" : null, args.downloads ? "downloads" : null].filter(Boolean)) {
    const response = await fetch(`${server}/api/admin/seed/reset`, {
      method: "POST",
      headers: { "content-type": "application/json", authorization: `Bearer ${token}` },
      body: JSON.stringify({ area }),
    });
    if (!response.ok) throw new Error(`Очистка ${area} не удалась: HTTP ${response.status}.`);
    const { removed } = await response.json();
    console.log(`Очищено ${area}: ${removed} файлов.`);
  }
}

for (const [index, item] of files.entries()) {
  await upload(server, token, item, index + 1, files.length);
}
console.log(`Готово: загружено ${files.length} файлов.`);
