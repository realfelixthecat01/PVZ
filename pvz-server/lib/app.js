import express from "express";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  MAX_PACKAGE_BYTES,
  MAX_SCREEN_FRAME_BYTES,
  rateLimitMultiplier,
} from "./config.js";
import {
  authenticate,
  initAccounts,
  login,
  loginRoot,
  logout,
  register,
  changePassword,
  getProfile,
  updateProfile,
  activateKey,
  createKey,
  listKeys,
  listUsers,
  hasCatalogAccess,
} from "./accounts.js";
import { effectivePlan, planDisplayName } from "./limits.js";
import {
  listAllSessions,
  listOwnedSessions,
  listOwnerFiles,
  getOwnerUsage,
  uploadPackageBuffer,
  extendSession,
  closeSession,
  downloadSessionPackage,
  downloadPackageByToken,
  startStudentSession,
  reportProgress,
  beginScreenWatch,
  endScreenWatch,
  getScreenFrame,
  isScreenRequested,
  storeScreenFrame,
  stopScreenShare,
  listSessionFiles,
  openSessionFile,
  deleteSessionFile,
  listStudentFiles,
  openStudentFile,
  listCatalogTaskbooks,
  downloadCatalogTaskbook,
  purgeExpired,
} from "./store.js";
import { beginUpload, completeUpload, putUpload, cleanupUploads } from "./uploads.js";
import { beginSeedUpload, resetSeedArea } from "./seed.js";
import { listPublicDownloads, listAllDownloads, findPublicDownload, openPublicDownload } from "./downloads.js";
import { InvalidPackageError } from "./pvzPackage.js";
import { renderLanding } from "./pages/landing.js";
import {
  HttpError,
  UnauthorizedError,
  parseJsonBody,
  isTokenShaped,
} from "./util.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const publicDirectory = path.join(here, "..", "public");
const pageCache = new Map();

function readPage(name) {
  if (!pageCache.has(name)) pageCache.set(name, fs.readFileSync(path.join(publicDirectory, name), "utf8"));
  return pageCache.get(name);
}

function bearerToken(request) {
  const header = request.headers.authorization ?? "";
  if (!header.toLowerCase().startsWith("bearer ")) return null;
  const token = header.slice(7).trim();
  return isTokenShaped(token) ? token : null;
}

async function principalOf(request) {
  const token = bearerToken(request);
  return token ? authenticate(token) : null;
}

async function requirePrincipal(request) {
  const principal = await principalOf(request);
  if (!principal) throw new UnauthorizedError("Сессия доступа истекла. Войдите снова.");
  return principal;
}

async function requireAdmin(request) {
  const principal = await requirePrincipal(request);
  if (!principal.isAdmin) throw new HttpError(403, "Доступ только для администратора.");
  return principal;
}

async function buildProfile(principal) {
  const usage = await getOwnerUsage(principal.userId);
  return getProfile(principal, usage);
}

function clientAddress(request) {
  const forwarded = request.headers["x-forwarded-for"];
  const first = Array.isArray(forwarded) ? forwarded[0] : forwarded;
  if (first) return String(first).split(",")[0].trim();
  return request.socket?.remoteAddress ?? "unknown";
}

// Окно в минуту на процесс: на Vercel каждый экземпляр считает сам, как и Kestrel-лимитер.
function rateLimiter(permitLimit, { multiplied = true } = {}) {
  const buckets = new Map();
  let lastCleanup = Date.now();
  return (request, response, next) => {
    const now = Date.now();
    if (now - lastCleanup > 60_000) {
      for (const [key, bucket] of buckets) if (now - bucket.start >= 60_000) buckets.delete(key);
      lastCleanup = now;
    }
    const key = clientAddress(request);
    let bucket = buckets.get(key);
    if (!bucket || now - bucket.start >= 60_000) {
      bucket = { start: now, count: 0 };
      buckets.set(key, bucket);
    }
    const limit = Math.max(1, Math.round(permitLimit * (multiplied ? rateLimitMultiplier() : 1)));
    bucket.count++;
    if (bucket.count > limit) {
      response.status(429).json({ error: "Слишком много запросов к серверу. Подождите минуту и попробуйте снова." });
      return;
    }
    next();
  };
}

function contentDisposition(name) {
  const ascii = String(name).replace(/[^\x20-\x7E]/g, "_").replace(/["\\]/g, "_");
  return `attachment; filename="${ascii}"; filename*=UTF-8''${encodeURIComponent(name)}`;
}

function sendBuffer(request, response, buffer, name) {
  response.setHeader("Content-Type", "application/octet-stream");
  response.setHeader("Content-Disposition", contentDisposition(name));
  response.setHeader("Accept-Ranges", "bytes");
  const range = request.headers.range;
  if (range) {
    const match = /^bytes=(\d*)-(\d*)$/.exec(range);
    if (match) {
      let start = match[1] === "" ? null : Number(match[1]);
      let end = match[2] === "" ? null : Number(match[2]);
      if (start === null && end !== null) {
        start = Math.max(0, buffer.length - end);
        end = buffer.length - 1;
      } else {
        start = start ?? 0;
        end = end === null ? buffer.length - 1 : end;
      }
      if (start >= 0 && end >= start && start < buffer.length) {
        end = Math.min(end, buffer.length - 1);
        response.status(206);
        response.setHeader("Content-Range", `bytes ${start}-${end}/${buffer.length}`);
        response.setHeader("Content-Length", String(end - start + 1));
        response.end(buffer.subarray(start, end + 1));
        return;
      }
      response.status(416).setHeader("Content-Range", `bytes */${buffer.length}`);
      response.end();
      return;
    }
  }
  response.setHeader("Content-Length", String(buffer.length));
  response.end(buffer);
}

export async function createApp() {
  await initAccounts();

  const app = express();
  app.disable("x-powered-by");
  app.set("trust proxy", true);
  app.use(express.json({ limit: "2mb" }));

  const teacherApi = rateLimiter(90);
  const studentApi = rateLimiter(120);
  const accountApi = rateLimiter(120);
  const accountAuth = rateLimiter(10);
  const teacherLogin = rateLimiter(5, { multiplied: false });

  // ---- страницы ----

  app.get("/health", (request, response) => response.json({ status: "ok" }));
  app.get("/", async (request, response, next) => {
    try {
      const downloads = await listPublicDownloads();
      response.type("html").send(renderLanding(downloads));
    } catch (error) {
      next(error);
    }
  });
  for (const [route, file] of [
    ["/pricing", "pricing.html"],
    ["/admin", "admin.html"],
    ["/login", "login.html"],
    ["/register", "register.html"],
    ["/cabinet", "cabinet.html"],
  ]) {
    app.get(route, (request, response) => response.type("html").send(readPage(file)));
  }
  app.get("/site-styles.css", (request, response) => {
    response.type("css").send(readPage("site-styles.css"));
  });
  app.get("/site-common.js", (request, response) => {
    response.type("application/javascript").send(readPage("site-common.js"));
  });

  // ---- скачивание сборок ----

  app.get("/api/downloads", async (request, response, next) => {
    try {
      response.json(await listPublicDownloads());
    } catch (error) {
      next(error);
    }
  });

  app.get("/downloads/:id", async (request, response, next) => {
    try {
      if (await findPublicDownload(request.params.id) === null) return response.status(404).end();
      const file = await openPublicDownload(request.params.id);
      if (!file) return response.status(404).end();
      sendBuffer(request, response, file.content, file.info.fileName);
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/admin/downloads", teacherApi, async (request, response, next) => {
    try {
      await requireAdmin(request);
      response.json(await listAllDownloads());
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/admin/downloads/:id", teacherApi, async (request, response, next) => {
    try {
      await requireAdmin(request);
      const file = await openPublicDownload(request.params.id);
      if (!file) return response.status(404).end();
      sendBuffer(request, response, file.content, file.info.fileName);
    } catch (error) {
      next(error);
    }
  });

  // ---- каталог задачников ----

  app.get("/api/catalog/:subject/:classNumber", async (request, response, next) => {
    try {
      const classNumber = Number(request.params.classNumber);
      response.json(await listCatalogTaskbooks(request.params.subject, classNumber));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/catalog/:subject/:classNumber/:fileName", teacherApi, async (request, response, next) => {
    try {
      const classNumber = Number(request.params.classNumber);
      const principal = await principalOf(request);
      if (!principal) return response.status(401).json({ error: "Войдите в личный кабинет, чтобы скачивать задачники из каталога." });
      if (!(await hasCatalogAccess(principal, request.params.subject, classNumber))) {
        return response.status(403).json({ error: "Нужна подписка на предмет, класс или весь каталог. Активируйте ключ в личном кабинете." });
      }
      let packageBuffer;
      packageBuffer = await downloadCatalogTaskbook(request.params.subject, classNumber, request.params.fileName);
      if (!packageBuffer) return response.status(404).end();
      sendBuffer(request, response, packageBuffer, request.params.fileName);
    } catch (error) {
      next(error);
    }
  });

  // ---- вход учителя и задачники ----

  app.post("/api/teacher/login", teacherLogin, async (request, response, next) => {
    try {
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Введите пароль доступа." });
      const receipt = await loginRoot(request.body.password);
      if (!receipt) return response.status(401).end();
      response.json({ accessToken: receipt.accessToken, expiresAt: receipt.expiresAt });
    } catch (error) {
      next(error);
    }
  });

  app.post(
    "/api/teacher/sessions",
    teacherApi,
    express.raw({ type: "application/octet-stream", limit: MAX_PACKAGE_BYTES + 1024 }),
    async (request, response, next) => {
      try {
        const principal = await requirePrincipal(request);
        const plan = String(request.query.plan ?? "").toLowerCase();
        const paid = plan === "paid-month" || plan === "paidonemonth";
        if (!Buffer.isBuffer(request.body)) return response.status(415).end();
        if (request.body.length > MAX_PACKAGE_BYTES) {
          return response.status(400).json({ error: "Размер .pvz превышает 16 МиБ." });
        }
        const receipt = await uploadPackageBuffer(request.body, {
          retention: paid ? 2 : 5,
          plan: paid ? 1 : 0,
          ownerUserId: principal.kind === "root" ? null : principal.userId,
          enforceQuota: false,
          principal: null,
        });
        response.json(receipt);
      } catch (error) {
        next(error);
      }
    },
  );

  app.get("/api/teacher/sessions", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      if (principal.isAdmin || principal.kind === "root") {
        const sessions = await listAllSessions();
        const users = await listUsers();
        const names = new Map(users.map((user) => [user.id, user.fullName?.trim() ? user.fullName : user.username]));
        response.json(sessions.map((session) => ({
          ...session,
          ownerName: session.ownerId ? (names.get(session.ownerId) ?? null) : null,
        })));
        return;
      }
      const selfName = principal.fullName?.trim() ? principal.fullName : principal.username;
      response.json((await listOwnedSessions(principal.userId)).map((session) => ({ ...session, ownerName: selfName })));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/teacher/sessions/:id/extend", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const expiresAt = principal.isAdmin
        ? await extendSession(request.params.id)
        : await extendSession(request.params.id, principal.userId);
      if (!expiresAt) return response.status(404).end();
      response.json({ expiresAt });
    } catch (error) {
      next(error);
    }
  });

  app.delete("/api/teacher/sessions/:id", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const closed = principal.isAdmin
        ? await closeSession(request.params.id)
        : await closeSession(request.params.id, principal.userId);
      response.status(closed ? 204 : 404).end();
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/teacher/sessions/:id/download", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const packageBuffer = principal.isAdmin
        ? await downloadSessionPackage(request.params.id)
        : await downloadSessionPackage(request.params.id, principal.userId);
      if (!packageBuffer) return response.status(404).end();
      sendBuffer(request, response, packageBuffer, "taskbook.pvz");
    } catch (error) {
      next(error);
    }
  });

  // ---- аккаунты ----

  app.post("/api/account/register", accountAuth, async (request, response, next) => {
    try {
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Заполните форму регистрации." });
      const { receipt, principal } = await register(request.body);
      response.json({
        accessToken: receipt.accessToken,
        expiresAt: receipt.expiresAt,
        profile: await buildProfile(principal),
      });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/account/login", accountAuth, async (request, response, next) => {
    try {
      if (!parseJsonBody(request.body)) {
        return response.status(400).json({ error: "Введите имя аккаунта (или email) и пароль." });
      }
      const result = await login(request.body.identifier, request.body.password);
      if (!result) return response.status(401).json({ error: "Неверное имя аккаунта или пароль." });
      response.json({
        accessToken: result.receipt.accessToken,
        expiresAt: result.receipt.expiresAt,
        profile: await buildProfile(result.principal),
      });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/account/logout", accountApi, async (request, response, next) => {
    try {
      const principal = await principalOf(request);
      if (!principal) return response.status(401).end();
      await logout(bearerToken(request));
      response.status(204).end();
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/account/me", accountApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      response.json(await buildProfile(principal));
    } catch (error) {
      next(error);
    }
  });

  app.put("/api/account/me", accountApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Заполните ФИО и школу." });
      await updateProfile(principal, request.body.fullName, request.body.school);
      response.json(await buildProfile(principal));
    } catch (error) {
      next(error);
    }
  });

  app.put("/api/account/me/password", accountAuth, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Заполните оба поля пароля." });
      await changePassword(principal, request.body.currentPassword, request.body.newPassword);
      response.status(204).end();
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/account/keys", accountApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      if (!parseJsonBody(request.body) || !String(request.body.code ?? "").trim()) {
        return response.status(400).json({ error: "Введите ключ активации." });
      }
      const message = await activateKey(principal, request.body.code);
      response.json({ message, profile: await buildProfile(principal) });
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/account/files", accountApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      response.json(await listOwnerFiles(principal.userId));
    } catch (error) {
      next(error);
    }
  });

  app.delete("/api/account/files/:id", accountApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const closed = await closeSession(request.params.id, principal.userId);
      response.status(closed ? 204 : 404).end();
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/account/files/:id/download", accountApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const packageBuffer = await downloadSessionPackage(request.params.id, principal.userId);
      if (!packageBuffer) return response.status(404).end();
      sendBuffer(request, response, packageBuffer, "taskbook.pvz");
    } catch (error) {
      next(error);
    }
  });

  // ---- админ ----

  app.post("/api/admin/keys", teacherApi, async (request, response, next) => {
    try {
      const principal = await requireAdmin(request);
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Укажите параметры ключа." });
      response.json(await createKey(principal, {
        kind: request.body.kind,
        storagePlan: request.body.storagePlan,
        durationDays: Number(request.body.durationDays),
        subject: request.body.subject,
        classNumber: request.body.classNumber === null || request.body.classNumber === undefined
          ? null
          : Number(request.body.classNumber),
      }));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/admin/keys", teacherApi, async (request, response, next) => {
    try {
      await requireAdmin(request);
      response.json(await listKeys());
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/admin/teachers", teacherApi, async (request, response, next) => {
    try {
      await requireAdmin(request);
      const now = new Date();
      const sessions = await listAllSessions();
      const users = await listUsers();
      const byOwner = new Map();
      for (const session of sessions) {
        if (!session.ownerId) continue;
        const list = byOwner.get(session.ownerId) ?? [];
        list.push(session);
        byOwner.set(session.ownerId, list);
      }
      response.json(users.map((user) => {
        const plan = effectivePlan(user, now);
        const until = user.storageUntil ? new Date(user.storageUntil) : null;
        return {
          id: user.id,
          username: user.username,
          email: user.email,
          fullName: user.fullName,
          school: user.school,
          plan,
          planDisplayName: planDisplayName(plan),
          planUntil: user.isAdmin || !until || until.getTime() <= now.getTime() ? null : until.toISOString(),
          sessions: byOwner.get(user.id) ?? [],
        };
      }));
    } catch (error) {
      next(error);
    }
  });

  // ---- наполнение каталога и сборок (только администратор) ----

  app.post("/api/admin/seed", teacherApi, async (request, response, next) => {
    try {
      await requireAdmin(request);
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Не передан файл для наполнения." });
      response.json(await beginSeedUpload(request.body.pathname, Number(request.body.size)));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/admin/seed/reset", teacherApi, async (request, response, next) => {
    try {
      await requireAdmin(request);
      const area = String(request.body?.area ?? "");
      response.json({ removed: await resetSeedArea(area) });
    } catch (error) {
      next(error);
    }
  });

  // ---- просмотр экрана ученика ----

  app.post("/api/admin/students/:ssid/watch", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const started = await beginScreenWatch(principal, request.params.ssid);
      response.status(started ? 204 : 404).end();
    } catch (error) {
      next(error);
    }
  });

  app.delete("/api/admin/students/:ssid/watch", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      await endScreenWatch(principal, request.params.ssid);
      response.status(204).end();
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/admin/students/:ssid/watch/frame", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const frame = await getScreenFrame(principal, request.params.ssid);
      if (!frame) return response.status(204).end();
      response.setHeader("Content-Type", "image/jpeg");
      response.end(frame);
    } catch (error) {
      next(error);
    }
  });

  // ---- ученик ----

  app.post("/api/taskbooks/:token/sessions", studentApi, async (request, response, next) => {
    try {
      if (!parseJsonBody(request.body)) {
        return response.status(400).json({ error: "Укажите имя, фамилию и класс." });
      }
      const session = await startStudentSession(request.params.token, request.body);
      if (!session) return response.status(404).end();
      response.json(session);
    } catch (error) {
      next(error);
    }
  });

  app.put("/api/student/sessions/:sessionId/progress", studentApi, async (request, response, next) => {
    try {
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Не передан прогресс ученика." });
      const updated = await reportProgress({
        sessionId: request.params.sessionId,
        tasks: request.body.tasks,
      });
      response.status(updated ? 204 : 404).end();
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/student/sessions/:sessionId/watch", studentApi, async (request, response, next) => {
    try {
      const watching = await isScreenRequested(request.params.sessionId);
      if (watching === null) return response.status(404).end();
      response.json({ watching });
    } catch (error) {
      next(error);
    }
  });

  app.post(
    "/api/student/sessions/:sessionId/screen",
    studentApi,
    express.raw({ type: () => true, limit: MAX_SCREEN_FRAME_BYTES + 1024 }),
    async (request, response, next) => {
      try {
        if (!Buffer.isBuffer(request.body)) return response.status(400).json({ error: "Кадр экрана слишком большой." });
        const result = await storeScreenFrame(request.params.sessionId, request.body);
        if (result === null) return response.status(404).end();
        response.json({ watching: result });
      } catch (error) {
        next(error);
      }
    },
  );

  app.delete("/api/student/sessions/:sessionId/screen", studentApi, async (request, response, next) => {
    try {
      await stopScreenShare(request.params.sessionId);
      response.status(204).end();
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/student/sessions/:sessionId/files", studentApi, async (request, response, next) => {
    try {
      const files = await listStudentFiles(request.params.sessionId);
      if (!files) return response.status(404).end();
      response.json(files);
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/student/sessions/:sessionId/files/:fileId", studentApi, async (request, response, next) => {
    try {
      const file = await openStudentFile(request.params.sessionId, request.params.fileId);
      if (!file) return response.status(404).end();
      sendBuffer(request, response, file.content, file.info.name);
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/teacher/sessions/:id/files", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const files = await listSessionFiles(principal, request.params.id);
      if (!files) return response.status(404).end();
      response.json(files);
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/teacher/sessions/:id/files/:fileId/download", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const file = await openSessionFile(principal, request.params.id, request.params.fileId);
      if (!file) return response.status(404).end();
      sendBuffer(request, response, file.content, file.info.name);
    } catch (error) {
      next(error);
    }
  });

  app.delete("/api/teacher/sessions/:id/files/:fileId", teacherApi, async (request, response, next) => {
    try {
      const principal = await requirePrincipal(request);
      const deleted = await deleteSessionFile(principal, request.params.id, request.params.fileId);
      response.status(deleted ? 204 : 404).end();
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/taskbooks/:token", studentApi, async (request, response, next) => {
    try {
      const packageBuffer = await downloadPackageByToken(request.params.token);
      if (!packageBuffer) return response.status(404).end();
      sendBuffer(request, response, packageBuffer, "taskbook.pvz");
    } catch (error) {
      next(error);
    }
  });

  // ---- загрузка через begin / PUT / complete ----

  app.post("/api/upload/begin", accountApi, async (request, response, next) => {
    try {
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Не передан запрос загрузки." });
      const principal = await principalOf(request);
      const target = String(request.body.target ?? "");
      if (target !== "student-work" && !principal) {
        return response.status(401).json({ error: "Сессия доступа истекла. Войдите снова." });
      }
      response.json(await beginUpload(principal, request.body));
    } catch (error) {
      next(error);
    }
  });

  app.put(
    "/api/upload/put/:uploadId",
    express.raw({ type: () => true, limit: MAX_PACKAGE_BYTES + 1024 }),
    async (request, response, next) => {
      try {
        if (!Buffer.isBuffer(request.body)) return response.status(400).json({ error: "Пустое тело загрузки." });
        response.json(await putUpload(request.params.uploadId, request.body));
      } catch (error) {
        next(error);
      }
    },
  );

  app.post("/api/upload/complete", accountApi, async (request, response, next) => {
    try {
      if (!parseJsonBody(request.body)) return response.status(400).json({ error: "Не передан идентификатор загрузки." });
      const principal = await principalOf(request);
      response.json(await completeUpload(principal, String(request.body.uploadId ?? "")));
    } catch (error) {
      next(error);
    }
  });

  // ---- обслуживание ----

  app.all("/api/cron/cleanup", async (request, response, next) => {
    try {
      const secret = process.env.CRON_SECRET;
      const token = bearerToken(request);
      if (!secret || token !== secret) return response.status(401).end();
      const sessions = await purgeExpired();
      const uploads = await cleanupUploads();
      response.json({ ok: true, sessions, uploads });
    } catch (error) {
      next(error);
    }
  });

  app.use((request, response) => response.status(404).json({ error: "Страница не найдена." }));

  // eslint-disable-next-line no-unused-vars
  app.use((error, request, response, next) => {
    if (error instanceof InvalidPackageError) {
      return response.status(400).json({ error: error.message });
    }
    if (error instanceof HttpError) {
      return response.status(error.status).json({ error: error.message });
    }
    if (error?.type === "entity.too.large") {
      return response.status(400).json({ error: "Размер запроса превышает допустимый." });
    }
    if (error instanceof SyntaxError && "body" in error) {
      return response.status(400).json({ error: "Некорректный JSON в запросе." });
    }
    console.error(error);
    response.status(500).json({ error: "Внутренняя ошибка сервера." });
  });

  return app;
}
