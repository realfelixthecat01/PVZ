// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

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
    // Как в C# Load: распаковку останавливаем на заявленной длине, чтобы файл-бомба
    // не развернул в память все 64 МиБ раньше времени.
    jsonBytes = zlib.brotliDecompressSync(compressed, { maxOutputLength: documentLength });
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

// Зеркало TaskbookScoring.GetTaskMaxScore: kind 0 тесты, 1 пропуски, 2 выбор ответа,
// 3 соответствие, 4 короткий ответ.
export function getTaskMaxScore(task) {
  const bonus = typeof task.bonusPointsForAllTests === "number" ? task.bonusPointsForAllTests : 10;
  const sumPoints = (items) =>
    (Array.isArray(items) ? items : []).reduce((sum, item) => sum + (item.points ?? 25), 0);
  switch (task.kind) {
    case 2: {
      // В максимум входят только верные варианты: отмеченный зря отнимает баллы.
      const options = Array.isArray(task.options) ? task.options : [];
      if (options.length === 0) return 0;
      return options
        .filter((option) => option.correct)
        .reduce((sum, option) => sum + (option.points ?? 25), 0) + bonus;
    }
    case 3: {
      const pairs = Array.isArray(task.pairs) ? task.pairs : [];
      if (pairs.length === 0) return 0;
      return sumPoints(pairs) + bonus;
    }
    case 1:
    case 4: {
      const blanks = Array.isArray(task.blanks) ? task.blanks : [];
      if (blanks.length === 0) return 0;
      return sumPoints(blanks) + bonus;
    }
    default: {
      const tests = Array.isArray(task.tests) ? task.tests : [];
      if (tests.length === 0) return 0;
      return sumPoints(tests) + bonus;
    }
  }
}

export function getDocumentMaxScore(document) {
  return document.tasks.reduce((sum, task) => sum + getTaskMaxScore(task), 0);
}

export function getTaskMaxScores(document) {
  return document.tasks.map(getTaskMaxScore);
}
