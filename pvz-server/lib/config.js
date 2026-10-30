export const MAX_PACKAGE_BYTES = 16 * 1024 * 1024;
export const MAX_EXCHANGE_FILE_BYTES = 32 * 1024 * 1024;
export const MAX_SESSION_EXCHANGE_BYTES = 512 * 1024 * 1024;
export const MAX_SCREEN_FRAME_BYTES = 2 * 1024 * 1024;

export const DEFAULT_ACCESS_PASSWORD = "ezfridgembxd";
export const ROOT_EMAIL = "admin@pvz.local";
export const ROOT_USERNAME = "admin";
export const ROOT_FULL_NAME = "Администратор";

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
