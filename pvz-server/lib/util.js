// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import crypto from "node:crypto";

export class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

export class InvalidDataError extends HttpError {
  constructor(message) {
    super(400, message);
  }
}

export class UnauthorizedError extends HttpError {
  constructor(message) {
    super(401, message);
  }
}

export class QuotaError extends HttpError {
  constructor(message) {
    super(400, message);
  }
}

export function sha256Hex(value) {
  return crypto.createHash("sha256").update(value, "utf8").digest("hex").toUpperCase();
}

export function sha256(value) {
  return crypto.createHash("sha256").update(value, "utf8").digest("hex");
}

export function base64Url(bytes) {
  return Buffer.from(bytes).toString("base64").replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
}

export function newAccessToken() {
  return base64Url(crypto.randomBytes(32));
}

const KEY_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

export function randomCode(length, alphabet = KEY_ALPHABET) {
  const bytes = crypto.randomBytes(length);
  let result = "";
  for (let index = 0; index < length; index++) result += alphabet[bytes[index] & 31];
  return result;
}

export function isTokenShaped(token) {
  return typeof token === "string" && token.length === 43 && /^[A-Za-z0-9_-]+$/.test(token);
}

export function fixedTimeEqualHex(left, right) {
  if (typeof left !== "string" || typeof right !== "string" || left.length !== right.length) return false;
  try {
    return crypto.timingSafeEqual(Buffer.from(left, "hex"), Buffer.from(right, "hex"));
  } catch {
    return false;
  }
}

// Секреты из окружения (CRON_SECRET) бывают любой длины, поэтому сравниваем
// посимвольно и без hex.
export function secretsEqual(left, right) {
  const a = Buffer.from(String(left ?? ""), "utf8");
  const b = Buffer.from(String(right ?? ""), "utf8");
  return a.length === b.length && crypto.timingSafeEqual(a, b);
}

export function isSessionId(value) {
  return typeof value === "string" && value.length === 32 && /^[0-9a-fA-F]+$/.test(value);
}

export function requireText(value, maximumLength, error) {
  const normalized = typeof value === "string" ? value.trim() : "";
  if (normalized.length === 0 || normalized.length > maximumLength) throw new InvalidDataError(error);
  return normalized;
}

export function sanitizeFileName(rawName) {
  const raw = typeof rawName === "string" ? rawName.trim() : "";
  const base = raw.split(/[\\/]/).pop() ?? "";
  let cleaned = "";
  for (const character of base) {
    if (character >= " " && !/["<>|:*?\u0000-\u001F]/.test(character)) cleaned += character;
  }
  cleaned = cleaned.trim().replace(/[.]+$/, "");
  if (cleaned.length === 0) cleaned = "file";
  if (cleaned.startsWith(".")) cleaned = "_" + cleaned.slice(1);
  return cleaned.length > 120 ? cleaned.slice(0, 120) : cleaned;
}

export function formatDateRu(value) {
  const date = value instanceof Date ? value : new Date(value);
  const day = String(date.getUTCDate()).padStart(2, "0");
  const month = String(date.getUTCMonth() + 1).padStart(2, "0");
  return `${day}.${month}.${date.getUTCFullYear()}`;
}

export function iso(value = new Date()) {
  return (value instanceof Date ? value : new Date(value)).toISOString();
}

export function addMs(date, ms) {
  return new Date(date.getTime() + ms);
}

export function addDays(date, days) {
  return new Date(date.getTime() + days * 24 * 60 * 60 * 1000);
}

export function parseDate(value) {
  if (!value) return null;
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function isExpired(value, now = new Date()) {
  const date = parseDate(value);
  return !date || date.getTime() <= now.getTime();
}

export function parseJsonBody(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : null;
}

export function randomId() {
  return crypto.randomBytes(16).toString("hex");
}
