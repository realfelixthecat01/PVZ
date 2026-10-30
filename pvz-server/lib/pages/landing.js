// Порт PVZS/LandingPage.cs: сборка главной страницы с карточками сборок.
// index-shell.html — статичная HTML-копия Render() из C#, где {{RenderDownloads(downloads)}}
// заменён на плейсхолдер <!--DOWNLOADS--> (он же и есть convention для инъекции).

import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const SHELL_PATH = fileURLToPath(new URL('../../public/index-shell.html', import.meta.url));

// Порт LandingPage.FormatSize (C#):
//   bytes <= 0 -> "";  >= 1 МБ -> mb.ToString("0.#", InvariantCulture) + " МБ";
//   иначе -> Math.Max(1, bytes / 1024) + " КБ" (целочисленное деление long).
export function formatSize(bytes) {
  if (bytes <= 0) return '';
  const mb = bytes / 1024 / 1024;
  if (mb >= 1) {
    return mb.toFixed(1).replace(/\.0$/, '') + ' МБ';
  }
  return Math.max(1, Math.trunc(bytes / 1024)) + ' КБ';
}

// Порт LandingPage.RenderDownloadCard (C#).
function renderDownloadCard(item) {
  const teacher = item.role === 'teacher';
  const name = teacher ? 'PVZT' : 'PVZP';
  const descKey = teacher ? 'downloads.pvzt.desc' : 'downloads.pvzp.desc';
  if (!item.available) {
    return '<div class="dl-card disabled">\n' +
      '  <div class="dl-top"><b>' + name + '</b><span class="badge muted" data-i18n="downloads.soon">Скоро</span></div>\n' +
      '  <p data-i18n="' + descKey + '">Клиент PVZ для Windows.</p>\n' +
      '</div>';
  }
  const size = formatSize(item.sizeBytes);
  return '<div class="dl-card">\n' +
    '  <div class="dl-top"><b>' + name + '</b><span class="badge muted">' + size + '</span></div>\n' +
    '  <p data-i18n="' + descKey + '">Клиент PVZ для Windows.</p>\n' +
    '  <a class="btn primary" href="/downloads/' + item.id + '" download><span data-i18n="downloads.download">Скачать</span> ' + name + '</a>\n' +
    '</div>';
}

// Порт LandingPage.RenderDownloads (C#): пустой список -> заглушка, иначе карточки встык (string.Concat).
function renderDownloads(downloads) {
  const items = downloads || [];
  if (items.length === 0) {
    return '<p class="muted" data-i18n="downloads.none">Сборки пока не выложены.</p>';
  }
  return items.map(renderDownloadCard).join('');
}

export function renderLanding(downloads) {
  const shell = fs.readFileSync(SHELL_PATH, 'utf8');
  return shell.replace('<!--DOWNLOADS-->', () => renderDownloads(downloads));
}
