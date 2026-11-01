// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

import { STUDENT_ONLINE_MS } from "./config.js";
import { parseDate } from "./util.js";

// Отчёты по сессии: построчная выгрузка результатов и сводка по каждому заданию.
// Сводка считается из уже готовых массивов earnedPoints/completedTasks, новых запросов к хранилищу нет.

const MAX_TITLE_CHARS = 70;

export function shortTitle(description) {
  const line = String(description ?? "")
    .split(/\r?\n/)
    .map((part) => part.trim())
    .find(Boolean);
  if (!line) return "";
  if (line.length <= MAX_TITLE_CHARS) return line;
  const cut = line.slice(0, MAX_TITLE_CHARS);
  const space = cut.lastIndexOf(" ");
  return `${(space > MAX_TITLE_CHARS - 20 ? cut.slice(0, space) : cut).trimEnd()}…`;
}

export function taskTitles(session) {
  const scores = session?.taskMaxScores ?? [];
  return scores.map((_, index) => session.taskTitles?.[index] || `Задача ${index + 1}`);
}

function round(value) {
  return Math.round(value * 10) / 10;
}

function percent(earned, maximum) {
  return maximum > 0 ? round((earned / maximum) * 100) : 0;
}

function studentRows(session, now) {
  return (session.students ?? []).map((student) => {
    const startedAt = parseDate(student.startedAt) ?? now;
    const lastActivityAt = parseDate(student.lastActivityAt) ?? startedAt;
    const points = (session.taskMaxScores ?? []).map((_, index) => student.earnedPoints?.[index] ?? 0);
    const completed = (session.taskMaxScores ?? []).map((_, index) => Boolean(student.completedTasks?.[index]));
    const earned = points.reduce((sum, value) => sum + value, 0);
    return {
      firstName: student.firstName,
      lastName: student.lastName,
      className: student.className,
      studentSessionId: student.sessionId,
      startedAt: student.startedAt,
      lastActivityAt: student.lastActivityAt,
      elapsedSeconds: Math.max(0, Math.floor((lastActivityAt.getTime() - startedAt.getTime()) / 1000)),
      earnedPoints: earned,
      maximumPoints: session.maximumPoints,
      percent: percent(earned, session.maximumPoints),
      completedTasks: completed.filter(Boolean).length,
      totalTasks: session.totalTasks,
      isOnline: now.getTime() - lastActivityAt.getTime() <= STUDENT_ONLINE_MS,
      tasks: points.map((value, index) => ({ earnedPoints: value, completed: completed[index] })),
    };
  });
}

export function sessionReport(session, now = new Date()) {
  const scores = session.taskMaxScores ?? [];
  const titles = taskTitles(session);
  const rows = studentRows(session, now).sort((a, b) => b.earnedPoints - a.earnedPoints);
  const students = rows.length;

  const tasks = scores.map((maxPoints, index) => {
    let solved = 0;
    let partially = 0;
    let sum = 0;
    for (const row of rows) {
      const task = row.tasks[index];
      if (task.completed) solved++;
      else if (task.earnedPoints > 0) partially++;
      sum += task.earnedPoints;
    }
    const average = students > 0 ? sum / students : 0;
    return {
      number: index + 1,
      title: titles[index],
      maxPoints,
      solved,
      partially,
      untouched: students - solved - partially,
      averagePoints: round(average),
      averagePercent: percent(average, maxPoints),
    };
  });

  const best = rows[0];
  const online = rows.filter((row) => row.isOnline).length;
  const solvedEverything = rows.filter((row) => row.completedTasks === session.totalTasks).length;
  const totalEarned = rows.reduce((sum, row) => sum + row.earnedPoints, 0);
  return {
    sessionId: session.id,
    tokenHint: session.tokenHint,
    uploadedAt: session.uploadedAt,
    expiresAt: session.expiresAt,
    totalTasks: session.totalTasks,
    maximumPoints: session.maximumPoints,
    students,
    online,
    solvedEverything,
    averagePercent: percent(totalEarned / (students || 1), session.maximumPoints),
    bestStudent: best ? { name: `${best.lastName} ${best.firstName}`, className: best.className, earnedPoints: best.earnedPoints } : null,
    rows,
    tasks,
  };
}

// Excel в русской локализации ждёт разделитель ; вместо запятой,
// а без BOM он не распознаёт кириллицу в UTF-8.
function csvCell(value) {
  const text = value === null || value === undefined ? "" : String(value);
  return `"${text.replace(/"/g, '""')}"`;
}

function csvRow(values) {
  return values.map(csvCell).join(";");
}

function formatDateTime(value) {
  const date = parseDate(value);
  if (!date) return "";
  const pad = (part) => String(part).padStart(2, "0");
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function resultsCsv(session, now = new Date()) {
  const report = sessionReport(session, now);
  const scores = session.taskMaxScores ?? [];
  const titles = taskTitles(session);
  const lines = [];

  lines.push(csvRow([
    "Фамилия",
    "Имя",
    "Класс",
    "Решил задач",
    "Баллы",
    "Максимум баллов",
    "Выполнено, %",
    "Время в задачнике, мин",
    "Статус",
    "Начал",
    "Последняя активность",
    ...scores.map((maxPoints, index) => `Задача ${index + 1} (макс ${maxPoints}): ${titles[index]}`),
  ]));

  for (const row of report.rows) {
    lines.push(csvRow([
      row.lastName,
      row.firstName,
      row.className,
      `${row.completedTasks} из ${row.totalTasks}`,
      row.earnedPoints,
      row.maximumPoints,
      Math.round(row.percent),
      Math.round(row.elapsedSeconds / 60),
      row.isOnline ? "В сети" : "Не в сети",
      formatDateTime(row.startedAt),
      formatDateTime(row.lastActivityAt),
      ...row.tasks.map((task) => task.earnedPoints),
    ]));
  }

  if (report.rows.length === 0) lines.push(csvRow(["Пока никто не открыл этот задачник"]));

  lines.push("");
  lines.push(csvRow(["Статистика по заданиям"]));
  lines.push(csvRow(["Номер", "Задание", "Максимум баллов", "Решили", "Решают частично", "Не начинали", "Средний балл", "Средний %"]));
  for (const task of report.tasks) {
    lines.push(csvRow([
      task.number,
      task.title,
      task.maxPoints,
      task.solved,
      task.partially,
      task.untouched,
      Math.round(task.averagePoints),
      Math.round(task.averagePercent),
    ]));
  }

  return Buffer.from(`\uFEFF${lines.join("\r\n")}\r\n`, "utf8");
}
