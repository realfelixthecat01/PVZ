import crypto from "node:crypto";
import zlib from "node:zlib";
import { MAX_PACKAGE_BYTES } from "./config.js";

const MAGIC = Buffer.from("PVZPKG1", "utf8");
const CHECKSUM_LENGTH = 32;
const HEADER_LENGTH = MAGIC.length + 4 + CHECKSUM_LENGTH;
const MAX_DOCUMENT_BYTES = 64 * 1024 * 1024;

export function loadPackage(buffer) {
  if (!buffer || buffer.length <= HEADER_LENGTH || buffer.length > MAX_PACKAGE_BYTES) {
    throw new InvalidPackageError("Размер файла задачника недопустим.");
  }
  if (!buffer.subarray(0, MAGIC.length).equals(MAGIC)) {
    throw new InvalidPackageError("Файл не является задачником PVZ или имеет неподдерживаемую версию.");
  }

  const documentLength = buffer.readInt32LE(MAGIC.length);
  if (documentLength <= 0 || documentLength > MAX_DOCUMENT_BYTES) {
    throw new InvalidPackageError("В файле указан недопустимый размер задачника.");
  }

  const expectedHash = buffer.subarray(MAGIC.length + 4, HEADER_LENGTH);
  const compressed = buffer.subarray(HEADER_LENGTH);
  let jsonBytes;
  try {
    jsonBytes = zlib.brotliDecompressSync(compressed, { maxOutputLength: MAX_DOCUMENT_BYTES });
  } catch {
    throw new InvalidPackageError("Файл задачника повреждён или содержит неверные сжатые данные.");
  }

  if (jsonBytes.length !== documentLength) {
    throw new InvalidPackageError("Файл задачника повреждён или обрезан.");
  }
  if (!crypto.timingSafeEqual(expectedHash, crypto.createHash("sha256").update(jsonBytes).digest())) {
    throw new InvalidPackageError("Контрольная сумма задачника не совпадает.");
  }

  let document;
  try {
    document = JSON.parse(jsonBytes.toString("utf8"));
  } catch {
    throw new InvalidPackageError("Файл не содержит корректный задачник PVZ.");
  }

  if (!document || document.schemaVersion !== 1 || !Array.isArray(document.tasks) || document.tasks.length === 0) {
    throw new InvalidPackageError("В файле нет задач или версия формата не поддерживается.");
  }
  return document;
}

export class InvalidPackageError extends Error {}

// TaskbookScoring.GetTaskMaxScore: kind 0 — тесты, kind 1 — пропуски.
export function getTaskMaxScore(task) {
  const bonus = typeof task.bonusPointsForAllTests === "number" ? task.bonusPointsForAllTests : 10;
  if (task.kind === 1) {
    const blanks = Array.isArray(task.blanks) ? task.blanks : [];
    if (blanks.length === 0) return 0;
    return blanks.reduce((sum, blank) => sum + (blank.points ?? 25), 0) + bonus;
  }
  const tests = Array.isArray(task.tests) ? task.tests : [];
  if (tests.length === 0) return 0;
  return tests.reduce((sum, test) => sum + (test.points ?? 25), 0) + bonus;
}

export function getDocumentMaxScore(document) {
  return document.tasks.reduce((sum, task) => sum + getTaskMaxScore(task), 0);
}

export function getTaskMaxScores(document) {
  return document.tasks.map(getTaskMaxScore);
}
