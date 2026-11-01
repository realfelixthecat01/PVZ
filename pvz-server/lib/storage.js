// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { isVercelRuntime } from "./config.js";

// Состояние (Redis или локальный JSON-файл) и файлы (Vercel Blob или локальный диск).
// Оба бэкенда реализуют один интерфейс, чтобы один и тот же код работал
// в разработке на машине учителя и на Vercel.

const dataDir = process.env.PVZ_SERVER_DATA_DIR
  || path.join(isVercelRuntime() ? "/tmp" : process.cwd(), isVercelRuntime() ? "pvz-data" : "data");
fs.mkdirSync(path.join(dataDir, "blobs"), { recursive: true });
fs.mkdirSync(path.join(dataDir, "uploads"), { recursive: true });

export function isBlobBackend() {
  // OIDC (BLOB_STORE_ID + VERCEL_OIDC_TOKEN) работает только в сборке на Vercel,
  // локально остаётся read-write токен.
  return Boolean(process.env.BLOB_READ_WRITE_TOKEN || process.env.BLOB_STORE_ID);
}

// state (KV)

class LocalKv {
  constructor() {
    this.file = path.join(dataDir, "store.json");
    this.values = new Map();
    this.expiry = new Map();
    this.queue = Promise.resolve();
    try {
      const raw = JSON.parse(fs.readFileSync(this.file, "utf8"));
      for (const [key, entry] of Object.entries(raw)) {
        this.values.set(key, entry.value);
        if (entry.expireAt) this.expiry.set(key, entry.expireAt);
      }
    } catch {
      // пустое хранилище при первом запуске
    }
  }

  async get(key) {
    this.expireIfNeeded(key);
    return this.values.has(key) ? this.values.get(key) : null;
  }

  async mget(keys) {
    return keys.map((key) => {
      this.expireIfNeeded(key);
      return this.values.has(key) ? this.values.get(key) : null;
    });
  }

  async set(key, value, ttlMs = null) {
    this.values.set(key, value);
    if (ttlMs) this.expiry.set(key, Date.now() + ttlMs);
    else this.expiry.delete(key);
    await this.flush();
  }

  async del(key) {
    this.values.delete(key);
    this.expiry.delete(key);
    await this.flush();
  }

  async incr(key) {
    const current = Number(await this.get(key)) || 0;
    const next = current + 1;
    await this.set(key, String(next));
    return next;
  }

  // Атомарная проверка-запись в одном микротаске: локальный процесс однопоточный.
  async acquire(key, token, ttlMs) {
    this.expireIfNeeded(key);
    if (this.values.has(key)) return false;
    this.values.set(key, token);
    this.expiry.set(key, Date.now() + ttlMs);
    await this.flush();
    return true;
  }

  async release(key, token) {
    this.expireIfNeeded(key);
    if (this.values.get(key) !== token) return;
    this.values.delete(key);
    this.expiry.delete(key);
    await this.flush();
  }

  expireIfNeeded(key) {
    const expireAt = this.expiry.get(key);
    if (expireAt && expireAt <= Date.now()) {
      this.values.delete(key);
      this.expiry.delete(key);
    }
  }

  async flush() {
    this.expireAll();
    const snapshot = {};
    for (const [key, value] of this.values) {
      snapshot[key] = { value, expireAt: this.expiry.get(key) ?? 0 };
    }
    const temporary = this.file + `.${crypto.randomBytes(4).toString("hex")}.tmp`;
    await this.queue;
    this.queue = (async () => {
      fs.writeFileSync(temporary, JSON.stringify(snapshot));
      fs.renameSync(temporary, this.file);
    })();
    await this.queue;
  }

  expireAll() {
    for (const key of [...this.expiry.keys()]) this.expireIfNeeded(key);
  }
}

class RedisKv {
  constructor(url) {
    this.url = url;
    this.clientPromise = null;
  }

  client() {
    if (this.clientPromise) return this.clientPromise;
    const opening = (async () => {
      const { createClient } = await import("redis");
      const client = createClient({ url: this.url, socket: { reconnectStrategy: false } });
      client.on("error", () => {});
      await client.connect();
      return client;
    })();
    // Упавшее подключение не должно висеть в памяти: при неудаче пробуруем заново.
    this.clientPromise = opening.catch((error) => {
      if (this.clientPromise === opening) this.clientPromise = null;
      throw error;
    });
    return this.clientPromise;
  }

  async get(key) {
    return (await this.client()).get(key);
  }

  async mget(keys) {
    if (keys.length === 0) return [];
    return (await this.client()).mGet(keys);
  }

  async set(key, value, ttlMs = null) {
    const client = await this.client();
    if (ttlMs) await client.set(key, value, { PX: ttlMs });
    else await client.set(key, value);
  }

  async del(key) {
    await (await this.client()).del(key);
  }

  async incr(key) {
    return Number(await (await this.client()).incr(key));
  }

  async acquire(key, token, ttlMs) {
    const result = await (await this.client()).set(key, token, { NX: true, PX: ttlMs });
    return result === "OK";
  }

  async release(key, token) {
    // Снимаем блокировку только если она всё ещё наша.
    await (await this.client()).eval(
      "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
      { keys: [key], arguments: [token] },
    );
  }
}

let kvInstance = null;

export function kv() {
  if (!kvInstance) {
    if (process.env.REDIS_URL) kvInstance = new RedisKv(process.env.REDIS_URL);
    else kvInstance = new LocalKv();
  }
  return kvInstance;
}

export async function getJson(key) {
  const raw = await kv().get(key);
  if (raw === null || raw === undefined) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

export async function setJson(key, value, ttlMs = null) {
  await kv().set(key, JSON.stringify(value), ttlMs);
}

export async function mgetJson(keys) {
  if (keys.length === 0) return [];
  const values = await kv().mget(keys);
  return values.map((raw) => {
    if (raw === null || raw === undefined) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  });
}

export async function deleteJson(key) {
  await kv().del(key);
}

// Глобальная блокировка записи, как семафор в настольном PVZS.
const LOCK_TTL_MS = 15000;

export async function withLock(fn) {
  const store = kv();
  const lockKey = "pvz:lock";
  for (let attempt = 0; attempt < 150; attempt++) {
    const token = crypto.randomBytes(12).toString("hex");
    if (await store.acquire(lockKey, token, LOCK_TTL_MS)) {
      try {
        return await fn();
      } finally {
        await store.release(lockKey, token);
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw Object.assign(new Error("Сервер занят. Повторите запрос через несколько секунд."), { status: 503 });
}

// файлы (blob)

function localBlobPath(pathname) {
  const normalized = pathname.replace(/\\/g, "/");
  if (normalized.includes("..") || normalized.startsWith("/")) {
    throw new Error("Недопустимый путь хранилища.");
  }
  return path.join(dataDir, "blobs", normalized);
}

class LocalBlob {
  async put(pathname, buffer) {
    const target = localBlobPath(pathname);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, buffer);
    return { url: `local://${pathname}`, pathname };
  }

  async getBuffer(pathname) {
    try {
      return fs.readFileSync(localBlobPath(pathname));
    } catch {
      return null;
    }
  }

  async del(pathname) {
    try {
      fs.unlinkSync(localBlobPath(pathname));
    } catch {
      // уже удалён
    }
  }

  async delPrefix(prefix) {
    const target = localBlobPath(prefix);
    try {
      fs.rmSync(target, { recursive: true, force: true });
    } catch {
      // уже удалён
    }
  }

  async list(prefix) {
    const root = path.join(dataDir, "blobs");
    const items = [];
    const walk = (directory) => {
      for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        const full = path.join(directory, entry.name);
        if (entry.isDirectory()) walk(full);
        else {
          const relative = path.relative(root, full).replace(/\\/g, "/");
          if (relative.startsWith(prefix)) items.push({ pathname: relative, size: fs.statSync(full).size });
        }
      }
    };
    if (fs.existsSync(root)) walk(root);
    items.sort((a, b) => a.pathname.localeCompare(b.pathname));
    return items;
  }

  async presign(pathname) {
    const uploadId = crypto.randomBytes(16).toString("hex");
    return {
      uploadId,
      uploadUrl: `/api/upload/put/${uploadId}`,
      headers: { "content-type": "application/octet-stream" },
      pathname,
    };
  }

  async stat(pathname) {
    try {
      return { size: fs.statSync(localBlobPath(pathname)).size, url: `local://${pathname}` };
    } catch {
      return null;
    }
  }

  async readUpload(uploadId) {
    const file = path.join(dataDir, "uploads", uploadId.replace(/[^0-9a-f]/gi, ""));
    return fs.existsSync(file) ? fs.readFileSync(file) : null;
  }

  async writeUpload(uploadId, buffer) {
    fs.writeFileSync(path.join(dataDir, "uploads", uploadId), buffer);
  }

  async removeUpload(uploadId) {
    try {
      fs.unlinkSync(path.join(dataDir, "uploads", uploadId));
    } catch {
      // нет файла, и ладно
    }
  }

  async promoteUpload(uploadId, pathname) {
    const source = path.join(dataDir, "uploads", uploadId.replace(/[^0-9a-f]/gi, ""));
    const target = localBlobPath(pathname);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.renameSync(source, target);
  }

  get remote() {
    return false;
  }
}

class VercelBlob {
  constructor(token) {
    this.token = token || null;
    // Хранилище проекта приватное: файлы отдаются только через сервер.
    this.access = process.env.PVZ_BLOB_ACCESS === "public" ? "public" : "private";
  }

  sdk() {
    if (!this.sdkModule) this.sdkModule = import("@vercel/blob");
    return this.sdkModule;
  }

  // Read-write токен или OIDC (BLOB_STORE_ID + VERCEL_OIDC_TOKEN), SDK выбирает сам.
  authOptions(extra = {}) {
    const options = { ...extra };
    if (this.token) options.token = this.token;
    return options;
  }

  async put(pathname, buffer, contentType = "application/octet-stream") {
    const { put } = await this.sdk();
    const blob = await put(pathname, buffer, this.authOptions({
      access: this.access,
      addRandomSuffix: false,
      allowOverwrite: true,
      contentType,
    }));
    return { url: blob.url, pathname: blob.pathname };
  }

  async getBuffer(pathname) {
    const { get } = await this.sdk();
    try {
      const result = await get(pathname, this.authOptions({ access: this.access }));
      if (!result || !result.stream) return null;
      // Ответ 404 тоже приносит тело: забираем поток, чтобы не держать соединение.
      if (result.statusCode !== 200) {
        await result.stream.cancel();
        return null;
      }
      return Buffer.from(await new Response(result.stream).arrayBuffer());
    } catch {
      return null;
    }
  }

  async del(pathname) {
    const { del } = await this.sdk();
    try {
      await del(pathname, this.authOptions());
    } catch {
      // уже удалён
    }
  }

  async delPrefix(prefix) {
    const { del } = await this.sdk();
    const items = await this.list(prefix);
    for (const item of items) {
      try {
        await del(item.url || item.pathname, this.authOptions());
      } catch {
        // уже удалён
      }
    }
  }

  async list(prefix) {
    const { list } = await this.sdk();
    const items = [];
    let cursor;
    do {
      const page = await list(this.authOptions({ prefix, cursor }));
      for (const blob of page.blobs) {
        items.push({ pathname: blob.pathname, size: blob.size, url: blob.url });
      }
      cursor = page.cursor;
    } while (cursor);
    items.sort((a, b) => a.pathname.localeCompare(b.pathname));
    return items;
  }

  // Клиент грузит файл напрямую в Blob: функция не принимает тело,
  // поэтому 4,5 МБ лимит Vercel не мешает загрузке задачников до 16 МиБ.
  async presign(pathname, { contentType = "application/octet-stream", maxBytes } = {}) {
    const { issueSignedToken, presignUrl } = await this.sdk();
    const signed = await issueSignedToken(this.authOptions({
      pathname,
      operations: ["put"],
      allowedContentTypes: [contentType],
      maximumSizeInBytes: maxBytes,
      validUntil: Date.now() + 30 * 60 * 1000,
    }));
    // Суффикс у presigned PUT выключен нарочно: иначе Blob добавит к имени свою
    // приписку, и pathname не совпадёт с тем, что ожидает сервер.
    const { presignedUrl } = await presignUrl(signed, {
      operation: "put",
      pathname,
      addRandomSuffix: false,
      allowOverwrite: true,
    });
    return {
      uploadId: crypto.randomBytes(16).toString("hex"),
      uploadUrl: presignedUrl,
      headers: { "content-type": contentType },
      pathname,
    };
  }

  async stat(pathname) {
    const { head } = await this.sdk();
    try {
      const info = await head(pathname, this.authOptions());
      return info ? { size: info.size, url: info.url } : null;
    } catch {
      return null;
    }
  }

  async readUpload() {
    return null; // на Vercel файл приходит прямо в Blob
  }

  async writeUpload() {
    throw new Error("Прямая загрузка через функцию недоступна на Vercel, используйте presigned URL.");
  }

  async removeUpload() {}

  async promoteUpload() {
    throw new Error("Перемещение blob-файлов недоступно.");
  }

  get remote() {
    return true;
  }
}

let blobInstance = null;

export function blob() {
  if (!blobInstance) {
    if (isBlobBackend()) blobInstance = new VercelBlob(process.env.BLOB_READ_WRITE_TOKEN);
    else blobInstance = new LocalBlob();
  }
  return blobInstance;
}

export const BLOBS_ARE_REMOTE = () => blob().remote;
