// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import fs from "node:fs";
import path from "node:path";
import { normalizeSubject, isValidClass } from "../lib/limits.js";

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
  let skipped = 0;
  for (const file of walk(directory)) {
    if (!file.toLowerCase().endsWith(".pvz")) continue;
    const relative = path.relative(directory, file).split(path.sep);
    const subject = relative.length === 3 ? normalizeSubject(relative[0]) : null;
    const classNumber = Number(relative[1]);
    if (!subject || !isValidClass(subject, classNumber) || !/^[^\\/\u0000]+\.pvz$/i.test(relative[2])) {
      console.warn(`Пропущен путь ${file}: ждём предмет/класс/имя.pvz с допустимым классом.`);
      skipped++;
      continue;
    }
    files.push({ file, pathname: `pvz/catalog/${subject}/${classNumber}/${relative[2]}` });
  }
  return { files, skipped };
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
  console.log(`[${index}/${total}] ${pathname}, ${(size / 1024 / 1024).toFixed(2)} МиБ`);
}

const args = parseArgs(process.argv.slice(2));
if (!args.server || !args.password || (!args.catalog && !args.downloads)) {
  console.error("Укажите --server <URL> --password <пароль> и --catalog <папка> и/или --downloads <папка>.");
  process.exit(1);
}

const server = String(args.server).replace(/\/+$/, "");
const catalog = args.catalog ? collectCatalog(path.resolve(String(args.catalog))) : { files: [], skipped: 0 };
const downloads = args.downloads ? collectDownloads(path.resolve(String(args.downloads))) : [];
const files = [...catalog.files, ...downloads];
if (files.length === 0) {
  console.error("Не найдено файлов для загрузки.");
  process.exit(1);
}

console.log(`Сервер: ${server}, файлов: ${files.length}, пропущено: ${catalog.skipped}.`);
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
