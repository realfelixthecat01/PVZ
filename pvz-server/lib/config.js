// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

export const MAX_PACKAGE_BYTES = 16 * 1024 * 1024;
export const MAX_EXCHANGE_FILE_BYTES = 32 * 1024 * 1024;
export const MAX_SESSION_EXCHANGE_BYTES = 512 * 1024 * 1024;
export const MAX_SCREEN_FRAME_BYTES = 2 * 1024 * 1024;

// Пароль root-аккаунта берётся из PVZ_ROOT_PASSWORD. Встроенного пароля в коде
// нет; при первом запуске без переменной сервер генерирует его и печатает в консоль.
// Прежний встроенный пароль остался в истории git, поэтому в репозитории его больше
// нет: старый аккаунт подменяется только если учитель сам подтвердил его через
// PVZ_LEGACY_ROOT_PASSWORD.
export function legacyRootPassword() {
  const raw = typeof process.env.PVZ_LEGACY_ROOT_PASSWORD === "string" ? process.env.PVZ_LEGACY_ROOT_PASSWORD : "";
  return raw.length === 0 || raw.length > 256 ? null : raw;
}
export const ROOT_EMAIL = "admin@pvz.local";
export const ROOT_USERNAME = "admin";
export const ROOT_FULL_NAME = "Администратор";

export function configuredRootPassword() {
  const raw = typeof process.env.PVZ_ROOT_PASSWORD === "string" ? process.env.PVZ_ROOT_PASSWORD.trim() : "";
  if (raw.length === 0) return null;
  if (raw.length < 8 || raw.length > 128) {
    throw new Error("PVZ_ROOT_PASSWORD должен быть от 8 до 128 символов.");
  }
  return raw;
}

// Сколько неудачных входов с одного адреса в одно и то же имя аккаунта
// терпит сервер до временной блокировки.
export function loginFailureLimit() {
  const raw = process.env.PVZ_LOGIN_FAILURE_LIMIT;
  if (!raw) return 10;
  const value = Number(raw);
  if (!Number.isInteger(value) || value < 1 || value > 100_000) {
    throw new Error("PVZ_LOGIN_FAILURE_LIMIT must be an integer between 1 and 100000.");
  }
  return value;
}

export const SESSION_LIFETIME_MS = 14 * 24 * 60 * 60 * 1000;
export const ACCESS_TOKEN_LIFETIME_MS = 12 * 60 * 60 * 1000;
export const WEB_TOKEN_LIFETIME_MS = 30 * 24 * 60 * 60 * 1000;
export const SCREEN_WATCH_TIMEOUT_MS = 15 * 1000;
export const STUDENT_ONLINE_MS = 30 * 1000;
export const UPLOAD_TTL_MS = 30 * 60 * 1000;

export function isVercelRuntime() {
  return Boolean(process.env.VERCEL || process.env.VERCEL_ENV);
}

export function rateLimitMultiplier() {
  const raw = process.env.PVZ_RATE_LIMIT_MULTIPLIER;
  if (!raw) return 1;
  const value = Number(raw);
  if (!Number.isInteger(value) || value < 1 || value > 1000) {
    throw new Error("PVZ_RATE_LIMIT_MULTIPLIER must be an integer between 1 and 1000.");
  }
  return value;
}
