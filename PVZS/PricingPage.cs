namespace PVZS;

internal static class PricingPage
{
    public static string Render() => $$"""
<!DOCTYPE html>
<html lang="ru">
<head>
<title>Тарифы — Пункт Выдачи Задачек</title>
{{SiteStyles.Head}}
<style>
  header.page { text-align: center; padding: 44px 0 10px; }
  header.page h1 { margin-bottom: 10px; }
  header.page p { max-width: 720px; margin: 0 auto; font-size: 16px; }

  .plans {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(290px, 1fr));
    gap: 22px; margin: 30px 0 14px; align-items: stretch;
  }
  .plan { padding: 30px; display: flex; flex-direction: column; }
  .plan .name { font-size: 19px; font-weight: 700; display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
  .plan .price { font-size: 42px; font-weight: 700; margin: 14px 0 4px; letter-spacing: -.02em; }
  .plan .price .per { font-size: 15px; font-weight: 400; color: var(--muted); }
  .plan .keynote { font-size: 15px; color: var(--muted); margin: 16px 0 4px; }
  .plan .tagline { color: var(--muted); font-size: 14px; margin-bottom: 18px; }
  .plan ul { list-style: none; margin: 0 0 22px; padding: 0; color: var(--muted); font-size: 14.5px; flex: 1; }
  .plan ul li { margin-bottom: 9px; padding-left: 26px; position: relative; }
  .plan ul li::before { content: "✓"; position: absolute; left: 0; color: var(--success); font-weight: 700; }
  .plan .btn { width: 100%; }
  .plan.featured { border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent), 0 16px 40px rgba(99, 102, 241, .2); position: relative; }
  .plan.featured .ribbon {
    position: absolute; top: -13px; left: 50%; transform: translateX(-50%);
    white-space: nowrap;
  }

  .subs {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 18px; margin: 26px 0 10px;
  }
  .subcard { padding: 26px; display: flex; flex-direction: column; gap: 6px; }
  .subcard h3 { margin: 0; }
  .subcard .subprice { font-size: 34px; font-weight: 700; margin: 8px 0 2px; letter-spacing: -.02em; }
  .subcard .subprice .per { font-size: 14px; font-weight: 400; color: var(--muted); }
  .subcard p { font-size: 14px; margin: 0 0 16px; flex: 1; }
  .subhead { text-align: center; margin-top: 34px; }
  .subhead h2 { margin-bottom: 8px; }
  .subhead p { max-width: 660px; margin: 0 auto; font-size: 15px; }

  .compare { padding: 30px; margin-top: 34px; overflow-x: auto; }
  .compare h2 { margin-bottom: 6px; }
  table { width: 100%; border-collapse: collapse; font-size: 14.5px; margin-top: 14px; min-width: 560px; }
  th, td { text-align: left; padding: 12px 14px; border-bottom: 1px solid rgba(55, 65, 95, .55); }
  th { color: var(--muted); font-weight: 600; font-size: 13px; text-transform: uppercase; letter-spacing: .04em; }
  tr:last-child td { border-bottom: none; }
  td.count { color: var(--ink); font-weight: 600; }
  td.no { color: var(--subtle); }

  .how { padding: 30px; margin-top: 22px; }
  .how h2 { margin-bottom: 16px; }
  .how .steps { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 16px; }
  .how .step { display: flex; gap: 14px; align-items: flex-start; }
  .how .step .num {
    flex: none; width: 30px; height: 30px; border-radius: 50%;
    display: inline-flex; align-items: center; justify-content: center;
    background: rgba(99, 102, 241, .15); color: var(--accent-strong);
    border: 1px solid rgba(99, 102, 241, .35); font-weight: 700; font-size: 14px;
  }
  .how .step p { font-size: 14px; margin: 4px 0 0; }

  .note { text-align: center; color: var(--subtle); font-size: 13.5px; margin: 26px 0 6px; }

  .faq { margin-top: 26px; }
  .faq h2 { text-align: center; margin-bottom: 18px; }
  .faq .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 16px; }
  .faq .qa { padding: 22px; }
  .faq .qa h3 { font-size: 15.5px; margin-bottom: 8px; }
  .faq .qa p { font-size: 14px; margin: 0; }
</style>
</head>
<body>
{{SiteStyles.Nav}}

<header class="page wrap">
  <span class="badge gold reveal" data-i18n="pricing.hero.badge">Ключ активации · без автосписаний</span>
  <h1 class="reveal" data-i18n="pricing.hero.title">Просто: храните свои задачники и скачивайте готовые.</h1>
  <p class="reveal" data-i18n="pricing.hero.lead">Есть два вида ключей. Ключ хранения — сколько ваших задачников лежит в облаке и как долго. Ключ каталога — доступ к готовым задачникам. Оба вводятся в личном кабинете.</p>
</header>

<section class="wrap">
  <div class="plans">
    <div class="card plan reveal">
      <div class="name"><span data-i18n="plan.free">Бесплатный</span><span class="badge muted" data-i18n="retention.hours48">48 часов</span></div>
      <div class="price"><span data-price="0">$0</span></div>
      <div class="tagline" data-i18n="pricing.free.tag">чтобы попробовать</div>
      <ul>
        <li data-i18n="pricing.free.li1">До 5 своих задачников</li>
        <li data-i18n="pricing.free.li2">Каждый хранится 48 часов, потом удаляется</li>
        <li data-i18n="pricing.free.li3">Живая доска результатов класса</li>
        <li data-i18n="pricing.free.li4">Заводить учеников не нужно</li>
      </ul>
      <a class="btn primary" href="/register" data-i18n="pricing.plan.freebtn">Начать бесплатно</a>
    </div>

    <div class="card plan featured reveal">
      <span class="badge accent ribbon" data-i18n="pricing.pro.tag">на четверть</span>
      <div class="name" data-i18n="plan.pro">Про</div>
      <div class="keynote" data-i18n="pricing.plan.keynote">Ключ выдаёт администратор школы</div>
      <ul>
        <li data-i18n="pricing.pro.li1">20 задачников на 48 часов</li>
        <li data-i18n="pricing.pro.li2">15 — на неделю</li>
        <li data-i18n="pricing.pro.li3">5 — на месяц</li>
        <li data-i18n="pricing.pro.li4">1 — на год</li>
      </ul>
      <a class="btn secondary" href="/cabinet" data-i18n="pricing.plan.activate">Ввести ключ</a>
    </div>

    <div class="card plan reveal">
      <div class="name"><span data-i18n="plan.plus">Про+</span><span class="badge accent" data-i18n="pricing.plus.tag">для больших потоков</span></div>
      <div class="keynote" data-i18n="pricing.plan.keynote">Ключ выдаёт администратор школы</div>
      <ul>
        <li data-i18n="pricing.plus.li1">48 часов — без лимита</li>
        <li data-i18n="pricing.plus.li2">Неделя — без лимита</li>
        <li data-i18n="pricing.plus.li3">Месяц — без лимита</li>
        <li data-i18n="pricing.plus.li4">100 задачников на год</li>
        <li data-i18n="pricing.plus.li5">20 задачников навсегда</li>
      </ul>
      <a class="btn secondary" href="/cabinet" data-i18n="pricing.plan.activate">Ввести ключ</a>
    </div>
  </div>
</section>

<section class="wrap">
  <header class="subhead reveal">
    <h2 data-i18n="pricing.sub.title">Подписка на каталог</h2>
    <p data-i18n="pricing.sub.lead">Открывает скачивание готовых задачников. Ключ действует 30 дней.</p>
  </header>
  <div class="subs">
    <div class="card subcard reveal">
      <span class="badge gold" data-i18n="landing.store.gated">по подписке</span>
      <h3 data-i18n="pricing.sub.subject.name">Один предмет</h3>
      <div class="subprice"><span data-price="9.99">$9.99</span><span class="per" data-i18n="unit.month">/мес</span></div>
      <p data-i18n="pricing.sub.subject.desc">Все классы выбранного предмета.</p>
      <a class="btn primary" href="/cabinet" data-i18n="pricing.plan.activate">Ввести ключ</a>
    </div>
    <div class="card subcard reveal">
      <span class="badge gold" data-i18n="landing.store.gated">по подписке</span>
      <h3 data-i18n="pricing.sub.class.name">Один класс</h3>
      <div class="subprice"><span data-price="4.99">$4.99</span><span class="per" data-i18n="unit.month">/мес</span></div>
      <p data-i18n="pricing.sub.class.desc">Один предмет и один класс.</p>
      <a class="btn primary" href="/cabinet" data-i18n="pricing.plan.activate">Ввести ключ</a>
    </div>
    <div class="card subcard reveal">
      <span class="badge gold" data-i18n="pricing.sub.best">выгоднее всего</span>
      <h3 data-i18n="pricing.sub.all.name">Все предметы</h3>
      <div class="subprice"><span data-price="19.99">$19.99</span><span class="per" data-i18n="unit.month">/мес</span></div>
      <p data-i18n="pricing.sub.all.desc">Весь каталог: все предметы и классы.</p>
      <a class="btn primary" href="/cabinet" data-i18n="pricing.plan.activate">Ввести ключ</a>
    </div>
  </div>
  <p class="note" data-i18n="pricing.note">Цены в долларах; BYN и RUB — по курсу сервера.</p>
</section>

<section class="wrap">
  <div class="card compare reveal">
    <h2 data-i18n="pricing.table.title">Сколько задачников помещается</h2>
    <p data-i18n="pricing.table.lead">Лимит своих задачников на каждом сроке хранения.</p>
    <table>
      <thead>
        <tr>
          <th data-i18n="pricing.table.term">Срок хранения</th>
          <th data-i18n="plan.free">Бесплатный</th>
          <th data-i18n="plan.pro">Про</th>
          <th data-i18n="plan.plus">Про+</th>
        </tr>
      </thead>
      <tbody>
        <tr><td data-i18n="pricing.table.hours">48 часов</td><td class="count">5</td><td class="count">20</td><td class="count" data-i18n="pricing.table.unlimited">без лимита</td></tr>
        <tr><td data-i18n="pricing.table.week">1 неделя</td><td class="no">—</td><td class="count">15</td><td class="count" data-i18n="pricing.table.unlimited">без лимита</td></tr>
        <tr><td data-i18n="pricing.table.month">1 месяц</td><td class="no">—</td><td class="count">5</td><td class="count" data-i18n="pricing.table.unlimited">без лимита</td></tr>
        <tr><td data-i18n="pricing.table.year">1 год</td><td class="no">—</td><td class="count">1</td><td class="count">100</td></tr>
        <tr><td data-i18n="pricing.table.forever">Навсегда</td><td class="no">—</td><td class="no">—</td><td class="count">20</td></tr>
      </tbody>
    </table>
  </div>

  <div class="card how reveal">
    <h2 data-i18n="pricing.how.title">Как это работает</h2>
    <div class="steps">
      <div class="step"><span class="num">1</span><p data-i18n="pricing.how.s1">Получите ключ у администратора школы.</p></div>
      <div class="step"><span class="num">2</span><p data-i18n="pricing.how.s2">Введите ключ в личном кабинете.</p></div>
      <div class="step"><span class="num">3</span><p data-i18n="pricing.how.s3">Тариф включится сразу.</p></div>
    </div>
  </div>
</section>

<section class="wrap faq">
  <h2 data-i18n="pricing.faq.title">Вопросы и ответы</h2>
  <div class="grid">
    <div class="card qa reveal">
      <h3 data-i18n="pricing.faq.q1">Нужно ли заводить учеников?</h3>
      <p data-i18n="pricing.faq.a1">Нет. Ученик просто вводит имя, фамилию и класс.</p>
    </div>
    <div class="card qa reveal">
      <h3 data-i18n="pricing.faq.q2">Что будет с файлом через 48 часов?</h3>
      <p data-i18n="pricing.faq.a2">На бесплатном тарифе сервер удалит его сам. Загрузите снова или включите «Про».</p>
    </div>
    <div class="card qa reveal">
      <h3 data-i18n="pricing.faq.q3">Как оплатить?</h3>
      <p data-i18n="pricing.faq.a3">Оплата не на сервере: администратор выдаёт ключ, вы вводите его в кабинете. Автосписаний нет.</p>
    </div>
    <div class="card qa reveal">
      <h3 data-i18n="pricing.faq.q4">А если на уроке нет интернета?</h3>
      <p data-i18n="pricing.faq.a4">Откройте задачник файлом .pvz — прогресс сохранится и уйдёт на сервер, когда сеть вернётся.</p>
    </div>
  </div>
</section>

{{SiteStyles.Footer}}
{{SiteStyles.Scripts}}
</body>
</html>
""";
}
