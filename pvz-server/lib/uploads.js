// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import {
  MAX_PACKAGE_BYTES,
  MAX_EXCHANGE_FILE_BYTES,
  UPLOAD_TTL_MS,
} from "./config.js";
import { blob } from "./storage.js";
import { getJson, setJson, deleteJson } from "./storage.js";
import { RETENTION, RETENTION_NAMES } from "./limits.js";
import { addSessionFile, addStudentFile, uploadPackageBuffer, listSessionFiles, assertGuestSessionLimit } from "./store.js";
import {
  HttpError,
  InvalidDataError,
  UnauthorizedError,
  randomId,
  iso,
  parseDate,
  isSessionId,
  isTokenShaped,
} from "./util.js";

// Протокол загрузки: begin выдаёт ссылку, PUT уходит в хранилище, complete оформляет файл.
// Тело запроса к функции Vercel ограничено 4,5 МБ, поэтому крупные файлы идут
// напрямую в Blob по краткосрочной presigned-ссылке.

const UPLOADS_INDEX = "pvz:uploads";
const uploadKey = (id) => `pvz:upload:${id}`;
const sessionPackagePath = (sessionId) => `pvz/sessions/${sessionId}/taskbook.pvz`;
const exchangePath = (sessionId, fileId) => `pvz/sessions/${sessionId}/files/${fileId}`;

function parseRetentionName(raw) {
  const name = String(raw ?? "").trim();
  const entry = Object.entries(RETENTION_NAMES).find(([, value]) => value.toLowerCase() === name.toLowerCase());
  if (!entry) return null;
  return Number(entry[0]);
}

function requireSize(size, maximum) {
  if (!Number.isInteger(size) || size <= 0 || size > maximum) return false;
  return true;
}

async function addPending(record) {
  await setJson(uploadKey(record.uploadId), record, UPLOAD_TTL_MS);
  const ids = (await getJson(UPLOADS_INDEX)) ?? [];
  ids.push(record.uploadId);
  await setJson(UPLOADS_INDEX, ids);
}

async function removePending(uploadId) {
  await deleteJson(uploadKey(uploadId));
  const ids = (await getJson(UPLOADS_INDEX)) ?? [];
  if (ids.includes(uploadId)) await setJson(UPLOADS_INDEX, ids.filter((id) => id !== uploadId));
}

// Запись живёт UPLOAD_TTL_MS: за это время файл должен успеть лечь в хранилище,
// иначе complete его не найдёт и cron подчистит ссылку.
async function issuePending(signed, record) {
  await addPending({
    uploadId: signed.uploadId,
    ...record,
    expiresAt: iso(new Date(Date.now() + UPLOAD_TTL_MS)),
  });
  return { uploadId: signed.uploadId, uploadUrl: signed.uploadUrl, headers: signed.headers };
}

async function presign(pathname, maximum) {
  return blob().presign(pathname, { contentType: "application/octet-stream", maxBytes: maximum });
}

export async function beginUpload(principal, body) {
  const target = String(body?.target ?? "");

  switch (target) {
    case "teacher-session": {
      if (!principal) throw new UnauthorizedError("Требуется вход учителя.");
      if (!requireSize(body.size, MAX_PACKAGE_BYTES)) throw new InvalidDataError("Размер .pvz должен быть от 1 байта до 16 МиБ.");
      if (principal.isGuest) await assertGuestSessionLimit(principal.userId);
      const sessionId = randomId();
      const pathname = sessionPackagePath(sessionId);
      const signed = await presign(pathname, MAX_PACKAGE_BYTES);
      return issuePending(signed, {
        target,
        size: body.size,
        sessionId,
        retention: principal.isGuest ? RETENTION.HOURS_48 : RETENTION.TWO_WEEKS,
        ownerUserId: principal.kind === "root" ? null : principal.userId,
        pathname,
      });
    }

    case "account-file": {
      if (!principal) throw new UnauthorizedError("Требуется вход в личный кабинет.");
      if (principal.isGuest) {
        throw new HttpError(400, "В гостевом режиме личное хранилище недоступно. Создайте аккаунт учителя в приложении.");
      }
      if (!requireSize(body.size, MAX_PACKAGE_BYTES)) throw new InvalidDataError("Прикрепите один файл .pvz размером до 16 МиБ.");
      const retention = parseRetentionName(body.retention);
      if (retention === null) throw new InvalidDataError("Выберите срок хранения задачника.");
      const sessionId = randomId();
      const pathname = sessionPackagePath(sessionId);
      const signed = await presign(pathname, MAX_PACKAGE_BYTES);
      return issuePending(signed, {
        target,
        size: body.size,
        sessionId,
        retention,
        ownerUserId: principal.userId,
        pathname,
      });
    }

    case "teacher-material": {
      if (!principal) throw new UnauthorizedError("Требуется вход учителя.");
      if (!requireSize(body.size, MAX_EXCHANGE_FILE_BYTES)) {
        throw new InvalidDataError("Размер файла должен быть от 1 байта до 32 МиБ.");
      }
      const sessionId = String(body.sessionId ?? "");
      if (!isSessionId(sessionId) || (await listSessionFiles(principal, sessionId)) === null) {
        throw new HttpError(404, "Задачник не найден или закрыт.");
      }
      const fileId = randomId();
      const pathname = exchangePath(sessionId, fileId);
      const signed = await presign(pathname, MAX_EXCHANGE_FILE_BYTES);
      return issuePending(signed, {
        target,
        size: body.size,
        name: body.name ?? "",
        sessionId,
        fileId,
        pathname,
        ownerUserId: principal.userId,
      });
    }

    case "student-work": {
      if (!requireSize(body.size, MAX_EXCHANGE_FILE_BYTES)) {
        throw new InvalidDataError("Размер файла должен быть от 1 байта до 32 МиБ.");
      }
      const studentSessionId = String(body.studentSessionId ?? "");
      if (studentSessionId.length === 0) throw new InvalidDataError("Не указана сессия ученика.");
      const link = isTokenShaped(studentSessionId) ? await getJson(`pvz:student:${studentSessionId}`) : null;
      if (!link) throw new HttpError(404, "Сессия ученика не найдена.");
      const fileId = randomId();
      const pathname = exchangePath(link.sessionId, fileId);
      const signed = await presign(pathname, MAX_EXCHANGE_FILE_BYTES);
      return issuePending(signed, {
        target,
        size: body.size,
        name: body.name ?? "",
        sessionId: link.sessionId,
        studentSessionId,
        fileId,
        pathname,
      });
    }

    default:
      throw new InvalidDataError("Неизвестный тип загрузки.");
  }
}

async function readPending(uploadId, principal) {
  const record = await getJson(uploadKey(uploadId));
  if (!record) throw new HttpError(404, "Загрузка не найдена или истекла. Повторите попытку.");
  if (record.ownerUserId !== null && record.ownerUserId !== undefined) {
    if (!principal || principal.userId !== record.ownerUserId) throw new UnauthorizedError("Загрузка принадлежит другому аккаунту.");
  }
  const expiresAt = parseDate(record.expiresAt);
  if (!expiresAt || expiresAt.getTime() <= Date.now()) {
    await removePending(uploadId);
    throw new HttpError(404, "Загрузка не найдена или истекла. Повторите попытку.");
  }
  return record;
}

export async function putUpload(uploadId, buffer) {
  // uploadId выдаёт сервер, локальный бэкенд по нему же пишет файл на диск.
  if (!isSessionId(uploadId)) throw new HttpError(404, "Загрузка не найдена или истекла. Повторите попытку.");
  const record = await getJson(uploadKey(uploadId));
  if (!record) throw new HttpError(404, "Загрузка не найдена или истекла. Повторите попытку.");
  if (!buffer || buffer.length !== record.size) {
    throw new InvalidDataError("Размер загруженного файла не совпал.");
  }
  await blob().writeUpload(uploadId, buffer);
  return { ok: true };
}

async function fetchUploaded(record) {
  if (blob().remote) {
    const buffer = await blob().getBuffer(record.pathname);
    if (!buffer) throw new InvalidDataError("Файл не загружен. Повторите попытку.");
    if (buffer.length !== record.size) {
      await blob().del(record.pathname);
      throw new InvalidDataError("Размер загруженного файла не совпал.");
    }
    return buffer;
  }
  // Локальный бэкенд: файл лежит в uploads/{uploadId}, переносим на финальный путь.
  const buffer = await blob().readUpload(record.uploadId);
  if (!buffer) throw new InvalidDataError("Файл не загружен. Повторите попытку.");
  if (buffer.length !== record.size) {
    await blob().removeUpload(record.uploadId);
    throw new InvalidDataError("Размер загруженного файла не совпал.");
  }
  await blob().promoteUpload(record.uploadId, record.pathname);
  return buffer;
}

export async function completeUpload(principal, uploadId) {
  const record = await readPending(uploadId, principal);

  switch (record.target) {
    case "teacher-session":
    case "account-file": {
      const packageBuffer = await fetchUploaded(record);
      try {
        const receipt = await uploadPackageBuffer(packageBuffer, {
          retention: record.retention,
          ownerUserId: record.ownerUserId,
          sessionId: record.sessionId,
          packageAlreadyStored: true,
        });
        await removePending(uploadId);
        return { kind: "session", receipt };
      } catch (error) {
        await blob().del(record.pathname);
        await removePending(uploadId);
        throw error;
      }
    }

    case "teacher-material": {
      await fetchUploaded(record);
      const info = await addSessionFile(principal, record.sessionId, {
        id: record.fileId,
        name: record.name,
        sizeBytes: record.size,
        pathname: record.pathname,
      });
      if (!info) {
        await blob().del(record.pathname);
        throw new HttpError(404, "Задачник не найден или закрыт.");
      }
      await removePending(uploadId);
      return { kind: "file", info };
    }

    case "student-work": {
      await fetchUploaded(record);
      const info = await addStudentFile(record.studentSessionId, {
        id: record.fileId,
        name: record.name,
        sizeBytes: record.size,
        pathname: record.pathname,
      });
      if (!info) {
        await blob().del(record.pathname);
        throw new HttpError(404, "Сессия ученика не найдена.");
      }
      await removePending(uploadId);
      return { kind: "file", info };
    }

    default:
      throw new InvalidDataError("Неизвестный тип загрузки.");
  }
}

export async function cleanupUploads() {
  const ids = (await getJson(UPLOADS_INDEX)) ?? [];
  const keep = [];
  let removed = 0;
  for (const uploadId of ids) {
    const record = await getJson(uploadKey(uploadId));
    if (!record) continue;
    if (parseDate(record.expiresAt)?.getTime() > Date.now()) {
      keep.push(uploadId);
      continue;
    }
    await blob().del(record.pathname);
    await deleteJson(uploadKey(uploadId));
    removed++;
  }
  await setJson(UPLOADS_INDEX, keep);
  return removed;
}
