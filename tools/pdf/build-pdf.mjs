// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

// Собирает pvz_description.pdf из project-description.md.
// Требования: A4, Times New Roman 14 пт, поля 2/2/3/1 см, номера страниц внизу
// по центру, оглавление с настоящими номерами (два прохода), не больше 10 страниц.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import PDFDocument from "pdfkit";

const here = path.dirname(fileURLToPath(import.meta.url));
// Готовый PDF кладётся в docs: там лежит только то, что сдаётся на конкурс.
const docsDir = path.resolve(here, "..", "..", "docs");
const PAGE = { width: 595.28, height: 841.89 };
const M = { top: 56.7, bottom: 56.7, left: 85.04, right: 28.35 };
const TEXT_WIDTH = PAGE.width - M.left - M.right;
const BODY_SIZE = 14;
const LINE_GAP = 2;
const INDENT = 35.4;
const MAX_PAGES = 10;
const SPACE_WIDTH = 3.5;
const LINE_HEIGHT = 17.0;

const FONT_DIR = "C:/Windows/Fonts";
const FONTS = {
  regular: `${FONT_DIR}/times.ttf`,
  bold: `${FONT_DIR}/timesbd.ttf`,
  italic: `${FONT_DIR}/timesi.ttf`,
  boldItalic: `${FONT_DIR}/timesbi.ttf`,
};

const source = fs.readFileSync(path.join(here, "project-description.md"), "utf8").replace(/\r\n/g, "\n");

function parseFrontMatter(raw) {
  const match = raw.match(/^---\n([\s\S]*?)\n---\n/);
  if (!match) return { meta: {}, body: raw };
  const meta = {};
  for (const line of match[1].split("\n")) {
    const colon = line.indexOf(":");
    if (colon > 0) meta[line.slice(0, colon).trim()] = line.slice(colon + 1).trim();
  }
  return { meta, body: raw.slice(match[0].length) };
}

function parseBlocks(body) {
  const lines = body.split("\n");
  const blocks = [];
  let paragraph = [];

  const flush = () => {
    if (paragraph.length) {
      blocks.push({ type: "p", text: paragraph.join(" ") });
      paragraph = [];
    }
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();
    if (!line) {
      flush();
      continue;
    }
    if (line === "!toc") {
      flush();
      blocks.push({ type: "toc" });
      continue;
    }
    if (line.startsWith("## ")) {
      flush();
      blocks.push({ type: "h2", text: line.slice(3).trim() });
      continue;
    }
    if (line.startsWith("# ")) {
      flush();
      blocks.push({ type: "h1", text: line.slice(2).trim() });
      continue;
    }
    if (line.startsWith("- ")) {
      flush();
      blocks.push({ type: "li", text: line.slice(2).trim() });
      continue;
    }
    const ordered = line.match(/^(\d+)\.\s+(.*)$/);
    if (ordered) {
      flush();
      blocks.push({ type: "oli", number: ordered[1], text: ordered[2].trim() });
      continue;
    }
    if (line.startsWith("|")) {
      flush();
      const rows = [];
      while (i < lines.length && lines[i].trim().startsWith("|")) {
        const cells = lines[i].trim().split("|").slice(1, -1).map((cell) => cell.trim());
        const isSeparator = cells.every((cell) => /^:?-{3,}:?$/.test(cell));
        if (!isSeparator) rows.push(cells);
        i++;
      }
      i--;
      blocks.push({ type: "table", rows });
      continue;
    }
    paragraph.push(line);
  }
  flush();
  return blocks;
}

function clean(text) {
  return text.replace(/`/g, "");
}

function registerFonts(doc) {
  doc.registerFont("TNR", FONTS.regular);
  doc.registerFont("TNR-Bold", FONTS.bold);
  doc.registerFont("TNR-Italic", FONTS.italic);
  doc.registerFont("TNR-BoldItalic", FONTS.boldItalic);
}

function ensureSpace(doc, height) {
  if (doc.y + height > PAGE.height - M.bottom) {
    doc.addPage();
    doc.y = M.top;
  }
}

function tokenize(text) {
  const parts = text.split(/(\*\*[^*]+\*\*)/g).filter(Boolean);
  const tokens = [];
  for (const part of parts) {
    const bold = part.startsWith("**");
    const value = clean(bold ? part.slice(2, -2) : part);
    for (const word of value.split(/\s+/).filter(Boolean)) tokens.push({ word, bold });
  }
  return tokens;
}

function layoutLines(doc, tokens, width, indent) {
  const lines = [];
  let line = [];
  let lineWidth = 0;
  let available = width - indent;
  for (const token of tokens) {
    doc.font(token.bold ? "TNR-Bold" : "TNR").fontSize(BODY_SIZE);
    const wordWidth = doc.widthOfString(token.word);
    const gap = line.length ? SPACE_WIDTH : 0;
    if (line.length && lineWidth + gap + wordWidth > available) {
      lines.push({ words: line, width: lineWidth, available, indent: lines.length === 0 ? indent : 0 });
      line = [];
      lineWidth = 0;
      available = width;
    }
    lineWidth += (line.length ? SPACE_WIDTH : 0) + wordWidth;
    line.push({ ...token, width: wordWidth });
  }
  if (line.length) {
    lines.push({ words: line, width: lineWidth, available, indent: lines.length === 0 ? indent : 0 });
  }
  return lines;
}

function drawRich(doc, text, { x, width, indent = 0, justify = true }) {
  const lines = layoutLines(doc, tokenize(text), width, indent);
  for (let index = 0; index < lines.length; index++) {
    if (doc.y + LINE_HEIGHT > PAGE.height - M.bottom) {
      doc.addPage();
      doc.y = M.top;
    }
    const line = lines[index];
    const gaps = line.words.length - 1;
    const stretching = justify && index < lines.length - 1 && gaps > 0
      ? Math.max(0, line.available - line.width) / gaps
      : 0;
    const y = doc.y;
    let cursor = x + line.indent;
    for (const word of line.words) {
      doc.font(word.bold ? "TNR-Bold" : "TNR").fontSize(BODY_SIZE).fillColor("#000");
      doc.text(word.word, cursor, y, { lineBreak: false });
      cursor += word.width + SPACE_WIDTH + stretching;
    }
    doc.y = y + LINE_HEIGHT;
  }
}

function drawHeading(doc, block, toc, next) {
  let need = LINE_HEIGHT * 2;
  if (next && next.type === "h2") need += LINE_HEIGHT * 2;
  need += LINE_HEIGHT * (block.type === "h1" ? 2 : 1.5);
  ensureSpace(doc, need);
  if (toc) {
    toc.push({
      text: block.text,
      page: doc.bufferedPageRange().count,
      level: block.type === "h1" ? 1 : 2,
    });
  }
  doc.fillColor("#000");
  if (block.type === "h1") {
    doc.font("TNR-Bold").fontSize(BODY_SIZE).text(block.text, M.left, doc.y + 10, {
      width: TEXT_WIDTH,
      align: "center",
      lineGap: LINE_GAP,
    });
    doc.y += 4;
  } else {
    doc.font("TNR-Bold").fontSize(BODY_SIZE).text(block.text, M.left + INDENT, doc.y + 7, {
      width: TEXT_WIDTH - INDENT,
      align: "left",
      lineGap: LINE_GAP,
    });
    doc.y += 1;
  }
}

function drawParagraph(doc, text) {
  ensureSpace(doc, LINE_HEIGHT * 2);
  drawRich(doc, text, { x: M.left, width: TEXT_WIDTH, indent: INDENT });
}

function drawListItem(doc, text) {
  ensureSpace(doc, LINE_HEIGHT * 2);
  const y = doc.y;
  doc.font("TNR").fontSize(BODY_SIZE).fillColor("#000").text("•", M.left + INDENT, y, { lineBreak: false });
  drawRich(doc, text, { x: M.left + INDENT + 16, width: TEXT_WIDTH - INDENT - 16 });
}

function drawOrderedItem(doc, number, text) {
  ensureSpace(doc, LINE_HEIGHT * 2);
  const y = doc.y;
  doc.font("TNR").fontSize(BODY_SIZE).fillColor("#000").text(`${number}.`, M.left, y, { lineBreak: false });
  drawRich(doc, text, { x: M.left + 20, width: TEXT_WIDTH - 20, justify: false });
}

function columnWidths(rows) {
  const count = Math.max(...rows.map((row) => row.length));
  const weights = Array.from({ length: count }, (_, index) => {
    const longest = Math.max(6, ...rows.map((row) => clean(row[index] ?? "").length));
    return Math.sqrt(longest);
  });
  const total = weights.reduce((sum, value) => sum + value, 0);
  return weights.map((weight) => Math.max(70, Math.round((TEXT_WIDTH * weight) / total)));
}

function drawTable(doc, rows) {
  const widths = columnWidths(rows);
  const paddingX = 5;
  const paddingY = 3;
  doc.y += 6;
  for (let rowIndex = 0; rowIndex < rows.length; rowIndex++) {
    const bold = rowIndex === 0;
    doc.font(bold ? "TNR-Bold" : "TNR").fontSize(BODY_SIZE);
    const cells = rows[rowIndex];
    const heights = cells.map((cell, index) =>
      doc.heightOfString(clean(cell), { width: widths[index] - paddingX * 2, lineGap: 1 }),
    );
    const height = Math.max(...heights) + paddingY * 2;
    ensureSpace(doc, height);
    const top = doc.y;
    let x = M.left;
    for (let index = 0; index < cells.length; index++) {
      const width = widths[index];
      doc.rect(x, top, width, height).lineWidth(0.6).strokeColor("#000").stroke();
      doc.fillColor("#000").font(bold ? "TNR-Bold" : "TNR").fontSize(BODY_SIZE);
      doc.text(clean(cells[index]), x + paddingX, top + paddingY, {
        width: width - paddingX * 2,
        align: "left",
        lineGap: 1,
      });
      x += width;
    }
    doc.y = top + height;
  }
  doc.y += 8;
}

function drawTitlePage(doc, meta) {
  doc.fillColor("#000");
  const schoolLines = meta.school.includes("«")
    ? meta.school.split("«").map((part) => part.trim()).filter(Boolean)
    : [meta.school];
  schoolLines[1] = "«" + schoolLines[1];
  doc.font("TNR").fontSize(BODY_SIZE).text(schoolLines.join("\n"), M.left, M.top, {
    width: TEXT_WIDTH,
    align: "center",
    lineGap: LINE_GAP,
  });
  doc.font("TNR").fontSize(BODY_SIZE).text("ПРОЕКТНАЯ РАБОТА", M.left, 300, {
    width: TEXT_WIDTH,
    align: "center",
    characterSpacing: 1.2,
  });
  doc.font("TNR-Bold").fontSize(BODY_SIZE).text(meta.title, M.left, 352, {
    width: TEXT_WIDTH,
    align: "center",
    lineGap: 5,
  });

  const authorX = M.left + TEXT_WIDTH * 0.52;
  const authorWidth = TEXT_WIDTH * 0.48;
  doc.font("TNR").fontSize(BODY_SIZE).text("Авторы работы:", authorX, 512, { width: authorWidth });
  doc.text(`учащиеся ${meta.class} класса`, { width: authorWidth, lineGap: LINE_GAP });
  doc.text(meta.author1 + ",", { width: authorWidth, lineGap: LINE_GAP });
  doc.text(meta.author2, { width: authorWidth, lineGap: LINE_GAP });
  if (meta.supervisor) {
    doc.moveDown(0.8);
    doc.text("Руководитель:", { width: authorWidth, lineGap: LINE_GAP });
    doc.text(meta.supervisor, { width: authorWidth, lineGap: LINE_GAP });
  }

  doc.font("TNR").fontSize(BODY_SIZE).text(meta.city, M.left, PAGE.height - 130, {
    width: TEXT_WIDTH,
    align: "center",
  });
  doc.text(meta.year, M.left, PAGE.height - 105, { width: TEXT_WIDTH, align: "center" });
}

function drawToc(doc, headings, pages) {
  doc.fillColor("#000");
  doc.font("TNR-Bold").fontSize(BODY_SIZE).text("ОГЛАВЛЕНИЕ", M.left, M.top, {
    width: TEXT_WIDTH,
    align: "center",
  });
  let y = M.top + 60;
  for (const heading of headings) {
    const number = pages ? String(pages[heading.text] ?? "") : "0";
    const x = M.left + (heading.level === 2 ? 20 : 0);
    const width = TEXT_WIDTH - (heading.level === 2 ? 20 : 0);
    doc.font(heading.level === 1 ? "TNR-Bold" : "TNR").fontSize(BODY_SIZE);
    doc.text(heading.text, x, y, { width: width - 40, align: "left", lineBreak: false });
    const titleWidth = Math.min(
      width - 40,
      doc.widthOfString(heading.text),
    );
    const numberWidth = doc.widthOfString(number);
    const lineY = y + 10;
    if (titleWidth + numberWidth + 20 < width) {
      doc.save();
      doc.moveTo(x + titleWidth + 6, lineY)
        .lineTo(x + width - numberWidth - 6, lineY)
        .dash(1, { space: 3 })
        .lineWidth(0.7)
        .strokeColor("#000")
        .stroke();
      doc.restore();
    }
    doc.text(number, x + width - numberWidth - 2, y, { lineBreak: false });
    y += 26;
  }
}

function build(meta, blocks, pages) {
  const doc = new PDFDocument({
    size: "A4",
    margins: M,
    autoFirstPage: false,
    bufferPages: true,
    info: {
      Title: meta.title,
      Author: `${meta.author1}; ${meta.author2}`,
      Subject: "Проектная работа по информатике",
      Keywords: "PVZ, Пункт Выдачи Задачек, проверочные работы, формат .pvz, Vercel",
      Creator: "PVZ",
    },
  });
  registerFonts(doc);

  const chunks = [];
  doc.on("data", (chunk) => chunks.push(chunk));
  const ended = new Promise((resolve) => doc.on("end", resolve));

  const headings = [];

  doc.addPage();
  drawTitlePage(doc, meta);
  doc.addPage();
  drawToc(doc, blocks.filter((block) => block.type === "h1" || block.type === "h2"), pages);

  doc.addPage();
  doc.y = M.top;
  for (let index = 0; index < blocks.length; index++) {
    const block = blocks[index];
    if (block.type === "toc") continue;
    if (block.type === "h1" && block.text.includes("ПРИЛОЖЕНИЕ А")) {
      doc.addPage();
      doc.y = M.top;
    }
    if (block.type === "h1" || block.type === "h2") drawHeading(doc, block, headings, blocks[index + 1]);
    else if (block.type === "p") drawParagraph(doc, block.text);
    else if (block.type === "li") drawListItem(doc, block.text);
    else if (block.type === "oli") drawOrderedItem(doc, block.number, block.text);
    else if (block.type === "table") drawTable(doc, block.rows);
  }

  const total = doc.bufferedPageRange().count;
  for (let index = 1; index < total; index++) {
    doc.switchToPage(index);
    const savedBottom = doc.page.margins.bottom;
    doc.page.margins.bottom = 0;
    doc.font("TNR").fontSize(12).fillColor("#000").text(String(index + 1), 0, PAGE.height - 40, {
      width: PAGE.width,
      align: "center",
      lineBreak: false,
    });
    doc.page.margins.bottom = savedBottom;
  }

  doc.end();
  return ended.then(() => ({ buffer: Buffer.concat(chunks), headings, total }));
}

const { meta, body } = parseFrontMatter(source);
const blocks = parseBlocks(body);

const first = await build(meta, blocks, null);
const pageMap = Object.fromEntries(first.headings.map((heading) => [heading.text, heading.page]));
const second = await build(meta, blocks, pageMap);

fs.writeFileSync(path.join(docsDir, "pvz_description.pdf"), second.buffer);

console.log(`Страниц: ${second.total} (лимит ${MAX_PAGES}).`);
for (const heading of second.headings) {
  console.log(`${String(heading.page).padStart(2)}  ${heading.text}`);
}
if (second.total > MAX_PAGES) {
  console.error(`Превышен лимит страниц: ${second.total} > ${MAX_PAGES}.`);
  process.exitCode = 1;
}
