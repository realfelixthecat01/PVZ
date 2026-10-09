namespace PVZS;

internal static class LandingPage
{
    public static string Render(IReadOnlyList<DownloadInfoDto> downloads) => $$"""
<!DOCTYPE html>
<html lang="ru">
<head>
<title>Пункт Выдачи Задачек — задачник, который приходит и уходит сам</title>
{{SiteStyles.Head}}
<style>
  .hero { padding: 54px 0 66px; text-align: center; }
  .hero h1 { max-width: 840px; margin: 12px auto 18px; }
  .hero p.lead { max-width: 700px; margin: 0 auto 30px; font-size: 17px; color: var(--muted); }
  .hero .ctas { display: flex; gap: 12px; justify-content: center; flex-wrap: wrap; }
  .products {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 20px; margin: 20px 0 54px;
  }
  .product { padding: 26px; }
  .product h3 { margin-top: 10px; }
  .product h3 .role { color: var(--muted); font-weight: 600; font-size: 14.5px; }
  .product .icon {
    width: 42px; height: 42px; border-radius: 12px;
    background: linear-gradient(135deg, var(--accent), var(--accent-strong));
    display: inline-flex; align-items: center; justify-content: center;
    font-size: 20px; color: #fff; box-shadow: 0 0 20px rgba(129, 140, 248, .45);
  }
  .product p { font-size: 14.5px; }
  .product ul { color: var(--muted); margin: 10px 0 0; padding-left: 18px; font-size: 14.5px; }
  .product ul li { margin-bottom: 4px; }

  .downloads { padding: 30px 36px; margin-bottom: 30px; }
  .downloads .head { max-width: 660px; margin-bottom: 20px; }
  .downloads .head h2 { margin: 10px 0 6px; }
  .downloads .head p { font-size: 14.5px; }
  .downloads .dl-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 16px; }
  .dl-card {
    padding: 18px; border: 1px solid var(--border); border-radius: 12px;
    background: rgba(13, 17, 27, .5); display: flex; flex-direction: column; gap: 10px;
  }
  .dl-card .dl-top { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
  .dl-card .dl-top b { font-size: 17px; color: var(--ink); }
  .dl-card p { font-size: 13.5px; margin: 0; }
  .dl-card .btn { margin-top: auto; }
  .dl-card.disabled { opacity: .6; }

  .tiers { padding: 40px; margin-bottom: 24px; }
  .tiers header { text-align: center; max-width: 680px; margin: 0 auto 26px; }
  .tiers header p { font-size: 15px; margin: 0; }
  .tiers .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 18px; }
  .tier {
    padding: 22px; border-radius: 12px; border: 1px solid var(--border);
    background: rgba(13, 17, 27, .55); display: flex; flex-direction: column; gap: 10px;
  }
  .tier .name { font-weight: 700; font-size: 17px; color: var(--ink); display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
  .tier p { font-size: 14px; margin: 0; }
  .tier.highlight { border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent), 0 10px 30px rgba(99, 102, 241, .18); }
  .tiers .more { text-align: center; margin-top: 24px; }

  .store {
    padding: 36px; margin-bottom: 30px;
    display: grid; grid-template-columns: 1.1fr 1fr; gap: 28px; align-items: center;
  }
  .store h2 { margin: 10px 0; }
  .store p { font-size: 14.5px; }
  .store .shelf { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
  .store .shelf .item {
    padding: 12px 14px; border-radius: 10px; border: 1px solid var(--border);
    background: rgba(13, 17, 27, .5); font-size: 12.5px; color: var(--muted); position: relative;
  }
  .store .shelf .item b { color: var(--ink); display: block; font-size: 13.5px; margin-bottom: 6px; padding-right: 74px; }
  .store .shelf .item .gate { position: absolute; top: 10px; right: 10px; }
  .store .subs { margin-top: 16px; display: grid; gap: 8px; }
  .store .subs .sub {
    display: flex; justify-content: space-between; gap: 10px; align-items: center;
    padding: 10px 14px; border-radius: 10px; border: 1px dashed var(--border);
    font-size: 13.5px; color: var(--muted);
  }
  .store .subs .sub b { color: var(--ink); font-size: 15px; white-space: nowrap; }
  @media (max-width: 760px) {
    .store { grid-template-columns: 1fr; padding: 26px; }
    .hero h1 { font-size: 34px; }
    .tiers { padding: 26px; }
    .downloads { padding: 24px; }
  }
</style>
</head>
<body>
{{SiteStyles.Nav}}

<section class="hero wrap">
  <span class="badge accent reveal" data-i18n="landing.hero.badge">Школа · Информатика · Математика · Английский</span>
  <h1 class="reveal" data-i18n="landing.hero.title">Задачник, который приходит и уходит сам.</h1>
  <p class="lead reveal" data-i18n="landing.hero.lead">Учитель собирает задачник, выдаёт шестисимвольный токен ученикам и видит прогресс в реальном времени. Бесплатно — до 5 задачников на 48 часов: сервер удаляет их сам.</p>
  <div class="ctas reveal">
    <a class="btn primary" href="/admin" data-i18n="landing.hero.cta1">Открыть панель учителя</a>
    <a class="btn secondary" href="/pricing" data-i18n="landing.hero.cta2">Посмотреть тарифы</a>
  </div>
</section>

<section class="wrap">
  <div class="products">
    <div class="card product reveal">
      <div class="icon">◉</div>
      <h3>PVZT <span class="role" data-i18n="landing.p1.role">— учитель</span></h3>
      <p data-i18n="landing.p1.desc">Редактор задачников с тестами, выгрузкой в exe, загрузкой в облако и живой доской результатов.</p>
      <ul>
        <li data-i18n="landing.p1.li1">Собирает оффлайн .pvz и самодостаточный exe</li>
        <li data-i18n="landing.p1.li2">Загружает задачник в облако одним кликом</li>
        <li data-i18n="landing.p1.li3">Видит прогресс каждого ученика в реальном времени</li>
      </ul>
    </div>
    <div class="card product reveal">
      <div class="icon">◆</div>
      <h3>PVZP <span class="role" data-i18n="landing.p2.role">— ученик</span></h3>
      <p data-i18n="landing.p2.desc">Один exe, никакого Python и Pascal устанавливать не надо. Открывает оффлайн или по токену.</p>
      <ul>
        <li data-i18n="landing.p2.li1">Запускает Python и Pascal ABC через встроенные раннеры</li>
        <li data-i18n="landing.p2.li2">Шлёт прогресс учителю, пока задачник открыт</li>
        <li data-i18n="landing.p2.li3">Тёмная тема и подсказки, которые не бесят</li>
      </ul>
    </div>
    <div class="card product reveal">
      <div class="icon">☁</div>
      <h3>PVZ Cloud</h3>
      <p data-i18n="landing.p3.desc">Сервер, который хранит задачник ровно столько, сколько выбрал учитель. Потом автоматически удаляет.</p>
      <ul>
        <li data-i18n="landing.p3.li1">Токены из 6 символов, пароль учителя проверяет сервер</li>
        <li data-i18n="landing.p3.li2">Аккаунты, ключи активации и каталог в SQLite рядом с exe</li>
        <li data-i18n="landing.p3.li3">HTTPS и ограничение частоты запросов из коробки</li>
      </ul>
    </div>
  </div>
</section>

<section class="wrap">
  <div class="card downloads reveal">
    <div class="head">
      <span class="badge accent" data-i18n="downloads.badge">Скачать приложения</span>
      <h2 data-i18n="downloads.title">Клиенты для учителя и ученика</h2>
      <p data-i18n="downloads.lead">Сборки для Windows 10/11 (64 бита). Установка не нужна — просто запустите файл.</p>
    </div>
    <div class="dl-grid">
      {{RenderDownloads(downloads)}}
    </div>
  </div>
</section>

<section class="wrap">
  <div class="card tiers reveal">
    <header>
      <span class="badge gold" data-i18n="landing.tiers.badge">Тарифы хранения</span>
      <h2 style="margin-top:12px" data-i18n="landing.tiers.title">Храните задачник столько, сколько нужно.</h2>
      <p data-i18n="landing.tiers.lead">Тариф включается ключом активации в личном кабинете — без автосписаний и привязки карты.</p>
    </header>
    <div class="grid">
      <div class="tier">
        <div class="name"><span data-i18n="plan.free">Бесплатный</span><span class="badge muted" data-i18n="pricing.free.tag">для быстрых проверочных</span></div>
        <p data-i18n="landing.tiers.free.desc">До 5 задачников одновременно. Каждый файл живёт 48 часов, потом сервер удаляет его сам.</p>
      </div>
      <div class="tier highlight">
        <div class="name"><span data-i18n="plan.pro">Про</span><span class="badge accent" data-i18n="pricing.pro.tag">на четверть и больничные</span></div>
        <p data-i18n="landing.tiers.pro.desc">20 задачников на 48 часов, 15 на неделю, 5 на месяц и 1 на год. Срок выбирается при загрузке.</p>
      </div>
      <div class="tier">
        <div class="name"><span data-i18n="plan.plus">Про+</span><span class="badge accent" data-i18n="pricing.plus.tag">для потоков и олимпиад</span></div>
        <p data-i18n="landing.tiers.plus.desc">48 часов, неделя и месяц без лимитов, 100 файлов на год и 20 задачников навсегда.</p>
      </div>
    </div>
    <div class="more">
      <a class="btn primary" href="/pricing" data-i18n="landing.tiers.cta">Все тарифы</a>
    </div>
  </div>

  <div class="card store reveal">
    <div>
      <span class="badge gold" data-i18n="landing.store.badge">Каталог задачников</span>
      <h2 data-i18n="landing.store.title">Готовые задачники от других учителей.</h2>
      <p data-i18n="landing.store.desc">Выкладывайте свои задачники и забирайте чужие по подписке: на один класс, на предмет или на весь каталог.</p>
      <div class="subs">
        <div class="sub">
          <span data-i18n="landing.store.sub.a">Подписка на предмет —</span>
          <b><span data-price="9.99">$9.99</span><span data-i18n="unit.month">/мес</span></b>
        </div>
        <div class="sub">
          <span data-i18n="landing.store.sub.b">на класс —</span>
          <b><span data-price="4.99">$4.99</span><span data-i18n="unit.month">/мес</span></b>
        </div>
        <div class="sub">
          <span data-i18n="landing.store.sub.c">все предметы —</span>
          <b><span data-price="19.99">$19.99</span><span data-i18n="unit.month">/мес</span></b>
        </div>
      </div>
    </div>
    <div class="shelf">
      <div class="item"><b data-i18n="landing.shelf1">Информатика · 7 класс</b><span data-i18n="landing.shelf1.sub">Циклы и условия · 12 задач</span><span class="badge muted gate" data-i18n="landing.store.gated">по подписке</span></div>
      <div class="item"><b data-i18n="landing.shelf2">Математика · 5 класс</b><span data-i18n="landing.shelf2.sub">Дроби · 10 задач</span><span class="badge muted gate" data-i18n="landing.store.gated">по подписке</span></div>
      <div class="item"><b data-i18n="landing.shelf3">Английский · 4 класс</b><span data-i18n="landing.shelf3.sub">Present Simple · 8 задач</span><span class="badge muted gate" data-i18n="landing.store.gated">по подписке</span></div>
      <div class="item"><b data-i18n="landing.shelf4">Информатика · 10 класс</b><span data-i18n="landing.shelf4.sub">ЕГЭ · задача 24</span><span class="badge muted gate" data-i18n="landing.store.gated">по подписке</span></div>
    </div>
  </div>
</section>

{{SiteStyles.Footer}}
{{SiteStyles.Scripts}}
</body>
</html>
""";

    private static string RenderDownloads(IReadOnlyList<DownloadInfoDto> downloads)
    {
        if (downloads.Count == 0)
            return """<p class="muted" data-i18n="downloads.none">Сборки пока не выложены.</p>""";
        return string.Concat(downloads.Select(RenderDownloadCard));
    }

    private static string RenderDownloadCard(DownloadInfoDto item)
    {
        var teacher = item.Role == "teacher";
        var name = teacher ? "PVZT" : "PVZP";
        var descKey = teacher ? "downloads.pvzt.desc" : "downloads.pvzp.desc";
        if (!item.Available)
        {
            return $"""
            <div class="dl-card disabled">
              <div class="dl-top"><b>{name}</b><span class="badge muted" data-i18n="downloads.soon">Скоро</span></div>
              <p data-i18n="{descKey}">Клиент PVZ для Windows.</p>
            </div>
            """;
        }

        var size = FormatSize(item.SizeBytes);
        return $"""
        <div class="dl-card">
          <div class="dl-top"><b>{name}</b><span class="badge muted">{size}</span></div>
          <p data-i18n="{descKey}">Клиент PVZ для Windows.</p>
          <a class="btn primary" href="/downloads/{item.Id}" download><span data-i18n="downloads.download">Скачать</span> {name}</a>
        </div>
        """;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
            return "";
        var mb = bytes / 1024d / 1024d;
        return mb >= 1
            ? mb.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " МБ"
            : Math.Max(1, bytes / 1024) + " КБ";
    }
}
