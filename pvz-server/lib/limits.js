// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

// Порт PVZ.Core.Models: предметы каталога, сроки хранения и лимиты.

export const SUBJECTS = [
  { id: "informatics", displayName: "Информатика" },
  { id: "english", displayName: "Английский язык" },
  { id: "math", displayName: "Математика" },
];

export function normalizeSubject(subject) {
  const found = SUBJECTS.find((item) => item.id === String(subject ?? "").toLowerCase());
  return found ? found.id : null;
}

export function minimumClassNumber(subject) {
  switch (subject) {
    case "informatics": return 6;
    case "english": return 2;
    case "math": return 1;
    default: throw new Error("Неизвестный предмет.");
  }
}

export function isValidClass(subject, classNumber) {
  const normalized = normalizeSubject(subject);
  if (!normalized) return false;
  return Number.isInteger(classNumber) && classNumber >= minimumClassNumber(normalized) && classNumber <= 11;
}

export function displaySubject(subject) {
  return SUBJECTS.find((item) => item.id === subject)?.displayName ?? subject;
}

// RetentionPeriod из C#: числа вместо строк, порядок enum совпадает.
export const RETENTION = {
  HOURS_48: 0,
  WEEK_1: 1,
  MONTH_1: 2,
  YEAR_1: 3,
  FOREVER: 4,
  TWO_WEEKS: 5,
};

export const RETENTION_NAMES = {
  0: "Hours48",
  1: "Week1",
  2: "Month1",
  3: "Year1",
  4: "Forever",
  5: "TwoWeeks",
};

export const ALL_RETENTIONS = [0, 1, 2, 3, 4, 5];

export function retentionLifetimeMs(retention) {
  switch (retention) {
    case RETENTION.HOURS_48: return 48 * 60 * 60 * 1000;
    case RETENTION.WEEK_1: return 7 * 24 * 60 * 60 * 1000;
    case RETENTION.MONTH_1: return 30 * 24 * 60 * 60 * 1000;
    case RETENTION.YEAR_1: return 365 * 24 * 60 * 60 * 1000;
    case RETENTION.FOREVER: return 36500 * 24 * 60 * 60 * 1000;
    default: return 14 * 24 * 60 * 60 * 1000;
  }
}

export function retentionDisplayName(retention) {
  switch (retention) {
    case RETENTION.HOURS_48: return "Хранение · 48 часов";
    case RETENTION.WEEK_1: return "Хранение · 1 неделя";
    case RETENTION.MONTH_1: return "Хранение · 1 месяц";
    case RETENTION.YEAR_1: return "Хранение · 1 год";
    case RETENTION.FOREVER: return "Постоянное хранение";
    default: return "Хранение · 2 недели";
  }
}

export function retentionShortLabel(retention) {
  switch (retention) {
    case RETENTION.HOURS_48: return "48 ч";
    case RETENTION.WEEK_1: return "1 нед";
    case RETENTION.MONTH_1: return "1 мес";
    case RETENTION.YEAR_1: return "1 год";
    case RETENTION.FOREVER: return "∞";
    default: return "2 нед";
  }
}

// Хранилище не ограничено по количеству задачников, срок выбирает учитель при публикации.
export const UNLIMITED = -1;

// Каталог задачников: список лежит в KV, сами файлы в Blob.
export function catalogPrefix(subject, classNumber) {
  return `pvz/catalog/${subject}/${classNumber}/`;
}

export function catalogIndexKey(subject, classNumber) {
  return `pvz:catalog:${subject}:${classNumber}`;
}
