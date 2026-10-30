import fs from "node:fs";
import path from "node:path";
import { blob, getJson, setJson, isBlobBackend } from "./storage.js";

// Сборки клиентов PVZ для страницы скачивания: PVZP.exe и PVZT.exe.
// На Vercel файлы лежат в Blob (pvz/downloads/), локально — в папке downloads.
// Серверная сборка PVZS больше не публикуется — сервер теперь живёт на Vercel.

const ITEMS = [
  { id: "pvzp", fileName: "PVZP.exe", role: "student" },
  { id: "pvzt", fileName: "PVZT.exe", role: "teacher" },
];

export const DOWNLOADS_INDEX_KEY = "pvz:downloads";

function localDirectory() {
  return process.env.PVZ_DOWNLOAD_DIR || path.join(process.cwd(), "downloads");
}

function localPath(fileName) {
  return path.join(localDirectory(), fileName);
}

function localSize(fileName) {
  try {
    const stat = fs.statSync(localPath(fileName));
    return stat.isFile() ? stat.size : 0;
  } catch {
    return 0;
  }
}

export function publicIds() {
  return ITEMS.map((item) => item.id);
}

function findItem(id) {
  const normalized = String(id ?? "").toLowerCase();
  return ITEMS.find((item) => item.id === normalized) ?? null;
}

async function describeAll() {
  if (!isBlobBackend()) {
    return ITEMS.map((item) => ({
      id: item.id,
      fileName: item.fileName,
      role: item.role,
      sizeBytes: localSize(item.fileName),
      available: localSize(item.fileName) > 0,
    }));
  }

  const cached = await getJson(DOWNLOADS_INDEX_KEY);
  if (Array.isArray(cached) && cached.length > 0) return cached;

  const listed = await blob().list("pvz/downloads/");
  const byName = new Map(listed.map((item) => [item.pathname.split("/").pop().toLowerCase(), item.size]));
  const result = ITEMS.map((item) => {
    const size = byName.get(item.fileName.toLowerCase()) ?? 0;
    return {
      id: item.id,
      fileName: item.fileName,
      role: item.role,
      sizeBytes: size,
      available: size > 0,
    };
  });
  await setJson(DOWNLOADS_INDEX_KEY, result, 5 * 60 * 1000);
  return result;
}

export async function listPublicDownloads() {
  return describeAll();
}

export async function listAllDownloads() {
  return describeAll();
}

export async function findPublicDownload(id) {
  if (!findItem(id)) return null;
  const all = await describeAll();
  return all.find((entry) => entry.id === id.toLowerCase()) ?? null;
}

export async function openPublicDownload(id) {
  const item = findItem(id);
  if (!item) return null;

  let content;
  if (isBlobBackend()) {
    content = await blob().getBuffer(`pvz/downloads/${item.fileName}`);
  } else {
    try {
      content = fs.readFileSync(localPath(item.fileName));
    } catch {
      content = null;
    }
  }
  if (!content || content.length === 0) return null;
  return {
    info: { id: item.id, fileName: item.fileName, role: item.role, sizeBytes: content.length, available: true },
    content,
  };
}

export async function openDownloadById(id) {
  return openPublicDownload(id);
}
