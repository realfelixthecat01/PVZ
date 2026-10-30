import crypto from "node:crypto";
import {
  ROOT_EMAIL,
  ROOT_USERNAME,
  ROOT_FULL_NAME,
  DEFAULT_ACCESS_PASSWORD,
  ACCESS_TOKEN_LIFETIME_MS,
  WEB_TOKEN_LIFETIME_MS,
} from "./config.js";
import { getJson, setJson, withLock, deleteJson, kv } from "./storage.js";
import {
  STORAGE,
  UNLIMITED,
  effectivePlan,
  limitFor,
  planDisplayName,
  retentionsFor,
  displaySubject,
  normalizeSubject,
  retentionDisplayName,
  retentionShortLabel,
  isValidClass,
  minimumClassNumber,
} from "./limits.js";
import {
  HttpError,
  InvalidDataError,
  UnauthorizedError,
  isTokenShaped,
  newAccessToken,
  randomCode,
  sha256Hex,
  requireText,
  addDays,
  formatDateRu,
  iso,
  parseDate,
} from "./util.js";

const PBKDF2_ITERATIONS = 100_000;
const PBKDF2_BYTES = 32;
const SALT_BYTES = 16;
const KEY_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
const KEY_BODY_LENGTH = 12;
const MAXIMUM_KEY_ATTEMPTS = 20;

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

export function normalizeUsername(username) {
  const normalized = String(username ?? "").trim().toLowerCase();
  if (normalized.length < 3 || normalized.length > 32 || !/^[a-z0-9._-]+$/.test(normalized)) {
    throw new InvalidDataError(
      "Имя аккаунта — от 3 до 32 символов: латинские буквы, цифры, точка, дефис или подчёркивание.",
    );
  }
  return normalized;
}

const userKey = (id) => `pvz:user:${id}`;
const emailKey = (email) => `pvz:user:email:${normalizeEmail(email)}`;
const nameKey = (username) => `pvz:user:name:${String(username).toLowerCase()}`;
const tokenKey = (token) => `pvz:token:${sha256Hex(token)}`;
const subsKey = (userId) => `pvz:subs:${userId}`;
const keyKey = (code) => `pvz:key:${code}`;
const KEYCODES = "pvz:keycodes";
const NEXT_USER_ID = "pvz:user:next";

let rootReady = false;

async function ensureRootAccount() {
  if (rootReady) return;
  await withLock(async () => {
    if (rootReady) return;
    if (await kv().get(emailKey(ROOT_EMAIL))) {
      rootReady = true;
      return;
    }
    const id = await kv().incr(NEXT_USER_ID);
    const { passwordHash, passwordSalt } = hashPassword(DEFAULT_ACCESS_PASSWORD);
    const user = {
      id,
      username: ROOT_USERNAME,
      email: ROOT_EMAIL,
      passwordHash,
      passwordSalt,
      fullName: ROOT_FULL_NAME,
      school: "",
      isAdmin: true,
      storagePlan: STORAGE.FREE,
      storageUntil: null,
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

async function issueToken(userId, kind, lifetimeMs) {
  const token = newAccessToken();
  const now = new Date();
  const expiresAt = new Date(now.getTime() + lifetimeMs);
  await setJson(tokenKey(token), { userId, kind, expiresAt: iso(expiresAt) }, lifetimeMs);
  return { accessToken: token, expiresAt };
}

function toPrincipal(user, kind) {
  return {
    userId: user.id,
    username: user.username,
    email: user.email,
    fullName: user.fullName,
    isAdmin: Boolean(user.isAdmin),
    kind,
  };
}

export async function authenticate(token) {
  if (!isTokenShaped(token)) return null;
  const row = await getJson(tokenKey(token));
  if (!row) return null;
  if (parseDate(row.expiresAt)?.getTime() <= Date.now()) {
    await deleteJson(tokenKey(token));
    return null;
  }
  const user = await readUser(row.userId);
  return user ? toPrincipal(user, row.kind) : null;
}

export async function loginRoot(password) {
  await ensureRootAccount();
  const rootId = await kv().get(emailKey(ROOT_EMAIL));
  const root = await readUser(rootId);
  if (!root) return null;
  if (typeof password !== "string" || password.length === 0 || password.length > 256) return null;
  if (!verifyPassword(password, root.passwordHash, root.passwordSalt)) return null;
  return issueToken(root.id, "root", ACCESS_TOKEN_LIFETIME_MS);
}

async function findByIdentifier(identifier) {
  const trimmed = String(identifier ?? "").trim();
  if (trimmed.includes("@")) {
    const id = await kv().get(emailKey(trimmed));
    return readUser(id);
  }
  const id = await kv().get(nameKey(trimmed));
  return readUser(id);
}

export async function login(identifier, password) {
  await ensureRootAccount();
  if (typeof identifier !== "string" || identifier.trim().length === 0) return null;
  if (typeof password !== "string" || password.length === 0 || password.length > 256) return null;
  const user = await findByIdentifier(identifier);
  if (!user) {
    verifyPassword(password, dummyPassword.passwordHash, dummyPassword.passwordSalt);
    return null;
  }
  if (!verifyPassword(password, user.passwordHash, user.passwordSalt)) return null;
  const receipt = await issueToken(user.id, "web", WEB_TOKEN_LIFETIME_MS);
  return { receipt, principal: toPrincipal(user, "web") };
}

export async function register({ username, fullName, school, email, password }) {
  await ensureRootAccount();
  const normalizedUsername = normalizeUsername(username);
  const normalizedName = requireText(fullName, 120, "Введите ФИО.");
  const normalizedSchool = requireText(school, 160, "Введите школу или гимназию.");
  const normalizedEmail = normalizeEmail(requireText(email, 190, "Введите email."));
  if (!isValidEmail(normalizedEmail)) {
    throw new InvalidDataError("Укажите корректный email, например teacher@school.by.");
  }
  if (typeof password !== "string" || password.length < 6 || password.length > 128) {
    throw new InvalidDataError("Пароль должен быть от 6 до 128 символов.");
  }

  return withLock(async () => {
    if (await kv().get(nameKey(normalizedUsername))) {
      throw new InvalidDataError("Это имя аккаунта уже занято. Выберите другое.");
    }
    if (await kv().get(emailKey(normalizedEmail))) {
      throw new InvalidDataError("Аккаунт с таким email или именем уже зарегистрирован. Войдите в него.");
    }
    const id = await kv().incr(NEXT_USER_ID);
    const { passwordHash, passwordSalt } = hashPassword(password);
    const user = {
      id,
      username: normalizedUsername,
      email: normalizedEmail,
      passwordHash,
      passwordSalt,
      fullName: normalizedName,
      school: normalizedSchool,
      isAdmin: false,
      storagePlan: STORAGE.FREE,
      storageUntil: null,
      createdAt: iso(),
    };
    await setJson(userKey(id), user);
    await kv().set(emailKey(normalizedEmail), String(id));
    await kv().set(nameKey(normalizedUsername), String(id));
    const receipt = await issueToken(id, "web", WEB_TOKEN_LIFETIME_MS);
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
  await updateUser(user);
}

async function listSubscriptions(userId, now = new Date()) {
  const rows = (await getJson(subsKey(userId))) ?? [];
  return rows.filter((row) => parseDate(row.expiresAt)?.getTime() > now.getTime());
}

export async function getProfile(principal, usage) {
  const user = await readUser(principal.userId);
  if (!user) throw new UnauthorizedError("Аккаунт не найден. Войдите снова.");
  const now = new Date();
  const plan = effectivePlan(user, now);
  const until = parseDate(user.storageUntil);
  const planUntil = user.isAdmin || !until || until.getTime() <= now.getTime() ? null : until;
  const subscriptions = (await listSubscriptions(user.id, now)).map((row) => ({
    kind: row.kind,
    subject: row.subject,
    classNumber: row.classNumber ?? null,
    expiresAt: row.expiresAt,
  }));
  const storage = retentionsFor(plan).map((retention) => ({
    retention,
    used: usage.get(retention) ?? 0,
    limit: user.isAdmin ? UNLIMITED : limitFor(plan, retention),
  }));
  return {
    id: user.id,
    username: user.username,
    email: user.email,
    fullName: user.fullName,
    school: user.school,
    isAdmin: Boolean(user.isAdmin),
    plan,
    planDisplayName: planDisplayName(plan),
    planUntil: planUntil ? iso(planUntil) : null,
    subscriptions,
    storage,
  };
}

export async function updateProfile(principal, fullName, school) {
  const normalizedName = requireText(fullName, 120, "Введите ФИО.");
  const normalizedSchool = requireText(school, 160, "Введите школу или гимназию.");
  const user = await readUser(principal.userId);
  if (!user) throw new UnauthorizedError("Аккаунт не найден. Войдите снова.");
  user.fullName = normalizedName;
  user.school = normalizedSchool;
  await updateUser(user);
}

export async function hasCatalogAccess(principal, subject, classNumber) {
  const user = await readUser(principal.userId);
  if (!user) return false;
  if (user.isAdmin) return true;
  const now = new Date();
  for (const subscription of await listSubscriptions(user.id, now)) {
    if (subscription.kind === "all") return true;
    if (String(subscription.subject).toLowerCase() !== String(subject).toLowerCase()) continue;
    if (subscription.kind === "subject") return true;
    if (subscription.kind === "class" && subscription.classNumber === classNumber) return true;
  }
  return false;
}

export async function ensureCanStore(principal, retention, usage) {
  const user = await readUser(principal.userId);
  if (!user) throw new UnauthorizedError("Аккаунт не найден. Войдите снова.");
  if (user.isAdmin) return;
  const plan = effectivePlan(user);
  const limit = limitFor(plan, retention);
  if (limit === 0) {
    throw new HttpError(
      400,
      `Срок хранения «${retentionDisplayName(retention)}» недоступен на тарифе «${planDisplayName(plan)}». `
        + "Активируйте подходящий ключ в личном кабинете.",
    );
  }
  if (limit === UNLIMITED) return;
  const used = usage.get(retention) ?? 0;
  if (used >= limit) {
    throw new HttpError(
      400,
      `На тарифе «${planDisplayName(plan)}» можно хранить ${limit} задачников со сроком «${retentionShortLabel(retention)}». `
        + "Удалите ненужный задачник или активируйте тариф выше.",
    );
  }
}

function normalizeKeyCode(code) {
  let raw = String(code ?? "").trim().toUpperCase().replace(/-/g, "").replace(/ /g, "");
  if (raw.startsWith("PVZ")) raw = raw.slice(3);
  if (raw.length !== KEY_BODY_LENGTH || [...raw].some((character) => !KEY_ALPHABET.includes(character))) {
    throw new InvalidDataError("Ключ активации выглядит как PVZ-XXXX-XXXX-XXXX.");
  }
  return `PVZ-${raw.slice(0, 4)}-${raw.slice(4, 8)}-${raw.slice(8)}`;
}

function generateKeyCode() {
  const body = randomCode(KEY_BODY_LENGTH, KEY_ALPHABET);
  return `PVZ-${body.slice(0, 4)}-${body.slice(4, 8)}-${body.slice(8)}`;
}

export async function activateKey(principal, code) {
  const normalized = normalizeKeyCode(code);
  return withLock(async () => {
    const key = await getJson(keyKey(normalized));
    if (!key) throw new InvalidDataError("Ключ активации не найден. Проверьте код.");
    if (key.usedBy !== null && key.usedBy !== undefined) throw new InvalidDataError("Этот ключ уже был активирован.");
    const user = await readUser(principal.userId);
    if (!user) throw new UnauthorizedError("Аккаунт не найден. Войдите снова.");
    const now = new Date();
    key.usedBy = user.id;
    key.usedAt = iso(now);
    await setJson(keyKey(normalized), key);
    return applyKey(user, key, now);
  });
}

async function applyKey(user, key, now) {
  const days = Number(key.durationDays);
  switch (key.kind) {
    case "storage": {
      const target = key.storagePlan ?? STORAGE.PRO;
      const existingUntil = parseDate(user.storageUntil);
      const newUntil = user.storagePlan === target && existingUntil && existingUntil.getTime() > now.getTime()
        ? addDays(existingUntil, days)
        : addDays(now, days);
      user.storagePlan = target;
      user.storageUntil = iso(newUntil);
      await updateUser(user);
      return `Тариф «${planDisplayName(target)}» активирован до ${formatDateRu(newUntil)}.`;
    }
    case "subject": {
      const subject = key.subject ?? "informatics";
      const until = addDays(now, days);
      await addSubscription(user.id, { kind: "subject", subject, classNumber: null, createdAt: iso(now), expiresAt: iso(until) });
      return `Подписка на предмет «${displaySubject(subject)}» (все классы) активирована до ${formatDateRu(until)}.`;
    }
    case "all": {
      const until = addDays(now, days);
      await addSubscription(user.id, { kind: "all", subject: "", classNumber: null, createdAt: iso(now), expiresAt: iso(until) });
      return `Подписка на все предметы активирована до ${formatDateRu(until)}.`;
    }
    default: {
      const subject = key.subject ?? "informatics";
      const classNumber = key.classNumber ?? minimumClassNumber(subject);
      const until = addDays(now, days);
      await addSubscription(user.id, { kind: "class", subject, classNumber, createdAt: iso(now), expiresAt: iso(until) });
      return `Подписка на «${displaySubject(subject)}», ${classNumber} класс активирована до ${formatDateRu(until)}.`;
    }
  }
}

async function addSubscription(userId, row) {
  const rows = (await getJson(subsKey(userId))) ?? [];
  rows.push(row);
  await setJson(subsKey(userId), rows);
}

export async function createKey(principal, { kind, storagePlan, durationDays, subject, classNumber }) {
  const normalizedKind = String(kind ?? "").trim().toLowerCase();
  if (!["storage", "subject", "class", "all"].includes(normalizedKind)) {
    throw new InvalidDataError("Тип ключа должен быть storage, subject, class или all.");
  }
  if (!Number.isInteger(durationDays) || durationDays < 1 || durationDays > 3650) {
    throw new InvalidDataError("Срок действия ключа — от 1 до 3650 дней.");
  }

  let normalizedPlan = null;
  let normalizedSubject = null;
  let normalizedClass = null;
  switch (normalizedKind) {
    case "storage":
      normalizedPlan = String(storagePlan ?? "").trim().toLowerCase();
      if (![STORAGE.PRO, STORAGE.PRO_PLUS].includes(normalizedPlan)) {
        throw new InvalidDataError("Тариф ключа хранения — pro или pro_plus.");
      }
      break;
    case "subject":
      normalizedSubject = normalizeSubject(subject);
      if (!normalizedSubject) throw new InvalidDataError("Неизвестный предмет.");
      break;
    case "class":
      normalizedSubject = normalizeSubject(subject);
      if (!normalizedSubject) throw new InvalidDataError("Неизвестный предмет.");
      if (!Number.isInteger(classNumber) || !isValidClass(normalizedSubject, classNumber)) {
        throw new InvalidDataError("Для выбранного предмета этот класс недоступен.");
      }
      normalizedClass = classNumber;
      break;
    default:
      break;
  }

  for (let attempt = 0; attempt < MAXIMUM_KEY_ATTEMPTS; attempt++) {
    const code = generateKeyCode();
    const created = await withLock(async () => {
      if (await kv().get(keyKey(code))) return false;
      const now = iso();
      await setJson(keyKey(code), {
        code,
        kind: normalizedKind,
        storagePlan: normalizedPlan,
        durationDays,
        subject: normalizedSubject,
        classNumber: normalizedClass,
        createdAt: now,
        createdBy: principal.userId,
        usedBy: null,
        usedAt: null,
      });
      const codes = (await getJson(KEYCODES)) ?? [];
      codes.unshift(code);
      await setJson(KEYCODES, codes.slice(0, 2000));
      return true;
    });
    if (created) {
      return {
        code,
        kind: normalizedKind,
        storagePlan: normalizedPlan,
        durationDays,
        subject: normalizedSubject,
        classNumber: normalizedClass,
        createdAt: iso(),
        used: false,
        usedAt: null,
      };
    }
  }

  throw new InvalidDataError("Не удалось создать уникальный ключ. Попробуйте ещё раз.");
}

export async function listKeys() {
  const codes = (await getJson(KEYCODES)) ?? [];
  const rows = [];
  for (const code of codes.slice(0, 200)) {
    const key = await getJson(keyKey(code));
    if (!key) continue;
    rows.push({
      code: key.code,
      kind: key.kind,
      storagePlan: key.storagePlan ?? null,
      durationDays: key.durationDays,
      subject: key.subject ?? null,
      classNumber: key.classNumber ?? null,
      createdAt: key.createdAt,
      used: key.usedBy !== null && key.usedBy !== undefined,
      usedAt: key.usedAt ?? null,
    });
  }
  return rows;
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
