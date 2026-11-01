// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import { normalizeSubject, isValidClass, catalogIndexKey } from "./limits.js";
import { MAX_PACKAGE_BYTES } from "./config.js";
import { blob } from "./storage.js";
import { deleteJson } from "./storage.js";
import { DOWNLOADS_INDEX_KEY } from "./downloads.js";
import { InvalidDataError } from "./util.js";

// Наполнение Blob (каталог задачников и сборки клиентов) идёт с машины учителя:
// сервер выдаёт одноразовую presigned-ссылку, файл уходит в Blob напрямую.

const DOWNLOAD_FILES = ["PVZP.exe", "PVZT.exe"];
const MAX_DOWNLOAD_BYTES = 512 * 1024 * 1024;

function normalizePath(pathname) {
  return String(pathname ?? "").replace(/\\/g, "/").replace(/^\/+/, "");
}

function catalogTarget(parts) {
  if (parts.length !== 5) return null;
  const subject = normalizeSubject(parts[2]);
  const classNumber = Number(parts[3]);
  if (!subject || !isValidClass(subject, classNumber)) return null;
  if (!/^[^\\/\u0000]+\.pvz$/i.test(parts[4])) return null;
  return { pathname: `pvz/catalog/${subject}/${classNumber}/${parts[4]}`, maxBytes: MAX_PACKAGE_BYTES };
}

function downloadTarget(parts) {
  if (parts.length !== 3) return null;
  const fileName = DOWNLOAD_FILES.find((name) => name.toLowerCase() === parts[2].toLowerCase());
  if (!fileName) return null;
  return { pathname: `pvz/downloads/${fileName}`, maxBytes: MAX_DOWNLOAD_BYTES };
}

export function seedTarget(pathname) {
  const normalized = normalizePath(pathname);
  if (normalized.includes("..")) return null;
  const parts = normalized.split("/");
  if (parts[0] !== "pvz") return null;
  if (parts[1] === "catalog") return catalogTarget(parts);
  if (parts[1] === "downloads") return downloadTarget(parts);
  return null;
}

export async function resetSeedArea(area) {
  if (!blob().remote) throw new InvalidDataError("Локально чистите папку data/blobs.");
  if (area === "catalog") {
    const items = await blob().list("pvz/catalog/");
    const groups = new Set();
    for (const item of items) {
      const parts = item.pathname.split("/");
      if (parts.length >= 5) groups.add(`${parts[2]}:${parts[3]}`);
    }
    await blob().delPrefix("pvz/catalog/");
    for (const group of groups) {
      const [subject, classNumber] = group.split(":");
      await deleteJson(catalogIndexKey(subject, Number(classNumber)));
    }
    return items.length;
  }
  if (area === "downloads") {
    const items = await blob().list("pvz/downloads/");
    await blob().delPrefix("pvz/downloads/");
    await deleteJson(DOWNLOADS_INDEX_KEY);
    return items.length;
  }
  throw new InvalidDataError("Неизвестная область хранилища.");
}

export async function beginSeedUpload(pathname, size) {
  if (!blob().remote) throw new InvalidDataError("Локально наполняйте хранилище скриптом scripts/seed.mjs.");
  const target = seedTarget(pathname);
  if (!target) throw new InvalidDataError("Недопустимый путь наполнения хранилища.");
  if (!Number.isInteger(size) || size <= 0 || size > target.maxBytes) {
    throw new InvalidDataError("Размер файла недопустим.");
  }
  const signed = await blob().presign(target.pathname, {
    contentType: "application/octet-stream",
    maxBytes: target.maxBytes,
  });
  return { pathname: target.pathname, uploadUrl: signed.uploadUrl, headers: signed.headers };
}
