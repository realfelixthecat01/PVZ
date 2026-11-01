// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

(() => {
  const DICT = {
    ru: {
      'nav.home': 'Главная',
      'nav.admin': 'Панель учителя',
      'nav.cabinet': 'Кабинет',
      'foot.line': '© Пункт Выдачи Задачек. Задачник, который приходит и уходит сам.',
      'foot.admin': 'Панель учителя',
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
      'err.network': 'Сервер не отвечает. Проверьте интернет.',
      'err.http': 'Ошибка сервера',
      'err.required': 'Заполните все поля.',
      'landing.hero.badge': 'Школа · Информатика · Математика · Английский',
      'landing.hero.title': 'Задачник, который приходит и уходит сам.',
      'landing.hero.lead': 'Учитель собирает задачник, выдаёт шестисимвольный токен ученикам и видит прогресс в реальном времени. Всё бесплатно: задачник лежит в облаке столько, сколько выберет учитель.',
      'landing.hero.cta1': 'Открыть панель учителя',
      'landing.p1.role': 'для учителя',
      'landing.p1.desc': 'Редактор задачников с тестами, пропусками, вариантами ответа, выгрузкой в exe, загрузкой в облако и живой доской результатов.',
      'landing.p1.li1': 'Собирает оффлайн .pvz и самодостаточный exe',
      'landing.p1.li2': 'Загружает задачник в облако одним кликом',
      'landing.p1.li3': 'Видит прогресс каждого ученика в реальном времени',
      'landing.p2.role': 'для ученика',
      'landing.p2.desc': 'Один exe, никакого Python и Pascal устанавливать не надо. Открывает оффлайн или по токену.',
      'landing.p2.li1': 'Запускает Python и Pascal ABC через встроенные раннеры',
      'landing.p2.li2': 'Шлёт прогресс учителю, пока задачник открыт',
      'landing.p2.li3': 'Тёмная тема и подсказки, которые не бесят',
      'landing.p3.desc': 'Сервер, который хранит задачник ровно столько, сколько выбрал учитель. Потом автоматически удаляет.',
      'landing.p3.li1': 'Токены из 6 символов, пароль учителя проверяет сервер',
      'landing.p3.li2': 'Аккаунты и каталог хранятся в облаке',
      'landing.p3.li3': 'HTTPS и ограничение частоты запросов из коробки',
      'downloads.badge': 'Скачать приложения',
      'downloads.title': 'Клиенты для учителя и ученика',
      'downloads.lead': 'Сборки для Windows 10/11 (64 бита). Установка не нужна: просто запустите файл.',
      'downloads.download': 'Скачать',
      'downloads.soon': 'Скоро',
      'downloads.none': 'Сборки пока не выложены.',
      'downloads.pvzt.desc': 'Редактор задачников: тесты, пропуски, варианты ответа, соответствия, выгрузка в exe, загрузка в облако и живая доска результатов.',
      'downloads.pvzp.desc': 'Клиент ученика: открывает задачник оффлайн или по токену и шлёт прогресс учителю.',
      'auth.login.title': 'Вход для учителя',
      'auth.login.lead': 'Вход по имени аккаунта и паролю. Ученики видят свои результаты, учителя получают личный кабинет.',
      'auth.login.btn': 'Войти',
      'auth.login.no': 'Нет аккаунта?',
      'auth.login.reg': 'Зарегистрироваться',
      'auth.login.ph': 'masha.ivanova или teacher@school.by',
      'auth.reg.title': 'Регистрация учителя',
      'auth.reg.lead': 'Имя аккаунта, ФИО, школа и email: этого достаточно, чтобы получить личный кабинет.',
      'auth.reg.btn': 'Создать аккаунт',
      'auth.reg.have': 'Уже есть аккаунт?',
      'auth.reg.login': 'Войти',
      'auth.name': 'ФИО',
      'auth.name.ph': 'Иванова Мария Петровна',
      'auth.username': 'Имя аккаунта',
      'auth.username.ph': 'masha.ivanova',
      'auth.username.hint': 'Латинские буквы, цифры, точка, дефис или подчёркивание, от 3 до 32 символов.',
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
      'cabinet.lead': 'Профиль и файлы в облаке с таймером удаления.',
      'cabinet.guest.title': 'Войдите в кабинет',
      'cabinet.guest.desc': 'Кабинет хранит задачники в облаке. Войдите или зарегистрируйтесь.',
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
      'cabinet.storage': 'Хранилище',
      'cabinet.storage.used': 'занято',
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
      'nav.admin': 'Teacher panel',
      'nav.cabinet': 'Cabinet',
      'foot.line': '© PVZ — a taskbook that comes and goes on its own.',
      'foot.admin': 'Teacher panel',
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
      'err.network': 'The server is not responding. Check your connection.',
      'err.http': 'Server error',
      'err.required': 'Fill in every field.',
      'landing.hero.badge': 'School · Informatics · Math · English',
      'landing.hero.title': 'A taskbook that comes and goes on its own.',
      'landing.hero.lead': 'A teacher builds a taskbook, hands a six-character token to students and watches progress live. Fully free: a taskbook stays in the cloud for as long as the teacher picks.',
      'landing.hero.cta1': 'Open the teacher panel',
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
      'landing.p3.li2': 'Accounts and the catalog live in the cloud',
      'landing.p3.li3': 'HTTPS and request rate limits out of the box',
      'downloads.badge': 'Download apps',
      'downloads.title': 'Clients for the teacher and the student',
      'downloads.lead': 'Windows 10/11 (64-bit) builds. No installation — just run the file.',
      'downloads.download': 'Download',
      'downloads.soon': 'Coming soon',
      'downloads.none': 'No builds published yet.',
      'downloads.pvzt.desc': 'Taskbook editor: tests, .exe export, cloud upload and a live results board.',
      'downloads.pvzp.desc': 'Student client: opens a taskbook offline or by token and reports progress.',
      'auth.login.title': 'Teacher sign in',
      'auth.login.lead': 'Sign in with your account name and password. The cabinet keeps taskbooks in the cloud and shows what students have solved.',
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
      'cabinet.lead': 'Profile and cloud files with deletion timers.',
      'cabinet.guest.title': 'Sign in to your cabinet',
      'cabinet.guest.desc': 'The cabinet stores taskbooks in the cloud. Sign in or register.',
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
      'cabinet.storage': 'Storage',
      'cabinet.storage.used': 'used',
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
  const TOKEN_KEY = 'pvzAccountToken';

  const readSetting = (key, fallback) => {
    try { return localStorage.getItem(key) || fallback; } catch (_) { return fallback; }
  };
  const writeSetting = (key, value) => {
    try { localStorage.setItem(key, value); } catch (_) {}
  };

  let lang = readSetting('pvzLang', 'ru') === 'en' ? 'en' : 'ru';
  const listeners = [];

  const t = (key) => {
    const table = DICT[lang] || DICT.ru;
    return Object.prototype.hasOwnProperty.call(table, key) ? table[key] : key;
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

  const syncSwitchers = () => {
    document.querySelectorAll('[data-lang]').forEach((button) => {
      button.classList.toggle('active', button.getAttribute('data-lang') === lang);
    });
  };

  const notify = () => listeners.forEach((callback) => callback());

  const setLang = (next) => {
    lang = next === 'en' ? 'en' : 'ru';
    writeSetting('pvzLang', lang);
    syncSwitchers();
    applyTranslations();
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
    locale: () => (lang === 'ru' ? 'ru-RU' : 'en-US'),
    lang: () => lang,
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
  syncSwitchers();
  revealNow();
})();
