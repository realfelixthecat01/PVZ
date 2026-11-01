// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import crypto from "node:crypto";
import {
  ROOT_EMAIL,
  ROOT_USERNAME,
  ROOT_FULL_NAME,
  legacyRootPassword,
  configuredRootPassword,
  loginFailureLimit,
  ACCESS_TOKEN_LIFETIME_MS,
  WEB_TOKEN_LIFETIME_MS,
} from "./config.js";
import { getJson, setJson, withLock, deleteJson, kv } from "./storage.js";
import {
  ALL_RETENTIONS,
  UNLIMITED,
} from "./limits.js";
import {
  HttpError,
  InvalidDataError,
  UnauthorizedError,
  isTokenShaped,
  newAccessToken,
  randomCode,
  sha256Hex,
  iso,
  parseDate,
} from "./util.js";

const PBKDF2_ITERATIONS = 100_000;
const PBKDF2_BYTES = 32;
const SALT_BYTES = 16;

export const ROOT = { email: ROOT_EMAIL, username: ROOT_USERNAME, fullName: ROOT_FULL_NAME };

export function hashPassword(password) {
  const salt = crypto.randomBytes(SALT_BYTES);
  const hash = crypto.pbkdf2Sync(password, salt, PBKDF2_ITERATIONS, PBKDF2_BYTES, "sha256");
  return { passwordHash: hash.toString("base64"), passwordSalt: salt.toString("base64") };
}

export function verifyPassword(password, passwordHash, passwordSalt) {
  try {
    const salt = Buffer.from(passwordSalt, "base64");
    const expected = Buffer.from(passwordHash, "base64");
    if (salt.length === 0 || expected.length === 0) return false;
    const actual = crypto.pbkdf2Sync(password, salt, PBKDF2_ITERATIONS, expected.length, "sha256");
    return crypto.timingSafeEqual(actual, expected);
  } catch {
    return false;
  }
}

// Хэш «пустого» пароля выравнивает время ответа при несуществующем аккаунте.
const dummyPassword = hashPassword(crypto.randomBytes(16).toString("hex"));

export function normalizeEmail(email) {
  return String(email ?? "").trim().toLowerCase();
}

export function isValidEmail(email) {
  if (typeof email !== "string" || email.length < 5 || email.length > 190) return false;
  if ((email.match(/@/g) ?? []).length !== 1) return false;
  if (/\s/.test(email)) return false;
  const at = email.indexOf("@");
  return at > 0 && at < email.length - 3 && email.slice(at + 1).includes(".");
}

export function normalizeDisplayName(value) {
  const text = String(value ?? "").replace(/\s+/g, " ").trim();
  if (text.length < 2 || text.length > 120) {
    throw new InvalidDataError("Напишите имя и фамилию, например «Козыро Мария».");
  }
  if (!/^[A-Za-zА-Яа-яЁё0-9 .,'-]+$/.test(text)) {
    throw new InvalidDataError("В имени допускаются буквы, цифры, пробел, точка и дефис.");
  }
  return text;
}

export function normalizeClassName(value) {
  const raw = String(value ?? "").replace(/\s+/g, "").toLowerCase();
  const match = /^(\d{1,2})([a-zа-я]{1,2})?$/.exec(raw);
  if (!match) throw new InvalidDataError("Класс надо писать цифрой и буквой, например 9А или 7.");
  const number = Number(match[1]);
  if (number < 1 || number > 11) throw new InvalidDataError("Класс должен быть от 1 до 11.");
  return `${number}${(match[2] ?? "").toUpperCase()}`;
}

function lookupName(identifier) {
  return String(identifier ?? "").replace(/\s+/g, " ").trim().toLowerCase();
}

const userKey = (id) => `pvz:user:${id}`;
const emailKey = (email) => `pvz:user:email:${normalizeEmail(email)}`;
const nameKey = (username) => `pvz:user:name:${String(username).toLowerCase()}`;
const namesKey = (displayName) => `pvz:user:names:${sha256Hex(lookupName(displayName))}`;
const guestKey = (secret) => `pvz:guest:${sha256Hex(String(secret))}`;
const tokenKey = (token) => `pvz:token:${sha256Hex(token)}`;
const NEXT_USER_ID = "pvz:user:next";

// Под одним именем могут ходить несколько учителей или учеников, поэтому вход
// перебирает кандидатов и проверяет пароль каждого. Больше восьми не смотрим.
const MAX_NAME_CANDIDATES = 8;
export const GUEST_MAX_SESSIONS = 1;

// Неудачные входы считаем по паре «адрес + имя аккаунта», чтобы перебор
// чужого аккаунта не блокировал вход самому себе.
const LOGIN_LOCK_WINDOW_MS = 15 * 60 * 1000;
const failKey = (ip, identifier) =>
  `pvz:loginfail:${sha256Hex(`${ip}|${String(identifier).trim().toLowerCase()}`)}`;

let rootReady = false;
let legacyWarned = false;

// Свой пароль администратора задаётся переменной PVZ_ROOT_PASSWORD. Если её нет,
// при первом запуске пароль генерируется и один раз печатается в консоль.
function initialRootPassword() {
  const fromEnv = configuredRootPassword();
  if (fromEnv) return fromEnv;
  const generated = crypto.randomBytes(12).toString("base64url");
  console.log(`PVZ: пароль администратора ${ROOT_EMAIL}: ${generated}`);
  console.log("PVZ: сохраните его и смените в личном кабинете, либо задайте PVZ_ROOT_PASSWORD.");
  return generated;
}

// Старая сборка создавала root с паролем прямо в коде. Такой пароль меняем
// автоматически, если задан PVZ_ROOT_PASSWORD, иначе напоминаем об этом в логе.
async function retireLegacyRootPassword(root) {
  const legacy = legacyRootPassword();
  if (!legacy || !verifyPassword(legacy, root.passwordHash, root.passwordSalt)) return;
  const fromEnv = configuredRootPassword();
  if (!fromEnv) {
    if (!legacyWarned) {
      legacyWarned = true;
      console.warn("PVZ: у администратора всё ещё прежний встроенный пароль. Задайте PVZ_ROOT_PASSWORD и PVZ_LEGACY_ROOT_PASSWORD, затем перезапустите сервер.");
    }
    return;
  }
  const { passwordHash, passwordSalt } = hashPassword(fromEnv);
  root.passwordHash = passwordHash;
  root.passwordSalt = passwordSalt;
  root.tokenVersion = (root.tokenVersion ?? 0) + 1;
  await updateUser(root);
  console.log(`PVZ: пароль администратора заменён на тот, что в PVZ_ROOT_PASSWORD (id ${root.id}).`);
}

async function ensureRootAccount() {
  if (rootReady) return;
  await withLock(async () => {
    if (rootReady) return;
    const existingId = await kv().get(emailKey(ROOT_EMAIL));
    if (existingId) {
      const root = await readUser(existingId);
      if (root) await retireLegacyRootPassword(root);
      rootReady = true;
      return;
    }
    const id = await kv().incr(NEXT_USER_ID);
    const { passwordHash, passwordSalt } = hashPassword(initialRootPassword());
    const user = {
      id,
      username: ROOT_USERNAME,
      email: ROOT_EMAIL,
      passwordHash,
      passwordSalt,
      fullName: ROOT_FULL_NAME,
      school: "",
      isAdmin: true,
      tokenVersion: 0,
      createdAt: iso(),
    };
    await setJson(userKey(id), user);
    await kv().set(emailKey(ROOT_EMAIL), String(id));
    await kv().set(nameKey(ROOT_USERNAME), String(id));
    rootReady = true;
  });
}

export async function initAccounts() {
  await ensureRootAccount();
}

async function readUser(id) {
  if (id === null || id === undefined) return null;
  return getJson(userKey(id));
}

async function updateUser(user) {
  await setJson(userKey(user.id), user);
}

async function issueToken(user, kind, lifetimeMs) {
  const token = newAccessToken();
  const now = new Date();
  const expiresAt = new Date(now.getTime() + lifetimeMs);
  await setJson(
    tokenKey(token),
    { userId: user.id, kind, tokenVersion: user.tokenVersion ?? 0, expiresAt: iso(expiresAt) },
    lifetimeMs,
  );
  return { accessToken: token, expiresAt };
}

function toPrincipal(user, kind) {
  return {
    userId: user.id,
    username: user.username,
    email: user.email,
    fullName: user.fullName,
    isAdmin: Boolean(user.isAdmin),
    isGuest: Boolean(user.isGuest),
    role: user.role === "student" ? "student" : "teacher",
    className: user.className ?? "",
    kind,
  };
}

export async function authenticate(token) {
  if (!isTokenShaped(token)) return null;
  const row = await getJson(tokenKey(token));
  if (!row) return null;
  const expiresAt = parseDate(row.expiresAt);
  if (!expiresAt || expiresAt.getTime() <= Date.now()) {
    await deleteJson(tokenKey(token));
    return null;
  }
  const user = await readUser(row.userId);
  if (!user) return null;
  // Смена пароля поднимает tokenVersion, и все выданные раньше токены гаснут.
  if ((user.tokenVersion ?? 0) !== (row.tokenVersion ?? 0)) {
    await deleteJson(tokenKey(token));
    return null;
  }
  return toPrincipal(user, row.kind);
}

async function assertLoginAllowed(ip, identifier) {
  const key = failKey(ip ?? "unknown", identifier);
  const failures = Number(await kv().get(key)) || 0;
  if (failures >= loginFailureLimit()) {
    throw new HttpError(
      429,
      "Слишком много неудачных попыток входа. Подождите 15 минут и попробуйте снова.",
    );
  }
}

async function countLoginFailure(ip, identifier) {
  const key = failKey(ip ?? "unknown", identifier);
  const failures = (Number(await kv().get(key)) || 0) + 1;
  await kv().set(key, String(failures), LOGIN_LOCK_WINDOW_MS);
}

async function clearLoginFailures(ip, identifier) {
  await kv().del(failKey(ip ?? "unknown", identifier));
}

// Длинное тело не должно превращаться в дорогой PBKDF2, поэтому пароль смотрим
// до подбора кандидатов и проверяем в границах 1..256 символов.
function passwordUsable(password) {
  return typeof password === "string" && password.length > 0 && password.length <= 256;
}

export async function loginRoot(password, ip = null) {
  await ensureRootAccount();
  await assertLoginAllowed(ip, ROOT_EMAIL);
  const rootId = await kv().get(emailKey(ROOT_EMAIL));
  const root = await readUser(rootId);
  if (!root) return null;
  if (!passwordUsable(password) || !verifyPassword(password, root.passwordHash, root.passwordSalt)) {
    await countLoginFailure(ip, ROOT_EMAIL);
    return null;
  }
  await clearLoginFailures(ip, ROOT_EMAIL);
  return issueToken(root, "root", ACCESS_TOKEN_LIFETIME_MS);
}

async function readIdList(key) {
  const rows = await getJson(key);
  if (Array.isArray(rows)) return rows.map((row) => String(row));
  if (rows !== null && rows !== undefined && rows !== "") return [String(rows)];
  return [];
}

async function appendNameIndex(displayName, userId) {
  const key = namesKey(displayName);
  const ids = await readIdList(key);
  if (!ids.includes(String(userId))) await setJson(key, [...ids, String(userId)].slice(-MAX_NAME_CANDIDATES));
}

async function findUsersByIdentifier(identifier) {
  const trimmed = String(identifier ?? "").trim();
  if (trimmed.length === 0 || trimmed.length > 200) return [];
  if (trimmed.includes("@")) {
    const id = await kv().get(emailKey(trimmed));
    const user = await readUser(id);
    return user ? [user] : [];
  }
  const ids = new Set(await readIdList(namesKey(trimmed)));
  const legacy = await kv().get(nameKey(lookupName(trimmed)));
  if (legacy) ids.add(String(legacy));
  const users = [];
  for (const id of [...ids].slice(0, MAX_NAME_CANDIDATES)) {
    const user = await readUser(id);
    if (user) users.push(user);
  }
  return users;
}

export async function login(identifier, password, ip = null) {
  await ensureRootAccount();
  if (typeof identifier !== "string" || identifier.trim().length === 0) return null;
  await assertLoginAllowed(ip, identifier);
  if (!passwordUsable(password)) {
    await countLoginFailure(ip, identifier);
    return null;
  }
  const candidates = await findUsersByIdentifier(identifier);
  if (candidates.length === 0) {
    verifyPassword(password, dummyPassword.passwordHash, dummyPassword.passwordSalt);
    await countLoginFailure(ip, identifier);
    return null;
  }
  const user = candidates.find((candidate) =>
    verifyPassword(password, candidate.passwordHash, candidate.passwordSalt)) ?? null;
  if (!user) {
    await countLoginFailure(ip, identifier);
    return null;
  }
  await clearLoginFailures(ip, identifier);
  // Старые аккаунты умели входить только по латинскому имени аккаунта. Записываем
  // ФИО в индекс имён при удачном входе, и дальше в аккаунт можно войти по-русски.
  if (user.fullName) await withLock(() => appendNameIndex(user.fullName, user.id));
  const receipt = await issueToken(user, "web", WEB_TOKEN_LIFETIME_MS);
  return { receipt, principal: toPrincipal(user, "web") };
}

async function allocateUsername(displayName) {
  const base = lookupName(displayName).slice(0, 40);
  for (let attempt = 0; attempt < 40; attempt++) {
    const candidate = attempt === 0 ? base : `${base}${attempt + 2}`;
    if (!(await kv().get(nameKey(candidate)))) return candidate;
  }
  return `${base}-${randomCode(4).toLowerCase()}`;
}

export function isValidPassword(password) {
  return typeof password === "string" && password.length >= 6 && password.length <= 128;
}

// Регистрация общая для двух ролей. Учителю нужны только имя и пароль: школа
// и email остаются необязательными, их можно дописать позже в кабинете.
export async function register({ role, fullName, password, className, school, email }) {
  await ensureRootAccount();
  const isStudent = String(role ?? "").trim().toLowerCase() === "student";
  const normalizedName = normalizeDisplayName(fullName);
  const normalizedClass = isStudent ? normalizeClassName(className) : "";
  const normalizedSchool = String(school ?? "").replace(/\s+/g, " ").trim().slice(0, 160);
  const rawEmail = String(email ?? "").trim();
  const normalizedEmail = rawEmail.length === 0 ? "" : normalizeEmail(rawEmail);
  if (normalizedEmail && !isValidEmail(normalizedEmail)) {
    throw new InvalidDataError("Укажите корректный email, например teacher@school.by, или оставьте поле пустым.");
  }
  if (!isValidPassword(password)) {
    throw new InvalidDataError("Пароль должен быть от 6 до 128 символов.");
  }

  return withLock(async () => {
    if (normalizedEmail && await kv().get(emailKey(normalizedEmail))) {
      throw new InvalidDataError("Аккаунт с таким email уже зарегистрирован. Войдите в него.");
    }
    const id = await kv().incr(NEXT_USER_ID);
    const { passwordHash, passwordSalt } = hashPassword(password);
    const user = {
      id,
      username: await allocateUsername(normalizedName),
      email: normalizedEmail,
      passwordHash,
      passwordSalt,
      fullName: normalizedName,
      school: normalizedSchool,
      role: isStudent ? "student" : "teacher",
      className: normalizedClass,
      isGuest: false,
      isAdmin: false,
      tokenVersion: 0,
      createdAt: iso(),
    };
    await setJson(userKey(id), user);
    await kv().set(nameKey(user.username), String(id));
    if (normalizedEmail) await kv().set(emailKey(normalizedEmail), String(id));
    await appendNameIndex(normalizedName, id);
    const receipt = await issueToken(user, "web", WEB_TOKEN_LIFETIME_MS);
    return { receipt, principal: toPrincipal(user, "web") };
  });
}

// Гостевой вход: учитель приводит свой длинный случайный код, сервер по нему
// находит временный аккаунт или создаёт новый. Никаких имён и паролей.
export async function loginGuest(secret, ip = null) {
  await ensureRootAccount();
  if (typeof secret !== "string" || secret.length < 32 || secret.length > 128 || !/^[A-Za-z0-9_-]+$/.test(secret)) {
    throw new InvalidDataError("Гостевой ключ повреждён. Пересоздайте его в приложении.");
  }
  await assertLoginAllowed(ip, `guest:${sha256Hex(secret).slice(0, 12)}`);
  return withLock(async () => {
    const key = guestKey(secret);
    let user = null;
    const existingId = await kv().get(key);
    if (existingId) user = await readUser(existingId);
    if (!user) {
      const id = await kv().incr(NEXT_USER_ID);
      user = {
        id,
        username: `гость-${randomCode(4).toLowerCase()}`,
        email: "",
        passwordHash: "",
        passwordSalt: "",
        fullName: "Гость",
        school: "",
        role: "teacher",
        className: "",
        isGuest: true,
        isAdmin: false,
        tokenVersion: 0,
        createdAt: iso(),
      };
      await setJson(userKey(id), user);
      await kv().set(nameKey(user.username), String(id));
      await kv().set(key, String(id));
    }
    const receipt = await issueToken(user, "web", WEB_TOKEN_LIFETIME_MS);
    return { receipt, principal: toPrincipal(user, "web") };
  });
}

export async function logout(token) {
  await deleteJson(tokenKey(token));
}

export async function changePassword(principal, currentPassword, newPassword) {
  const user = await readUser(principal.userId);
  if (!user) throw new UnauthorizedError("Аккаунт не найден. Войдите снова.");
  if (typeof currentPassword !== "string" || !verifyPassword(currentPassword, user.passwordHash, user.passwordSalt)) {
    throw new InvalidDataError("Текущий пароль указан неверно.");
  }
  if (typeof newPassword !== "string" || newPassword.length < 6 || newPassword.length > 128) {
    throw new InvalidDataError("Новый пароль должен быть от 6 до 128 символов.");
  }
  const { passwordHash, passwordSalt } = hashPassword(newPassword);
  user.passwordHash = passwordHash;
  user.passwordSalt = passwordSalt;
  // Прежние токены этого аккаунта гаснут, текущему устройству выдаём новый.
  user.tokenVersion = (user.tokenVersion ?? 0) + 1;
  await updateUser(user);
  const lifetime = principal.kind === "root" ? ACCESS_TOKEN_LIFETIME_MS : WEB_TOKEN_LIFETIME_MS;
  return issueToken(user, principal.kind, lifetime);
}

export async function getProfile(principal, usage) {
  const user = await readUser(principal.userId);
  if (!user) throw new UnauthorizedError("Аккаунт не найден. Войдите снова.");
  const identity = {
    id: user.id,
    username: user.username,
    email: user.email,
    fullName: user.fullName,
    school: user.school ?? "",
  };
  if (user.role === "student") {
    return {
      ...identity,
      role: "student",
      className: user.className ?? "",
      isAdmin: false,
      isGuest: false,
      storage: [],
    };
  }
  const storage = ALL_RETENTIONS.map((retention) => ({
    retention,
    used: usage.get(retention) ?? 0,
    limit: UNLIMITED,
  }));
  return {
    ...identity,
    role: "teacher",
    className: "",
    isAdmin: Boolean(user.isAdmin),
    isGuest: Boolean(user.isGuest),
    storage,
  };
}

export async function updateProfile(principal, fullName, school) {
  const normalizedName = normalizeDisplayName(fullName);
  const normalizedSchool = String(school ?? "").replace(/\s+/g, " ").trim().slice(0, 160);
  const user = await readUser(principal.userId);
  if (!user) throw new UnauthorizedError("Аккаунт не найден. Войдите снова.");
  user.fullName = normalizedName;
  user.school = normalizedSchool;
  await updateUser(user);
  await withLock(() => appendNameIndex(normalizedName, user.id));
}

export async function listUsers() {
  const next = Number((await kv().get(NEXT_USER_ID)) ?? 0);
  const users = [];
  for (let id = 1; id <= next; id++) {
    const user = await readUser(id);
    if (user) users.push(user);
  }
  users.sort((a, b) => a.id - b.id);
  return users;
}

export async function findUserById(id) {
  return readUser(id);
}
