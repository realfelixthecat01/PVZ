(() => {
  const DICT = {
    ru: {
      'nav.home': 'Главная',
      'nav.pricing': 'Тарифы',
      'nav.admin': 'Панель учителя',
      'nav.cabinet': 'Кабинет',
      'foot.line': '© Пункт Выдачи Задачек — задачник, который приходит и уходит сам.',
      'foot.pricing': 'Тарифы',
      'foot.admin': 'Панель учителя',
      'unit.month': '/мес',
      'unit.h': 'ч',
      'unit.min': 'мин',
      'unit.d': 'д',
      'unit.lessMin': 'меньше минуты',
      'retention.hours48': '48 часов',
      'retention.week1': '1 неделя',
      'retention.month1': '1 месяц',
      'retention.year1': '1 год',
      'retention.forever': 'навсегда',
      'retention.twoWeeks': '2 недели',
      'subject.informatics': 'Информатика',
      'subject.english': 'Английский язык',
      'subject.math': 'Математика',
      'plan.free': 'Бесплатный',
      'plan.pro': 'Про',
      'plan.plus': 'Про+',
      'plan.until': 'активен до',
      'err.network': 'Сервер не отвечает. Проверьте интернет.',
      'err.http': 'Ошибка сервера',
      'err.required': 'Заполните все поля.',
      'landing.hero.badge': 'Школа · Информатика · Математика · Английский',
      'landing.hero.title': 'Задачник, который приходит и уходит сам.',
      'landing.hero.lead': 'Учитель собирает задачник, выдаёт шестисимвольный токен ученикам и видит прогресс в реальном времени. Бесплатно — до 5 задачников на 48 часов: сервер удаляет их сам.',
      'landing.hero.cta1': 'Открыть панель учителя',
      'landing.hero.cta2': 'Посмотреть тарифы',
      'landing.p1.role': '— учитель',
      'landing.p1.desc': 'Редактор задачников с тестами, выгрузкой в exe, загрузкой в облако и живой доской результатов.',
      'landing.p1.li1': 'Собирает оффлайн .pvz и самодостаточный exe',
      'landing.p1.li2': 'Загружает задачник в облако одним кликом',
      'landing.p1.li3': 'Видит прогресс каждого ученика в реальном времени',
      'landing.p2.role': '— ученик',
      'landing.p2.desc': 'Один exe, никакого Python и Pascal устанавливать не надо. Открывает оффлайн или по токену.',
      'landing.p2.li1': 'Запускает Python и Pascal ABC через встроенные раннеры',
      'landing.p2.li2': 'Шлёт прогресс учителю, пока задачник открыт',
      'landing.p2.li3': 'Тёмная тема и подсказки, которые не бесят',
      'landing.p3.desc': 'Сервер, который хранит задачник ровно столько, сколько выбрал учитель. Потом автоматически удаляет.',
      'landing.p3.li1': 'Токены из 6 символов, пароль учителя проверяет сервер',
      'landing.p3.li2': 'Аккаунты, ключи активации и каталог хранятся в облаке',
      'landing.p3.li3': 'HTTPS и ограничение частоты запросов из коробки',
      'landing.tiers.badge': 'Тарифы хранения',
      'landing.tiers.title': 'Храните задачник столько, сколько нужно.',
      'landing.tiers.lead': 'Тариф включается ключом активации в личном кабинете — без автосписаний и привязки карты.',
      'landing.tiers.free.desc': 'До 5 задачников одновременно. Каждый файл живёт 48 часов, потом сервер удаляет его сам.',
      'landing.tiers.pro.desc': '20 задачников на 48 часов, 15 на неделю, 5 на месяц и 1 на год. Срок выбирается при загрузке.',
      'landing.tiers.plus.desc': '48 часов, неделя и месяц без лимитов, 100 файлов на год и 20 задачников навсегда.',
      'landing.tiers.cta': 'Все тарифы',
      'landing.store.badge': 'Каталог задачников',
      'landing.store.title': 'Готовые задачники от других учителей.',
      'landing.store.desc': 'Выкладывайте свои задачники и забирайте чужие по подписке: на один класс, на предмет или на весь каталог.',
      'landing.store.sub.a': 'Подписка на предмет —',
      'landing.store.sub.b': 'на класс —',
      'landing.store.sub.c': 'все предметы —',
      'landing.store.gated': 'по подписке',
      'landing.shelf1': 'Информатика · 7 класс',
      'landing.shelf1.sub': 'Циклы и условия · 12 задач',
      'landing.shelf2': 'Математика · 5 класс',
      'landing.shelf2.sub': 'Дроби · 10 задач',
      'landing.shelf3': 'Английский · 4 класс',
      'landing.shelf3.sub': 'Present Simple · 8 задач',
      'landing.shelf4': 'Информатика · 10 класс',
      'landing.shelf4.sub': 'ЕГЭ · задача 24',
      'downloads.badge': 'Скачать приложения',
      'downloads.title': 'Клиенты для учителя и ученика',
      'downloads.lead': 'Сборки для Windows 10/11 (64 бита). Установка не нужна — просто запустите файл.',
      'downloads.download': 'Скачать',
      'downloads.soon': 'Скоро',
      'downloads.none': 'Сборки пока не выложены.',
      'downloads.pvzt.desc': 'Редактор задачников: тесты, выгрузка в exe, загрузка в облако и живая доска результатов.',
      'downloads.pvzp.desc': 'Клиент ученика: открывает задачник оффлайн или по токену и шлёт прогресс учителю.',
      'pricing.hero.badge': 'Ключ активации · без автосписаний',
      'pricing.hero.title': 'Просто: храните свои задачники и скачивайте готовые.',
      'pricing.hero.lead': 'Есть два вида ключей. Ключ хранения — сколько ваших задачников лежит в облаке и как долго. Ключ каталога — доступ к готовым задачникам. Оба вводятся в личном кабинете.',
      'pricing.free.tag': 'чтобы попробовать',
      'pricing.free.li1': 'До 5 своих задачников',
      'pricing.free.li2': 'Каждый хранится 48 часов, потом удаляется',
      'pricing.free.li3': 'Живая доска результатов класса',
      'pricing.free.li4': 'Заводить учеников не нужно',
      'pricing.pro.tag': 'на четверть',
      'pricing.pro.li1': '20 задачников на 48 часов',
      'pricing.pro.li2': '15 — на неделю',
      'pricing.pro.li3': '5 — на месяц',
      'pricing.pro.li4': '1 — на год',
      'pricing.plus.tag': 'для больших потоков',
      'pricing.plus.li1': '48 часов — без лимита',
      'pricing.plus.li2': 'Неделя — без лимита',
      'pricing.plus.li3': 'Месяц — без лимита',
      'pricing.plus.li4': '100 задачников на год',
      'pricing.plus.li5': '20 задачников навсегда',
      'pricing.plan.keynote': 'Ключ выдаёт администратор школы',
      'pricing.plan.freebtn': 'Начать бесплатно',
      'pricing.plan.activate': 'Ввести ключ',
      'pricing.sub.title': 'Подписка на каталог',
      'pricing.sub.lead': 'Открывает скачивание готовых задачников. Ключ действует 30 дней.',
      'pricing.sub.subject.name': 'Один предмет',
      'pricing.sub.subject.desc': 'Все классы выбранного предмета.',
      'pricing.sub.class.name': 'Один класс',
      'pricing.sub.class.desc': 'Один предмет и один класс.',
      'pricing.sub.all.name': 'Все предметы',
      'pricing.sub.all.desc': 'Весь каталог: все предметы и классы.',
      'pricing.sub.best': 'выгоднее всего',
      'pricing.table.title': 'Сколько задачников помещается',
      'pricing.table.lead': 'Лимит своих задачников на каждом сроке хранения.',
      'pricing.table.term': 'Срок хранения',
      'pricing.table.hours': '48 часов',
      'pricing.table.week': '1 неделя',
      'pricing.table.month': '1 месяц',
      'pricing.table.year': '1 год',
      'pricing.table.forever': 'Навсегда',
      'pricing.table.unlimited': 'без лимита',
      'pricing.how.title': 'Как это работает',
      'pricing.how.s1': 'Получите ключ у администратора школы.',
      'pricing.how.s2': 'Введите ключ в личном кабинете.',
      'pricing.how.s3': 'Тариф включится сразу.',
      'pricing.faq.title': 'Вопросы и ответы',
      'pricing.faq.q1': 'Нужно ли заводить учеников?',
      'pricing.faq.a1': 'Нет. Ученик просто вводит имя, фамилию и класс.',
      'pricing.faq.q2': 'Что будет с файлом через 48 часов?',
      'pricing.faq.a2': 'На бесплатном тарифе сервер удалит его сам. Загрузите снова или включите «Про».',
      'pricing.faq.q3': 'Как оплатить?',
      'pricing.faq.a3': 'Оплата не на сервере: администратор выдаёт ключ, вы вводите его в кабинете. Автосписаний нет.',
      'pricing.faq.q4': 'А если на уроке нет интернета?',
      'pricing.faq.a4': 'Откройте задачник файлом .pvz — прогресс сохранится и уйдёт на сервер, когда сеть вернётся.',
      'pricing.note': 'Цены в долларах; BYN и RUB — по курсу сервера.',
      'auth.login.title': 'Вход для учителя',
      'auth.login.lead': 'Вход по имени аккаунта и паролю. Кабинет хранит задачники в облаке и включает тарифы по ключам активации.',
      'auth.login.btn': 'Войти',
      'auth.login.no': 'Нет аккаунта?',
      'auth.login.reg': 'Зарегистрироваться',
      'auth.login.ph': 'masha.ivanova или teacher@school.by',
      'auth.reg.title': 'Регистрация учителя',
      'auth.reg.lead': 'Имя аккаунта, ФИО, школа и email — этого достаточно, чтобы получить личный кабинет.',
      'auth.reg.btn': 'Создать аккаунт',
      'auth.reg.have': 'Уже есть аккаунт?',
      'auth.reg.login': 'Войти',
      'auth.name': 'ФИО',
      'auth.name.ph': 'Иванова Мария Петровна',
      'auth.username': 'Имя аккаунта',
      'auth.username.ph': 'masha.ivanova',
      'auth.username.hint': 'Латинские буквы, цифры, точка, дефис или подчёркивание — от 3 до 32 символов.',
      'auth.school': 'Школа или гимназия',
      'auth.school.ph': 'Гимназия №1, Минск',
      'auth.email': 'Email',
      'auth.email.ph': 'teacher@school.by',
      'auth.password': 'Пароль',
      'auth.password.ph': 'Минимум 6 символов',
      'auth.logout': 'Выйти',
      'auth.err.login': 'Неверное имя аккаунта или пароль.',
      'auth.err.throttle': 'Слишком много попыток. Подождите минуту.',
      'cabinet.title': 'Личный кабинет',
      'cabinet.lead': 'Профиль, тарифы и файлы в облаке с таймером удаления.',
      'cabinet.guest.title': 'Войдите в кабинет',
      'cabinet.guest.desc': 'Кабинет хранит задачники в облаке и активирует ключи. Войдите или зарегистрируйтесь.',
      'cabinet.guest.login': 'Войти',
      'cabinet.guest.reg': 'Регистрация',
      'cabinet.profile': 'Профиль',
      'cabinet.username': 'Имя аккаунта',
      'cabinet.profile.save': 'Сохранить',
      'cabinet.profile.saved': 'Профиль обновлён.',
      'cabinet.pwd': 'Смена пароля',
      'cabinet.pwd.current': 'Текущий пароль',
      'cabinet.pwd.new': 'Новый пароль',
      'cabinet.pwd.save': 'Обновить пароль',
      'cabinet.pwd.saved': 'Пароль обновлён.',
      'cabinet.plan': 'Тариф',
      'cabinet.subs': 'Подписки на каталог',
      'cabinet.subs.none': 'Подписок нет. Активируйте ключ на предмет или класс.',
      'cabinet.sub.subject': 'Предмет',
      'cabinet.sub.class': 'Класс',
      'cabinet.sub.allSubjects': 'Все предметы',
      'cabinet.sub.allClasses': 'все классы',
      'cabinet.sub.until': 'до',
      'cabinet.storage': 'Хранилище',
      'cabinet.storage.used': 'занято',
      'cabinet.storage.unlimited': 'без лимита',
      'cabinet.key': 'Ключ активации',
      'cabinet.key.ph': 'PVZ-XXXX-XXXX-XXXX',
      'cabinet.key.btn': 'Активировать',
      'cabinet.files': 'Файлы в облаке',
      'cabinet.files.none': 'Файлов пока нет. Загрузите .pvz из PVZT или через форму ниже.',
      'cabinet.files.uploaded': 'Загружен',
      'cabinet.files.expires': 'Удаление через',
      'cabinet.files.tasks': 'Задач',
      'cabinet.files.students': 'Учеников',
      'cabinet.files.download': 'Скачать',
      'cabinet.files.delete': 'Удалить',
      'cabinet.files.confirm': 'Удалить файл? Ученики потеряют доступ, файл будет удалён с сервера.',
      'cabinet.upload': 'Загрузить задачник',
      'cabinet.upload.term': 'Срок хранения',
      'cabinet.upload.btn': 'Загрузить в облако',
      'cabinet.upload.ok': 'Задачник загружен. Токен для учеников: ',
      'cabinet.upload.noFile': 'Выберите файл .pvz.',
      'cabinet.logout': 'Выйти',
    },
    en: {
      'nav.home': 'Home',
      'nav.pricing': 'Pricing',
      'nav.admin': 'Teacher panel',
      'nav.cabinet': 'Cabinet',
      'foot.line': '© PVZ — a taskbook that comes and goes on its own.',
      'foot.pricing': 'Pricing',
      'foot.admin': 'Teacher panel',
      'unit.month': '/mo',
      'unit.h': 'h',
      'unit.min': 'min',
      'unit.d': 'd',
      'unit.lessMin': 'under a minute',
      'retention.hours48': '48 hours',
      'retention.week1': '1 week',
      'retention.month1': '1 month',
      'retention.year1': '1 year',
      'retention.forever': 'forever',
      'retention.twoWeeks': '2 weeks',
      'subject.informatics': 'Informatics',
      'subject.english': 'English',
      'subject.math': 'Math',
      'plan.free': 'Free',
      'plan.pro': 'Pro',
      'plan.plus': 'Pro+',
      'plan.until': 'active until',
      'err.network': 'The server is not responding. Check your connection.',
      'err.http': 'Server error',
      'err.required': 'Fill in every field.',
      'landing.hero.badge': 'School · Informatics · Math · English',
      'landing.hero.title': 'A taskbook that comes and goes on its own.',
      'landing.hero.lead': 'A teacher builds a taskbook, hands a six-character token to students and watches progress live. Free: up to 5 taskbooks for 48 hours — the server deletes them on its own.',
      'landing.hero.cta1': 'Open the teacher panel',
      'landing.hero.cta2': 'See pricing',
      'landing.p1.role': '— teacher',
      'landing.p1.desc': 'Taskbook editor with tests, .exe export, cloud upload and a live results board.',
      'landing.p1.li1': 'Builds an offline .pvz and a standalone .exe',
      'landing.p1.li2': 'Uploads the taskbook to the cloud in one click',
      'landing.p1.li3': 'Shows the progress of every student in real time',
      'landing.p2.role': '— student',
      'landing.p2.desc': 'One .exe, no Python or Pascal to install. Opens offline or by token.',
      'landing.p2.li1': 'Runs Python and Pascal ABC with built-in runners',
      'landing.p2.li2': 'Sends progress to the teacher while the taskbook is open',
      'landing.p2.li3': 'Dark theme and hints that help instead of nagging',
      'landing.p3.desc': 'The server keeps a taskbook exactly as long as the teacher chose. Then deletes it automatically.',
      'landing.p3.li1': 'Six-character tokens, passwords checked on the server',
      'landing.p3.li2': 'Accounts, activation keys and the catalog live in the cloud',
      'landing.p3.li3': 'HTTPS and request rate limits out of the box',
      'landing.tiers.badge': 'Storage plans',
      'landing.tiers.title': 'Keep a taskbook for as long as you need.',
      'landing.tiers.lead': 'A plan is unlocked with an activation key in your cabinet — no auto-charges, no card on file.',
      'landing.tiers.free.desc': 'Up to 5 taskbooks at once. Every file lives 48 hours, then the server deletes it.',
      'landing.tiers.pro.desc': '20 taskbooks for 48 hours, 15 for a week, 5 for a month, 1 for a year. Pick the term at upload.',
      'landing.tiers.plus.desc': '48 hours, a week and a month without limits, 100 files for a year and 20 taskbooks forever.',
      'landing.tiers.cta': 'All plans',
      'landing.store.badge': 'Taskbook catalog',
      'landing.store.title': 'Ready-made taskbooks from other teachers.',
      'landing.store.desc': 'Publish your taskbooks and download others by subscription: per class, per subject or the whole catalog.',
      'landing.store.sub.a': 'Subject subscription —',
      'landing.store.sub.b': 'class subscription —',
      'landing.store.sub.c': 'all subjects —',
      'landing.store.gated': 'by subscription',
      'landing.shelf1': 'Informatics · Grade 7',
      'landing.shelf1.sub': 'Loops and conditions · 12 tasks',
      'landing.shelf2': 'Math · Grade 5',
      'landing.shelf2.sub': 'Fractions · 10 tasks',
      'landing.shelf3': 'English · Grade 4',
      'landing.shelf3.sub': 'Present Simple · 8 tasks',
      'landing.shelf4': 'Informatics · Grade 10',
      'landing.shelf4.sub': 'Exam · task 24',
      'downloads.badge': 'Download apps',
      'downloads.title': 'Clients for the teacher and the student',
      'downloads.lead': 'Windows 10/11 (64-bit) builds. No installation — just run the file.',
      'downloads.download': 'Download',
      'downloads.soon': 'Coming soon',
      'downloads.none': 'No builds published yet.',
      'downloads.pvzt.desc': 'Taskbook editor: tests, .exe export, cloud upload and a live results board.',
      'downloads.pvzp.desc': 'Student client: opens a taskbook offline or by token and reports progress.',
      'pricing.hero.badge': 'Activation key · no auto-charges',
      'pricing.hero.title': 'Simple: store your own taskbooks and download ready-made ones.',
      'pricing.hero.lead': 'There are two kinds of keys. A storage key sets how many of your taskbooks stay in the cloud and for how long. A catalog key unlocks ready-made taskbooks. Both are entered in your cabinet.',
      'pricing.free.tag': 'to try it out',
      'pricing.free.li1': 'Up to 5 of your own taskbooks',
      'pricing.free.li2': 'Each is kept 48 hours, then deleted',
      'pricing.free.li3': 'Live class scoreboard',
      'pricing.free.li4': 'No student sign-up needed',
      'pricing.pro.tag': 'for a school term',
      'pricing.pro.li1': '20 taskbooks for 48 hours',
      'pricing.pro.li2': '15 for a week',
      'pricing.pro.li3': '5 for a month',
      'pricing.pro.li4': '1 for a year',
      'pricing.plus.tag': 'for big classes',
      'pricing.plus.li1': '48 hours — unlimited',
      'pricing.plus.li2': 'A week — unlimited',
      'pricing.plus.li3': 'A month — unlimited',
      'pricing.plus.li4': '100 taskbooks for a year',
      'pricing.plus.li5': '20 taskbooks forever',
      'pricing.plan.keynote': 'The key is issued by the school admin',
      'pricing.plan.freebtn': 'Start for free',
      'pricing.plan.activate': 'Enter the key',
      'pricing.sub.title': 'Catalog subscription',
      'pricing.sub.lead': 'Unlocks downloading ready-made taskbooks. The key lasts 30 days.',
      'pricing.sub.subject.name': 'One subject',
      'pricing.sub.subject.desc': 'All grades of one subject.',
      'pricing.sub.class.name': 'One class',
      'pricing.sub.class.desc': 'One subject and one grade.',
      'pricing.sub.all.name': 'All subjects',
      'pricing.sub.all.desc': 'The whole catalog: every subject and grade.',
      'pricing.sub.best': 'best value',
      'pricing.table.title': 'How many taskbooks fit',
      'pricing.table.lead': 'The limit of your own taskbooks for each storage term.',
      'pricing.table.term': 'Storage term',
      'pricing.table.hours': '48 hours',
      'pricing.table.week': '1 week',
      'pricing.table.month': '1 month',
      'pricing.table.year': '1 year',
      'pricing.table.forever': 'Forever',
      'pricing.table.unlimited': 'unlimited',
      'pricing.how.title': 'How it works',
      'pricing.how.s1': 'Get a key from the school admin.',
      'pricing.how.s2': 'Enter the key in your cabinet.',
      'pricing.how.s3': 'The plan turns on right away.',
      'pricing.faq.title': 'Questions and answers',
      'pricing.faq.q1': 'Do I need to register students?',
      'pricing.faq.a1': 'No. A student just types a first name, last name and grade.',
      'pricing.faq.q2': 'What happens to a file after 48 hours?',
      'pricing.faq.a2': 'On the free plan the server deletes it. Upload again or switch to Pro.',
      'pricing.faq.q3': 'How do I pay?',
      'pricing.faq.a3': 'Payment is not on the server: the admin gives you a key and you enter it in the cabinet. No auto-charges.',
      'pricing.faq.q4': 'What if there is no internet in class?',
      'pricing.faq.a4': 'Open a taskbook as a .pvz file — progress is saved and sent to the server when the network returns.',
      'pricing.note': 'Prices are in USD; BYN and RUB follow the server rate.',
      'auth.login.title': 'Teacher sign in',
      'auth.login.lead': 'Sign in with your account name and password. The cabinet keeps taskbooks in the cloud and unlocks plans with activation keys.',
      'auth.login.ph': 'masha.ivanova or teacher@school.by',
      'auth.login.btn': 'Sign in',
      'auth.login.no': 'No account yet?',
      'auth.login.reg': 'Create one',
      'auth.reg.title': 'Teacher registration',
      'auth.reg.lead': 'An account name, name, school and email are enough to get a cabinet.',
      'auth.reg.btn': 'Create account',
      'auth.reg.have': 'Already have an account?',
      'auth.reg.login': 'Sign in',
      'auth.name': 'Full name',
      'auth.name.ph': 'Maria Petrova',
      'auth.school': 'School or gymnasium',
      'auth.school.ph': 'Gymnasium No. 1, Minsk',
      'auth.username': 'Account name',
      'auth.username.ph': 'masha.ivanova',
      'auth.username.hint': 'Latin letters, digits, dot, dash or underscore — 3 to 32 characters.',
      'auth.email': 'Email',
      'auth.email.ph': 'teacher@school.by',
      'auth.password': 'Password',
      'auth.password.ph': 'At least 6 characters',
      'auth.logout': 'Sign out',
      'auth.err.login': 'Wrong account name or password.',
      'auth.err.throttle': 'Too many attempts. Wait a minute.',
      'cabinet.title': 'Your cabinet',
      'cabinet.lead': 'Profile, plans and cloud files with deletion timers.',
      'cabinet.guest.title': 'Sign in to your cabinet',
      'cabinet.guest.desc': 'The cabinet stores taskbooks in the cloud and activates keys. Sign in or register.',
      'cabinet.guest.login': 'Sign in',
      'cabinet.guest.reg': 'Register',
      'cabinet.profile': 'Profile',
      'cabinet.username': 'Account name',
      'cabinet.profile.save': 'Save',
      'cabinet.profile.saved': 'Profile updated.',
      'cabinet.pwd': 'Change password',
      'cabinet.pwd.current': 'Current password',
      'cabinet.pwd.new': 'New password',
      'cabinet.pwd.save': 'Update password',
      'cabinet.pwd.saved': 'Password updated.',
      'cabinet.plan': 'Plan',
      'cabinet.subs': 'Catalog subscriptions',
      'cabinet.subs.none': 'No subscriptions. Activate a subject or class key.',
      'cabinet.sub.subject': 'Subject',
      'cabinet.sub.class': 'Class',
      'cabinet.sub.allSubjects': 'All subjects',
      'cabinet.sub.allClasses': 'all grades',
      'cabinet.sub.until': 'until',
      'cabinet.storage': 'Storage',
      'cabinet.storage.used': 'used',
      'cabinet.storage.unlimited': 'unlimited',
      'cabinet.key': 'Activation key',
      'cabinet.key.ph': 'PVZ-XXXX-XXXX-XXXX',
      'cabinet.key.btn': 'Activate',
      'cabinet.files': 'Cloud files',
      'cabinet.files.none': 'No files yet. Upload a .pvz from PVZT or with the form below.',
      'cabinet.files.uploaded': 'Uploaded',
      'cabinet.files.expires': 'Deletes in',
      'cabinet.files.tasks': 'Tasks',
      'cabinet.files.students': 'Students',
      'cabinet.files.download': 'Download',
      'cabinet.files.delete': 'Delete',
      'cabinet.files.confirm': 'Delete this file? Students lose access and the file is removed from the server.',
      'cabinet.upload': 'Upload a taskbook',
      'cabinet.upload.term': 'Storage term',
      'cabinet.upload.btn': 'Upload to the cloud',
      'cabinet.upload.ok': 'Taskbook uploaded. Token for students: ',
      'cabinet.upload.noFile': 'Choose a .pvz file.',
      'cabinet.logout': 'Sign out',
    },
  };

  const RETENTION_KEYS = {
    0: 'retention.hours48',
    1: 'retention.week1',
    2: 'retention.month1',
    3: 'retention.year1',
    4: 'retention.forever',
    5: 'retention.twoWeeks',
  };
  const SUBJECT_KEYS = {
    informatics: 'subject.informatics',
    english: 'subject.english',
    math: 'subject.math',
  };
  const RATES = { USD: 1, BYN: 3.25, RUB: 95 };
  const SYMBOLS = { USD: '$', BYN: 'Br', RUB: '₽' };
  const TOKEN_KEY = 'pvzAccountToken';

  const readSetting = (key, fallback) => {
    try { return localStorage.getItem(key) || fallback; } catch (_) { return fallback; }
  };
  const writeSetting = (key, value) => {
    try { localStorage.setItem(key, value); } catch (_) {}
  };

  let lang = readSetting('pvzLang', 'ru') === 'en' ? 'en' : 'ru';
  let currency = ['USD', 'BYN', 'RUB'].includes(readSetting('pvzCurrency', 'USD'))
    ? readSetting('pvzCurrency', 'USD')
    : 'USD';
  const listeners = [];

  const t = (key) => {
    const table = DICT[lang] || DICT.ru;
    return Object.prototype.hasOwnProperty.call(table, key) ? table[key] : key;
  };

  const money = (usd) => {
    const rate = RATES[currency] || 1;
    const value = usd * rate;
    const digits = currency === 'RUB' || value === 0 ? 0 : 2;
    const text = value.toLocaleString(lang === 'ru' ? 'ru-RU' : 'en-US', {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    });
    return currency === 'USD' ? SYMBOLS.USD + text : text + ' ' + SYMBOLS[currency];
  };

  const retentionLabel = (value) => t(RETENTION_KEYS[value] || 'retention.hours48');
  const subjectName = (id) => (SUBJECT_KEYS[id] ? t(SUBJECT_KEYS[id]) : id);

  const applyTranslations = () => {
    document.querySelectorAll('[data-i18n]').forEach((element) => {
      const key = element.getAttribute('data-i18n');
      const value = DICT[lang][key];
      if (typeof value === 'string') element.textContent = value;
    });
    document.querySelectorAll('[data-i18n-ph]').forEach((element) => {
      const key = element.getAttribute('data-i18n-ph');
      const value = DICT[lang][key];
      if (typeof value === 'string') element.setAttribute('placeholder', value);
    });
    document.documentElement.lang = lang;
  };

  const applyCurrency = () => {
    document.querySelectorAll('[data-price]').forEach((element) => {
      element.textContent = money(parseFloat(element.getAttribute('data-price')) || 0);
    });
  };

  const syncSwitchers = () => {
    document.querySelectorAll('[data-lang]').forEach((button) => {
      button.classList.toggle('active', button.getAttribute('data-lang') === lang);
    });
    document.querySelectorAll('[data-currency]').forEach((button) => {
      button.classList.toggle('active', button.getAttribute('data-currency') === currency);
    });
  };

  const notify = () => listeners.forEach((callback) => callback());

  const setLang = (next) => {
    lang = next === 'en' ? 'en' : 'ru';
    writeSetting('pvzLang', lang);
    syncSwitchers();
    applyTranslations();
    applyCurrency();
    notify();
  };

  const setCurrency = (next) => {
    currency = ['USD', 'BYN', 'RUB'].includes(next) ? next : 'USD';
    writeSetting('pvzCurrency', currency);
    syncSwitchers();
    applyCurrency();
    notify();
  };

  const api = async (path, options = {}) => {
    const token = readSetting(TOKEN_KEY, '');
    const headers = Object.assign({}, options.headers || {});
    if (token && !headers['Authorization']) headers['Authorization'] = 'Bearer ' + token;
    let response;
    try {
      response = await fetch(path, Object.assign({}, options, { headers }));
    } catch (_) {
      const error = new Error(t('err.network'));
      error.status = 0;
      throw error;
    }
    if (!response.ok) {
      let body = null;
      try { body = await response.json(); } catch (_) {}
      const message = body && typeof body.error === 'string' && body.error
        ? body.error
        : t('err.http') + ' (' + response.status + ')';
      const error = new Error(message);
      error.status = response.status;
      throw error;
    }
    if (response.status === 204) return null;
    const type = response.headers.get('content-type') || '';
    return type.indexOf('application/json') === 0 ? response.json() : response;
  };

  document.querySelectorAll('[data-lang]').forEach((button) => {
    button.addEventListener('click', () => setLang(button.getAttribute('data-lang')));
  });
  document.querySelectorAll('[data-currency]').forEach((button) => {
    button.addEventListener('click', () => setCurrency(button.getAttribute('data-currency')));
  });

  const revealNow = () => {
    const items = document.querySelectorAll('.reveal');
    if (!('IntersectionObserver' in window)) {
      items.forEach((element) => element.classList.add('visible'));
      return;
    }
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('visible');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.12 });
    items.forEach((element) => observer.observe(element));
  };

  window.PVZ = {
    t,
    money,
    locale: () => (lang === 'ru' ? 'ru-RU' : 'en-US'),
    lang: () => lang,
    currency: () => currency,
    api,
    onChange: (callback) => listeners.push(callback),
    getToken: () => readSetting(TOKEN_KEY, ''),
    setToken: (value) => {
      if (value) writeSetting(TOKEN_KEY, value);
      else {
        try { localStorage.removeItem(TOKEN_KEY); } catch (_) {}
      }
    },
    retentionLabel,
    subjectName,
    reveal: revealNow,
  };

  applyTranslations();
  applyCurrency();
  syncSwitchers();
  revealNow();
})();
