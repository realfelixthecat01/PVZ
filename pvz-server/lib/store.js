// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import {
  MAX_PACKAGE_BYTES,
  MAX_EXCHANGE_FILE_BYTES,
  MAX_SESSION_EXCHANGE_BYTES,
  MAX_SCREEN_FRAME_BYTES,
  SCREEN_WATCH_TIMEOUT_MS,
  STUDENT_ONLINE_MS,
  SESSION_LIFETIME_MS,
} from "./config.js";
import { blob } from "./storage.js";
import { getJson, setJson, withLock, deleteJson, mgetJson } from "./storage.js";
import { loadPackage, getTaskMaxScores, getDocumentMaxScore } from "./pvzPackage.js";
import {
  RETENTION,
  retentionLifetimeMs,
  normalizeSubject,
  isValidClass,
  catalogPrefix,
  catalogIndexKey,
} from "./limits.js";
import { findUserById, GUEST_MAX_SESSIONS } from "./accounts.js";
import { shortTitle } from "./reporting.js";
import {
  HttpError,
  InvalidDataError,
  base64Url,
  fixedTimeEqualHex,
  isSessionId,
  isTokenShaped,
  randomCode,
  randomId,
  sha256Hex,
  sanitizeFileName,
  requireText,
  iso,
  parseDate,
} from "./util.js";
import crypto from "node:crypto";

const SESSIONS_INDEX = "pvz:sessions";
const sessionKey = (id) => `pvz:session:${id}`;
const studentKey = (ssid) => `pvz:student:${ssid}`;
const historyKey = (userId) => `pvz:hist:${userId}`;
// История ученика: последние задачники с результатами, старое вытесняется.
const HISTORY_LIMIT = 60;
const watchKey = (ssid) => `pvz:watch:${ssid}`;
const frameKey = (ssid) => `pvz:frame:${ssid}`;
const packagePath = (sessionId) => `pvz/sessions/${sessionId}/taskbook.pvz`;
const exchangePath = (sessionId, fileId) => `pvz/sessions/${sessionId}/files/${fileId}`;
// KV чистим с запасом: запись должна пережить свой expiresAt, чтобы её успел
// разобрать cron и учитель мог посмотреть итоговый отчёт.
const KV_GRACE_MS = 24 * 60 * 60 * 1000;

const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

function sessionLifetimeMs() {
  // Тестовый сервер может сокращать срок жизни сессии, как PVZ_SESSION_LIFETIME в C#.
  const raw = process.env.PVZ_SESSION_LIFETIME_SECONDS;
  if (raw) {
    const seconds = Number(raw);
    if (Number.isFinite(seconds) && seconds >= 1 && seconds < SESSION_LIFETIME_MS / 1000) {
      return seconds * 1000;
    }
  }
  return SESSION_LIFETIME_MS;
}

function clampLifetime(lifetime) {
  const limit = sessionLifetimeMs();
  return limit < SESSION_LIFETIME_MS ? Math.min(lifetime, limit) : lifetime;
}

function sessionRetention(session) {
  return session.retention ?? RETENTION.TWO_WEEKS;
}

function normalizeToken(token) {
  if (typeof token !== "string" || token.length !== 6) {
    throw new InvalidDataError("Токен должен состоять из 6 символов.");
  }
  const upper = token.toUpperCase();
  if ([...upper].some((character) => !ALPHABET.includes(character))) {
    throw new InvalidDataError("Токен должен состоять из 6 символов.");
  }
  return upper;
}

async function readSession(id) {
  if (!isSessionId(id)) return null;
  return getJson(sessionKey(id));
}

async function writeSession(session) {
  const ttl = Math.max(
    60_000,
    (parseDate(session.expiresAt)?.getTime() ?? Date.now()) - Date.now() + KV_GRACE_MS,
  );
  await setJson(sessionKey(session.id), session, ttl);
}

async function readIndex() {
  return (await getJson(SESSIONS_INDEX)) ?? [];
}

async function writeIndex(ids) {
  await setJson(SESSIONS_INDEX, ids);
}

async function loadSessions(ids) {
  const docs = await mgetJson(ids.map(sessionKey));
  return docs.filter(Boolean);
}

async function loadAllSessions() {
  return loadSessions(await readIndex());
}

function isActive(session, now = new Date()) {
  const expiresAt = parseDate(session?.expiresAt);
  return Boolean(expiresAt && expiresAt.getTime() > now.getTime());
}

// каталог

function validateCatalogClass(subject, classNumber) {
  const normalized = normalizeSubject(subject);
  if (!normalized) throw new InvalidDataError("Неизвестный предмет каталога.");
  if (!isValidClass(normalized, classNumber)) throw new InvalidDataError("Для выбранного предмета этот класс недоступен.");
  return normalized;
}

export async function listCatalogTaskbooks(subject, classNumber) {
  const normalized = validateCatalogClass(subject, classNumber);
  const cached = await getJson(catalogIndexKey(normalized, classNumber));
  if (Array.isArray(cached)) return cached;
  const prefix = catalogPrefix(normalized, classNumber);
  const items = (await blob().list(prefix))
    .filter((item) => item.pathname.toLowerCase().endsWith(".pvz") && item.size > 0 && item.size <= MAX_PACKAGE_BYTES)
    .map((item) => ({ fileName: item.pathname.slice(prefix.length), sizeBytes: item.size }))
    .sort((a, b) => a.fileName.localeCompare(b.fileName, "ru", { sensitivity: "base" }));
  await setJson(catalogIndexKey(normalized, classNumber), items, 10 * 60 * 1000);
  return items;
}

export async function downloadCatalogTaskbook(subject, classNumber, fileName) {
  const normalized = validateCatalogClass(subject, classNumber);
  if (typeof fileName !== "string" || fileName.length === 0 || fileName !== fileName.split(/[\\/]/).pop()) return null;
  if (!fileName.toLowerCase().endsWith(".pvz")) return null;
  const buffer = await blob().getBuffer(catalogPrefix(normalized, classNumber) + fileName);
  if (!buffer || buffer.length === 0 || buffer.length > MAX_PACKAGE_BYTES) return null;
  loadPackage(buffer);
  return buffer;
}

// сессии

async function uniqueToken(existingSessions) {
  const taken = new Set(existingSessions.map((session) => session.tokenHash));
  for (let attempt = 0; attempt < 50; attempt++) {
    const token = randomCode(6, ALPHABET);
    const hash = sha256Hex(token);
    if (!taken.has(hash)) return { token, tokenHash: hash };
  }
  throw new HttpError(503, "Не удалось создать токен задачника. Попробуйте ещё раз.");
}

async function createSessionFromPackage(packageBuffer, {
  retention,
  ownerUserId = null,
  sessionId = randomId(),
  storePackage = true,
}) {
  if (!packageBuffer || packageBuffer.length === 0 || packageBuffer.length > MAX_PACKAGE_BYTES) {
    throw new InvalidDataError("Размер .pvz файла недопустим.");
  }
  const document = loadPackage(packageBuffer);

  return withLock(async () => {
    const sessions = await loadAllSessions();
    const { token, tokenHash } = await uniqueToken(sessions);
    const now = new Date();
    const path = packagePath(sessionId);
    if (storePackage) await blob().put(path, packageBuffer, "application/octet-stream");

    const session = {
      id: sessionId,
      tokenHash,
      tokenHint: `${token.slice(0, 2)}****`,
      uploadedAt: iso(now),
      expiresAt: iso(new Date(now.getTime() + clampLifetime(retentionLifetimeMs(retention)))),
      totalTasks: document.tasks.length,
      maximumPoints: getDocumentMaxScore(document),
      retention,
      ownerUserId,
      subject: typeof document.subject === "string" ? document.subject : "",
      classNumber: Number.isInteger(document.classNumber) ? document.classNumber : null,
      packagePath: path,
      taskMaxScores: getTaskMaxScores(document),
      taskTitles: document.tasks.map((task) => shortTitle(task.description)),
      students: [],
      files: [],
    };
    try {
      await writeSession(session);
      const ids = await readIndex();
      ids.push(session.id);
      await writeIndex(ids);
    } catch (error) {
      if (storePackage) await blob().del(path);
      throw error;
    }
    return {
      id: session.id,
      token,
      expiresAt: session.expiresAt,
      retention,
    };
  });
}

export async function uploadPackageBuffer(packageBuffer, {
  retention,
  ownerUserId,
  sessionId,
  packageAlreadyStored = false,
}) {
  return createSessionFromPackage(packageBuffer, {
    retention,
    ownerUserId,
    sessionId: sessionId ?? randomId(),
    storePackage: !packageAlreadyStored,
  });
}

export function toSessionDto(session, now = new Date()) {
  return {
    id: session.id,
    tokenHint: session.tokenHint,
    uploadedAt: session.uploadedAt,
    expiresAt: session.expiresAt,
    totalTasks: session.totalTasks,
    maximumPoints: session.maximumPoints,
    retention: sessionRetention(session),
    students: [...session.students]
      .sort((a, b) => (parseDate(b.lastActivityAt)?.getTime() ?? 0) - (parseDate(a.lastActivityAt)?.getTime() ?? 0))
      .map((student) => {
        const startedAt = parseDate(student.startedAt);
        const lastActivity = parseDate(student.lastActivityAt);
        return {
          firstName: student.firstName,
          lastName: student.lastName,
          className: student.className,
          startedAt: student.startedAt,
          lastActivityAt: student.lastActivityAt,
          elapsedSeconds: Math.max(0, Math.floor((lastActivity.getTime() - startedAt.getTime()) / 1000)),
          earnedPoints: student.earnedPoints.reduce((sum, value) => sum + value, 0),
          maximumPoints: session.maximumPoints,
          completedTasks: student.completedTasks.filter(Boolean).length,
          totalTasks: session.totalTasks,
          isOnline: now.getTime() - lastActivity.getTime() <= STUDENT_ONLINE_MS,
          tasks: student.earnedPoints.map((points, index) => ({
            earnedPoints: points,
            completed: Boolean(student.completedTasks[index]),
          })),
          sessionId: student.sessionId,
        };
      }),
    ownerId: session.ownerUserId ?? null,
    ownerName: null,
  };
}

const uploadedDesc = (a, b) => (parseDate(b.uploadedAt)?.getTime() ?? 0) - (parseDate(a.uploadedAt)?.getTime() ?? 0);

async function listSessions(filter) {
  const now = new Date();
  const sessions = (await loadAllSessions()).filter((session) => isActive(session, now) && filter(session));
  return sessions.sort(uploadedDesc).map((session) => toSessionDto(session, now));
}

export async function listAllSessions() {
  return listSessions(() => true);
}

export async function listOwnedSessions(userId) {
  return listSessions((session) => session.ownerUserId === userId);
}

function countUsage(sessions, userId, now = new Date()) {
  const usage = new Map();
  for (const session of sessions) {
    if (session.ownerUserId !== userId || !isActive(session, now)) continue;
    const retention = sessionRetention(session);
    usage.set(retention, (usage.get(retention) ?? 0) + 1);
  }
  return usage;
}

export async function getOwnerUsage(userId) {
  const sessions = (await loadAllSessions()).filter((session) => isActive(session));
  return countUsage(sessions, userId);
}

export async function listOwnerFiles(userId) {
  const now = new Date();
  return (await loadAllSessions())
    .filter((session) => session.ownerUserId === userId && isActive(session, now))
    .sort(uploadedDesc)
    .map((session) => ({
      id: session.id,
      tokenHint: session.tokenHint,
      uploadedAt: session.uploadedAt,
      expiresAt: session.expiresAt,
      retention: sessionRetention(session),
      totalTasks: session.totalTasks,
      maximumPoints: session.maximumPoints,
      studentsCount: session.students.length,
    }));
}

export async function extendSession(sessionId, ownerUserId = undefined) {
  return withLock(async () => {
    const session = await readSession(sessionId);
    if (!isActive(session)) return null;
    if (ownerUserId !== undefined && session.ownerUserId !== ownerUserId) return null;
    const now = new Date();
    session.expiresAt = iso(new Date(now.getTime() + clampLifetime(retentionLifetimeMs(sessionRetention(session)))));
    await writeSession(session);
    return session.expiresAt;
  });
}

export async function closeSession(sessionId, ownerUserId = undefined) {
  return withLock(async () => {
    const session = await readSession(sessionId);
    if (!session) return false;
    if (ownerUserId !== undefined && session.ownerUserId !== ownerUserId) return false;
    await removeSessionData(session);
    return true;
  });
}

async function removeSessionData(session) {
  await deleteJson(sessionKey(session.id));
  const ids = await readIndex();
  await writeIndex(ids.filter((id) => id !== session.id));
  for (const student of session.students ?? []) await deleteJson(studentKey(student.sessionId));
  await blob().delPrefix(`pvz/sessions/${session.id}/`);
}

export async function downloadSessionPackage(sessionId, ownerUserId = undefined) {
  const session = await readSession(sessionId);
  if (!isActive(session)) return null;
  if (ownerUserId !== undefined && session.ownerUserId !== ownerUserId) return null;
  return blob().getBuffer(session.packagePath ?? packagePath(session.id));
}

export async function getSessionForReport(principal, sessionId) {
  const session = await readSession(sessionId);
  if (!isActive(session) || !canManage(principal, session)) return null;
  return session;
}

export async function downloadPackageByToken(token) {
  const tokenHash = sha256Hex(normalizeToken(token));
  const sessions = await loadAllSessions();
  const session = sessions.find((item) => fixedTimeEqualHex(item.tokenHash, tokenHash));
  if (!isActive(session)) return null;
  return blob().getBuffer(session.packagePath ?? packagePath(session.id));
}

// ученики

export async function startStudentSession(token, student, accountUserId = null) {
  if (!student || typeof student !== "object") return null;
  const firstName = requireText(student.firstName, 60, "Введите имя.");
  const lastName = requireText(student.lastName, 60, "Введите фамилию.");
  const className = requireText(student.className, 30, "Введите класс.");
  const tokenHash = sha256Hex(normalizeToken(token));

  return withLock(async () => {
    const sessions = await loadAllSessions();
    const session = sessions.find((item) => fixedTimeEqualHex(item.tokenHash, tokenHash));
    if (!isActive(session)) return null;
    const now = new Date();
    const sessionId = base64Url(crypto.randomBytes(32));
    session.students.push({
      sessionId,
      firstName,
      lastName,
      className,
      accountUserId: Number.isInteger(accountUserId) && accountUserId > 0 ? accountUserId : null,
      startedAt: iso(now),
      lastActivityAt: iso(now),
      earnedPoints: new Array(session.totalTasks).fill(0),
      completedTasks: new Array(session.totalTasks).fill(false),
    });
    await writeSession(session);
    await setJson(studentKey(sessionId), { sessionId: session.id }, KV_GRACE_MS + sessionLifetimeMs());
    return { sessionId, startedAt: iso(now) };
  });
}

async function findStudent(ssid) {
  // Идентификатор сессии ученика выдаёт сервер, чужой формат сюда прийти не может.
  if (!isTokenShaped(ssid)) return null;
  const link = await getJson(studentKey(ssid));
  if (!link) return null;
  const session = await readSession(link.sessionId);
  if (!session) return null;
  const student = session.students.find((item) => item.sessionId === ssid);
  return student ? { session, student } : null;
}

export async function reportProgress(update) {
  if (!update || typeof update.sessionId !== "string" || !Array.isArray(update.tasks)) return false;
  if (update.tasks.length > 1000) return false;
  return withLock(async () => {
    const found = await findStudent(update.sessionId);
    if (!found || !isActive(found.session)) return false;
    const { session, student } = found;
    if (update.tasks.length !== session.totalTasks) return false;
    const maxScores = session.taskMaxScores ?? [];
    for (let index = 0; index < update.tasks.length; index++) {
      const task = update.tasks[index];
      if (!task || !Number.isInteger(task.earnedPoints) || task.earnedPoints < 0 || task.earnedPoints > (maxScores[index] ?? 0)) {
        return false;
      }
    }
    student.earnedPoints = update.tasks.map((task) => task.earnedPoints);
    student.completedTasks = update.tasks.map((task) => Boolean(task.completed));
    student.lastActivityAt = iso();
    await writeSession(session);
    if (student.accountUserId) await upsertStudentHistory(student.accountUserId, session, student);
    return true;
  });
}

async function upsertStudentHistory(userId, session, student) {
  const rows = (await getJson(historyKey(userId))) ?? [];
  const row = {
    sessionId: session.id,
    ssid: student.sessionId,
    subject: session.subject ?? "",
    classNumber: session.classNumber ?? null,
    uploadedAt: session.uploadedAt,
    startedAt: student.startedAt,
    updatedAt: iso(),
    earnedPoints: student.earnedPoints.reduce((sum, value) => sum + value, 0),
    maximumPoints: session.maximumPoints,
    completedTasks: student.completedTasks.filter(Boolean).length,
    totalTasks: session.totalTasks,
    ownerId: session.ownerUserId ?? null,
  };
  const existing = rows.findIndex((item) => item.ssid === row.ssid);
  if (existing >= 0) rows[existing] = row; else rows.unshift(row);
  await setJson(historyKey(userId), rows.slice(0, HISTORY_LIMIT));
}

export async function listStudentHistory(userId) {
  const rows = (await getJson(historyKey(userId))) ?? [];
  const ownerIds = [...new Set(rows.map((row) => row.ownerId).filter((id) => id !== null && id !== undefined))];
  const names = new Map();
  for (const ownerId of ownerIds) {
    const owner = await findUserById(ownerId);
    if (owner) names.set(ownerId, owner.fullName?.trim() ? owner.fullName : owner.username);
  }
  return rows
    .sort((a, b) => (parseDate(b.updatedAt)?.getTime() ?? 0) - (parseDate(a.updatedAt)?.getTime() ?? 0))
    .map((row) => ({ ...row, teacherName: names.get(row.ownerId) ?? "" }));
}

export async function assertGuestSessionLimit(ownerUserId) {
  const owned = (await listOwnedSessions(ownerUserId)).length;
  if (owned >= GUEST_MAX_SESSIONS) {
    throw new HttpError(
      400,
      "В гостевом режиме активен только один задачник. Закройте его на вкладке «Сервер» и попробуйте снова.",
    );
  }
}

// просмотр экрана

function canManage(principal, session) {
  return Boolean(principal)
    && (principal.isAdmin || principal.kind === "root" || session.ownerUserId === principal.userId);
}

export async function beginScreenWatch(principal, ssid) {
  const found = await findStudent(ssid);
  if (!found || !isActive(found.session) || !canManage(principal, found.session)) return false;
  await setJson(watchKey(ssid), { requestedAt: iso() }, SCREEN_WATCH_TIMEOUT_MS);
  return true;
}

export async function endScreenWatch(principal, ssid) {
  const found = await findStudent(ssid);
  if (!found || !canManage(principal, found.session)) return;
  await deleteJson(watchKey(ssid));
  await deleteJson(frameKey(ssid));
}

async function isWatchActive(ssid) {
  const watch = await getJson(watchKey(ssid));
  if (!watch) return false;
  const requestedAt = parseDate(watch.requestedAt);
  return Boolean(requestedAt) && Date.now() - requestedAt.getTime() <= SCREEN_WATCH_TIMEOUT_MS;
}

export async function getScreenFrame(principal, ssid) {
  const found = await findStudent(ssid);
  if (!found || !canManage(principal, found.session)) return null;
  if (!(await isWatchActive(ssid))) return null;
  await setJson(watchKey(ssid), { requestedAt: iso() }, SCREEN_WATCH_TIMEOUT_MS);
  const frame = await getJson(frameKey(ssid));
  return frame?.data ? Buffer.from(frame.data, "base64") : null;
}

export async function isScreenRequested(ssid) {
  const found = await findStudent(ssid);
  if (!found || !isActive(found.session)) {
    await deleteJson(watchKey(ssid));
    await deleteJson(frameKey(ssid));
    return null;
  }
  return isWatchActive(ssid);
}

export async function storeScreenFrame(ssid, frame) {
  if (!frame || frame.length === 0 || frame.length > MAX_SCREEN_FRAME_BYTES) return false;
  const found = await findStudent(ssid);
  if (!found || !isActive(found.session)) {
    await deleteJson(watchKey(ssid));
    await deleteJson(frameKey(ssid));
    return null;
  }
  if (!(await isWatchActive(ssid))) return false;
  await setJson(frameKey(ssid), { data: frame.toString("base64") }, 60 * 1000);
  return true;
}

export async function stopScreenShare(ssid) {
  if (!isTokenShaped(ssid)) return;
  await deleteJson(watchKey(ssid));
  await deleteJson(frameKey(ssid));
}

// пересылка файлов

function validateExchangeSize(sizeBytes) {
  if (!Number.isInteger(sizeBytes) || sizeBytes <= 0 || sizeBytes > MAX_EXCHANGE_FILE_BYTES) {
    throw new InvalidDataError("Размер файла должен быть от 1 байта до 32 МиБ.");
  }
}

function toExchangeDto(record) {
  return {
    id: record.id,
    name: record.name,
    kind: record.kind,
    sizeBytes: record.sizeBytes,
    studentName: record.studentName ?? null,
    uploadedAt: record.uploadedAt,
  };
}

function sortExchangeDtos(records) {
  return [...records]
    .sort((a, b) => (parseDate(b.uploadedAt)?.getTime() ?? 0) - (parseDate(a.uploadedAt)?.getTime() ?? 0))
    .map(toExchangeDto);
}

export async function addSessionFile(principal, sessionId, file) {
  return withLock(async () => {
    const session = await readSession(sessionId);
    if (!isActive(session) || !canManage(principal, session)) return null;
    return addExchangeRecord(session, "material", file, null, null);
  });
}

export async function addStudentFile(ssid, file) {
  const found = await findStudent(ssid);
  if (!found || !isActive(found.session)) return null;
  const studentName = `${found.student.firstName} ${found.student.lastName} (${found.student.className})`;
  return withLock(async () => {
    const session = await readSession(found.session.id);
    if (!isActive(session)) return null;
    return addExchangeRecord(session, "work", file, ssid, studentName);
  });
}

async function addExchangeRecord(session, kind, file, studentSessionId, studentName) {
  validateExchangeSize(file.sizeBytes);
  const total = session.files.reduce((sum, record) => sum + record.sizeBytes, 0);
  if (total + file.sizeBytes > MAX_SESSION_EXCHANGE_BYTES) {
    throw new InvalidDataError("Лимит файлов задачника 512 МиБ. Удалите лишние файлы и повторите.");
  }
  const record = {
    id: file.id ?? randomId(),
    name: sanitizeFileName(file.name),
    kind,
    studentSessionId,
    studentName,
    sizeBytes: file.sizeBytes,
    uploadedAt: iso(),
    pathname: file.pathname ?? exchangePath(session.id, file.id),
  };
  session.files.push(record);
  await writeSession(session);
  return toExchangeDto(record);
}

export async function listSessionFiles(principal, sessionId) {
  const session = await readSession(sessionId);
  if (!isActive(session) || !canManage(principal, session)) return null;
  return sortExchangeDtos(session.files);
}

export async function listStudentFiles(ssid) {
  const found = await findStudent(ssid);
  if (!found || !isActive(found.session)) return null;
  const materials = found.session.files.filter((record) => record.kind === "material");
  const works = found.session.files.filter(
    (record) => record.kind === "work" && record.studentSessionId === ssid,
  );
  return { materials: sortExchangeDtos(materials), works: sortExchangeDtos(works) };
}

async function openExchangeFile(session, fileId, allowWorks, ssid) {
  if (!isSessionId(fileId)) return null;
  const record = session.files.find((item) => item.id === fileId);
  if (!record) return null;
  if (!allowWorks && record.kind === "work" && record.studentSessionId !== ssid) return null;
  const content = await blob().getBuffer(record.pathname ?? exchangePath(session.id, fileId));
  if (!content || content.length !== record.sizeBytes) return null;
  return { info: toExchangeDto(record), content };
}

export async function openSessionFile(principal, sessionId, fileId) {
  const session = await readSession(sessionId);
  if (!isActive(session) || !canManage(principal, session)) return null;
  return openExchangeFile(session, fileId, true, null);
}

export async function openStudentFile(ssid, fileId) {
  const found = await findStudent(ssid);
  if (!found || !isActive(found.session)) return null;
  return openExchangeFile(found.session, fileId, false, ssid);
}

export async function deleteSessionFile(principal, sessionId, fileId) {
  return withLock(async () => {
    const session = await readSession(sessionId);
    if (!isActive(session) || !canManage(principal, session)) return false;
    if (!isSessionId(fileId)) return false;
    const record = session.files.find((item) => item.id === fileId);
    if (!record) return false;
    session.files = session.files.filter((item) => item.id !== fileId);
    await writeSession(session);
    await blob().del(record.pathname ?? exchangePath(session.id, fileId));
    return true;
  });
}

// очистка

export async function purgeExpired() {
  const removed = [];
  await withLock(async () => {
    const now = new Date();
    const sessions = await loadAllSessions();
    const keep = [];
    for (const session of sessions) {
      if (isActive(session, now)) keep.push(session.id);
      else removed.push(session);
    }
    if (removed.length > 0) {
      await writeIndex(keep);
      for (const session of removed) await removeSessionData(session);
    }
  });
  return removed.length;
}
