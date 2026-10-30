// Порт PVZ.Core.Models: предметы каталога, сроки хранения, тарифы и лимиты.

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
    case "english": return 3;
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

// RetentionPeriod — числа, как в C# enum.
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
    case RETENTION.HOURS_48: return "Бесплатный · 48 часов";
    case RETENTION.WEEK_1: return "Хранение · 1 неделя";
    case RETENTION.MONTH_1: return "Хранение · 1 месяц";
    case RETENTION.YEAR_1: return "Хранение · 1 год";
    case RETENTION.FOREVER: return "Постоянное хранение";
    default: return "Бесплатный · 2 недели";
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

// StoragePlan — тариф настольного PVZT.
export const PLAN = {
  FREE_TWO_WEEKS: 0,
  PAID_ONE_MONTH: 1,
};

export function planToRetention(plan) {
  return plan === PLAN.PAID_ONE_MONTH ? RETENTION.MONTH_1 : RETENTION.TWO_WEEKS;
}

// Тарифы облачного кабинета.
export const STORAGE = {
  FREE: "free",
  PRO: "pro",
  PRO_PLUS: "pro_plus",
};

export const UNLIMITED = -1;

export function limitFor(plan, retention) {
  const key = `${plan}:${retention}`;
  switch (key) {
    case "free:0": return 5;
    case "pro:0": return 20;
    case "pro:1": return 15;
    case "pro:2": return 5;
    case "pro:3": return 1;
    case "pro_plus:0": return UNLIMITED;
    case "pro_plus:1": return UNLIMITED;
    case "pro_plus:2": return UNLIMITED;
    case "pro_plus:3": return 100;
    case "pro_plus:4": return 20;
    default: return 0;
  }
}

export function retentionsFor(plan) {
  switch (plan) {
    case STORAGE.PRO: return [RETENTION.HOURS_48, RETENTION.WEEK_1, RETENTION.MONTH_1, RETENTION.YEAR_1];
    case STORAGE.PRO_PLUS: return [RETENTION.HOURS_48, RETENTION.WEEK_1, RETENTION.MONTH_1, RETENTION.YEAR_1, RETENTION.FOREVER];
    default: return [RETENTION.HOURS_48];
  }
}

export function planDisplayName(plan) {
  switch (plan) {
    case STORAGE.PRO: return "Про";
    case STORAGE.PRO_PLUS: return "Про+";
    default: return "Бесплатный";
  }
}

export function effectivePlan(user, now = new Date()) {
  if (!user) return STORAGE.FREE;
  if (user.isAdmin) return STORAGE.PRO_PLUS;
  const until = user.storageUntil ? new Date(user.storageUntil) : null;
  if (until && until.getTime() > now.getTime() && (user.storagePlan === STORAGE.PRO || user.storagePlan === STORAGE.PRO_PLUS)) {
    return user.storagePlan;
  }
  return STORAGE.FREE;
}

// Каталог задачников: индекс хранится в KV, файлы — в Blob.
export function catalogPrefix(subject, classNumber) {
  return `pvz/catalog/${subject}/${classNumber}/`;
}

export function catalogIndexKey(subject, classNumber) {
  return `pvz:catalog:${subject}:${classNumber}`;
}
