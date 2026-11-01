// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import fs from "node:fs";
import path from "node:path";
import { blob, isBlobBackend } from "../lib/storage.js";
import { setJson } from "../lib/storage.js";
import { catalogIndexKey, normalizeSubject, isValidClass } from "../lib/limits.js";

// Наполняет хранилище сервера каталогом задачников и сборками клиентов.
//   node scripts/seed.mjs --catalog <папка zadachniki> --downloads <папка downloads>
// С BLOB_READ_WRITE_TOKEN и REDIS_URL пишет прямо в Vercel Blob/Redis.

function parseArgs(argv) {
  const args = {};
  for (let index = 0; index < argv.length; index++) {
    const arg = argv[index];
    if (!arg.startsWith("--")) continue;
    const key = arg.slice(2);
    const value = argv[index + 1];
    if (!value || value.startsWith("--")) {
      args[key] = true;
    } else {
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

async function seedCatalog(catalogDirectory) {
  let uploaded = 0;
  let skipped = 0;
  for (const file of walk(catalogDirectory)) {
    if (!file.toLowerCase().endsWith(".pvz")) continue;
    const relative = path.relative(catalogDirectory, file).split(path.sep);
    if (relative.length !== 3) {
      skipped++;
      continue;
    }
    const subject = normalizeSubject(relative[0]);
    const classNumber = Number(relative[1]);
    if (!subject || !isValidClass(subject, classNumber)) {
      skipped++;
      continue;
    }
    const fileName = relative[2];
    const buffer = fs.readFileSync(file);
    await blob().put(`pvz/catalog/${subject}/${classNumber}/${fileName}`, buffer, "application/octet-stream");
    uploaded++;
  }

  // Индекс каталога: по одному списку на предмет и класс.
  const groups = new Map();
  for (const file of walk(catalogDirectory)) {
    if (!file.toLowerCase().endsWith(".pvz")) continue;
    const relative = path.relative(catalogDirectory, file).split(path.sep);
    if (relative.length !== 3) continue;
    const subject = normalizeSubject(relative[0]);
    const classNumber = Number(relative[1]);
    if (!subject || !isValidClass(subject, classNumber)) continue;
    const key = `${subject}:${classNumber}`;
    const list = groups.get(key) ?? [];
    list.push({ fileName: relative[2], sizeBytes: fs.statSync(file).size });
    groups.set(key, list);
  }
  for (const [key, list] of groups) {
    const [subject, classNumber] = key.split(":");
    list.sort((a, b) => a.fileName.localeCompare(b.fileName, "ru", { sensitivity: "base" }));
    await setJson(catalogIndexKey(subject, Number(classNumber)), list);
  }
  return { uploaded, skipped, groups: groups.size };
}

async function seedDownloads(downloadsDirectory) {
  const items = [
    { id: "pvzp", fileName: "PVZP.exe", role: "student" },
    { id: "pvzt", fileName: "PVZT.exe", role: "teacher" },
  ];
  const index = [];
  for (const item of items) {
    const file = path.join(downloadsDirectory, item.fileName);
    if (!fs.existsSync(file)) {
      index.push({ ...item, sizeBytes: 0, available: false });
      continue;
    }
    const buffer = fs.readFileSync(file);
    // Локально сборки читаются прямо из папки downloads, в Blob их дублировать не нужно.
    if (isBlobBackend())
      await blob().put(`pvz/downloads/${item.fileName}`, buffer, "application/octet-stream");
    index.push({ ...item, sizeBytes: buffer.length, available: true });
  }
  await setJson("pvz:downloads", index);
  return index;
}

const args = parseArgs(process.argv.slice(2));
if (!args.catalog && !args.downloads) {
  console.error("Укажите --catalog <папка zadachniki> и/или --downloads <папка downloads>.");
  process.exit(1);
}

const mode = isBlobBackend() ? "Vercel Blob" : "локальный диск";
console.log(`Хранилище: ${mode}${process.env.REDIS_URL ? " + Redis" : ""}`);

if (args.catalog) {
  const result = await seedCatalog(path.resolve(String(args.catalog)));
  console.log(`Каталог: загружено ${result.uploaded}, пропущено ${result.skipped}, групп ${result.groups}.`);
}
if (args.downloads) {
  const index = await seedDownloads(path.resolve(String(args.downloads)));
  console.log(`Сборки: ${index.map((item) => `${item.fileName} ${item.available ? "есть" : "нет"}`).join(", ")}.`);
}
