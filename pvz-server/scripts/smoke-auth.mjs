// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

// Смоук серверной цепочки входа: запускайте с открытым портом, например
//   node scripts/smoke-auth.mjs
// Поднимает локальный сервер во временной папке и проверяет регистрацию,
// гостевой вход, лимиты гостя и историю ученика.
import crypto from "node:crypto";
import zlib from "node:zlib";
import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const PORT = 18700 + Math.floor(Math.random() * 300);
const BASE = `http://127.0.0.1:${PORT}`;
const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), "pvz-smoke-"));

let failures = 0;
function check(name, condition, extra = "") {
  if (condition) {
    console.log(`ok   ${name}`);
  } else {
    failures++;
    console.log(`FAIL ${name} ${extra}`);
  }
}

function buildPackage(document) {
  const json = Buffer.from(JSON.stringify(document), "utf8");
  const head = Buffer.alloc(7 + 4 + 32);
  head.write("PVZPKG1", 0, "utf8");
  head.writeInt32LE(json.length, 7);
  crypto.createHash("sha256").update(json).digest().copy(head, 11);
  return Buffer.concat([head, zlib.brotliCompressSync(json)]);
}

async function api(method, url, { token, body, raw } = {}) {
  const headers = {};
  let payload;
  if (raw) {
    payload = raw;
    headers["content-type"] = "application/octet-stream";
  } else if (body !== undefined) {
    payload = JSON.stringify(body);
    headers["content-type"] = "application/json";
  }
  if (token) headers.authorization = `Bearer ${token}`;
  const response = await fetch(BASE + url, { method, headers, body: payload });
  const text = await response.text();
  let json = null;
  try { json = JSON.parse(text); } catch {}
  return { status: response.status, json, text };
}

const packageRoot = fileURLToPath(new URL("..", import.meta.url));
const server = spawn(process.execPath, [path.join(packageRoot, "server.js")], {
  cwd: packageRoot,
  env: {
    ...process.env,
    PORT: String(PORT),
    HOST: "127.0.0.1",
    PVZ_SERVER_DATA_DIR: dataDir,
    PVZ_ROOT_PASSWORD: "smoke-root-password",
    PVZ_RATE_LIMIT_MULTIPLIER: "50",
  },
  stdio: ["ignore", "pipe", "pipe"],
});
let serverLog = "";
server.stdout.on("data", (chunk) => { serverLog += chunk; });
server.stderr.on("data", (chunk) => { serverLog += chunk; });

try {
  let ready = false;
  for (let attempt = 0; attempt < 60; attempt++) {
    try {
      const health = await fetch(`${BASE}/health`);
      if (health.ok) { ready = true; break; }
    } catch {}
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  check("сервер поднялся", ready);
  if (!ready) throw new Error(serverLog);

  const student = await api("POST", "/api/account/register", {
    body: { role: "student", fullName: "Тимур Захарик", password: "пароль123", className: "9а" },
  });
  check("регистрация ученика", student.status === 200 && Boolean(student.json?.accessToken), student.text);
  check("класс нормализован в 9А", student.json?.profile?.className === "9А", JSON.stringify(student.json?.profile));
  check("ученик помечен в профиле", student.json?.profile?.role === "student", JSON.stringify(student.json?.profile));

  const teacher = await api("POST", "/api/account/register", {
    body: { role: "teacher", fullName: "Ярослав Козыро", password: "пароль456", school: "Средняя школа №2" },
  });
  check("регистрация учителя", teacher.status === 200 && Boolean(teacher.json?.accessToken), teacher.text);
  check("роль учителя", teacher.json?.profile?.role === "teacher", JSON.stringify(teacher.json?.profile));

  const badRegister = await api("POST", "/api/account/register", {
    body: { role: "student", fullName: "О", password: "пароль123", className: "9" },
  });
  check("короткое имя отклонено", badRegister.status === 400, badRegister.text);

  const guest = await api("POST", "/api/guest/login", {
    body: { secret: crypto.randomBytes(48).toString("base64url") },
  });
  check("вход гостя", guest.status === 200 && Boolean(guest.json?.accessToken), guest.text);
  check("гостевой профиль", guest.json?.profile?.isGuest === true && guest.json?.profile?.role === "teacher", JSON.stringify(guest.json?.profile));

  const sameGuest = await api("POST", "/api/guest/login", { body: { secret: "x".repeat(40) } });
  check("гость повторно по тому же ключу", sameGuest.status === 200, sameGuest.text);

  const wrongLogin = await api("POST", "/api/account/login", {
    body: { identifier: "Тимур Захарик", password: "неверный" },
  });
  check("неверный пароль отклонён", wrongLogin.status === 401, wrongLogin.text);

  const login = await api("POST", "/api/account/login", {
    body: { identifier: "тимур   захарик", password: "пароль123" },
  });
  check("вход по русскому имени", login.status === 200 && Boolean(login.json?.accessToken), login.text);

  const document = {
    schemaVersion: 1,
    subject: "informatics",
    classNumber: 6,
    tasks: [{
      description: "Сложите два числа",
      kind: 0,
      bonusPointsForAllTests: 10,
      tests: [{ points: 25 }, { points: 25 }],
    }],
  };
  const packageBuffer = buildPackage(document);

  const published = await api("POST", "/api/teacher/sessions", { token: teacher.json.accessToken, raw: packageBuffer });
  check("учитель опубликовал задачник", published.status === 200 && Boolean(published.json?.token), published.text);
  const taskbookToken = published.json?.token;

  const guestPublish = await api("POST", "/api/teacher/sessions", { token: guest.json.accessToken, raw: packageBuffer });
  check("гость опубликовал задачник", guestPublish.status === 200 && Boolean(guestPublish.json?.token), guestPublish.text);

  const guestSecond = await api("POST", "/api/teacher/sessions", { token: guest.json.accessToken, raw: packageBuffer });
  check("второй задачник гостя запрещён", guestSecond.status === 400, guestSecond.text);

  const guestStorage = await api("POST", "/api/upload/begin", {
    token: guest.json.accessToken,
    body: { target: "account-file", size: 100, retention: "Month1" },
  });
  check("гостю запрещено личное хранилище", guestStorage.status === 400, guestStorage.text);

  const started = await api("POST", `/api/taskbooks/${taskbookToken}/sessions`, {
    token: student.json.accessToken,
    body: { firstName: "", lastName: "", className: "" },
  });
  check("сессия ученика без имени отклонена", started.status === 400, started.text);

  const startedOk = await api("POST", `/api/taskbooks/${taskbookToken}/sessions`, {
    token: student.json.accessToken,
    body: { firstName: "Тимур", lastName: "Захарик", className: "9А" },
  });
  check("сессия ученика начата", startedOk.status === 200 && Boolean(startedOk.json?.sessionId), startedOk.text);

  const progress = await api("PUT", `/api/student/sessions/${startedOk.json.sessionId}/progress`, {
    body: { tasks: [{ earnedPoints: 30, completed: true }] },
  });
  check("прогресс сохранён", progress.status === 204, progress.text);

  const history = await api("GET", "/api/student/history", { token: student.json.accessToken });
  const row = history.json?.[0];
  check("история ученика заполнена", history.status === 200 && Array.isArray(history.json) && history.json.length === 1, history.text);
  check("в истории баллы", row?.earnedPoints === 30 && row?.maximumPoints === 60, JSON.stringify(row));
  check("в истории учитель", row?.teacherName === "Ярослав Козыро", JSON.stringify(row));
  check("в истории предмет и класс", row?.subject === "informatics" && row?.classNumber === 6, JSON.stringify(row));

  const guestProfile = await api("GET", "/api/account/me", { token: guest.json.accessToken });
  check("профиль гостя читается", guestProfile.status === 200, guestProfile.text);

  const anonymous = await api("GET", "/api/student/history");
  check("история без токена запрещена", anonymous.status === 401, anonymous.text);
} catch (error) {
  failures++;
  console.log(`FAIL неожиданная ошибка: ${error.message}`);
  console.log(serverLog);
} finally {
  server.kill();
  fs.rmSync(dataDir, { recursive: true, force: true });
}

console.log(failures === 0 ? "Смоук пройден." : `Смоук провален: ${failures}.`);
process.exit(failures === 0 ? 0 : 1);
