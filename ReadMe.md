# RanobeLocalLib

> Веб-приложение для работы с ранобэ: парсинг произведений и глав, хранение данных, пользовательские сессии и чтение сохранённого контента.
>
> **Проект находится в разработке.** Текущая документация описывает состояние репозитория на момент подготовки этого README и будет расширяться вместе с проектом.

---

## 🇷🇺 Русский

### О проекте

**RanobeLocalLib** — веб-проект на базе **ASP.NET Core / .NET 10**, предназначенный для работы с ранобэ и их главами.

Серверная часть предоставляет Web API, работает с базой данных PostgreSQL и содержит механизм асинхронного парсинга ранобэ. Парсер получает страницу произведения, определяет список глав, загружает содержимое глав и сохраняет произведение, главы и абзацы в базе данных.

В репозитории также присутствует отдельная директория `frontend`, предназначенная для клиентской части приложения.

Проект является **рабочей разработкой**, поэтому структура и набор возможностей могут изменяться.

### Основные возможности

- 📚 Парсинг ранобэ по указанному адресу.
- 📖 Получение списка глав произведения.
- 📄 Получение текста глав и отдельных абзацев.
- 💾 Сохранение ранобэ, глав и абзацев в PostgreSQL.
- 👤 Пользовательские сущности и авторизация.
- 🔐 Хеширование пользовательских паролей средствами ASP.NET Identity.
- 🎫 Работа с пользовательскими сессиями.
- 📑 Отслеживание прогресса чтения глав.
- ⚙️ Web API на ASP.NET Core.
- 🌐 CORS для взаимодействия серверной части с frontend.
- 📊 Отслеживание состояния выполняющегося парсинга.
- 🗄️ Подготовленная конфигурация Redis.
- 🌍 Подготовленная основа для интерфейса на нескольких языках.

### Технологии

| Компонент | Технология |
|---|---|
| Backend | ASP.NET Core |
| Runtime | .NET 10 |
| Язык | C# |
| ORM | Entity Framework Core |
| Основная БД | PostgreSQL |
| Кэш | Redis |
| Парсинг HTML | AngleSharp |
| Конфигурация окружения | dotenv.net |
| Авторизация | JWT Bearer / пользовательские сессии |
| Frontend | отдельная директория `frontend` |
| API | ASP.NET Core Controllers |

В проекте используется `update.csproj` с Target Framework `net10.0`. Среди подключённых зависимостей присутствуют PostgreSQL через Npgsql, Entity Framework Core, JWT Bearer authentication, Redis caching, `dotenv.net` и локальная библиотека AngleSharp.

---

## Архитектура

Проект разделён на несколько логических частей:

```text
RanobeLocalLib/
├── controllers/       # HTTP API контроллеры
├── frontend/          # клиентская часть приложения
├── libs/              # локальные библиотеки
├── models/             # модели приложения
├── utilities/          # вспомогательные компоненты
├── old/                # предыдущие/сохранённые части проекта
├── Properties/         # настройки проекта .NET
├── Parse.cs            # механизм парсинга ранобэ
├── Program.cs          # запуск и конфигурация ASP.NET Core
├── Result.cs           # единый результат выполнения API-операций
├── dbConnect.cs        # Entity Framework Core и модели БД
├── update.csproj       # конфигурация .NET-проекта
├── appsettings.json    # настройки приложения
└── .env.example        # пример переменных окружения
```

### Серверная часть

Точкой входа является `Program.cs`.

При запуске приложение:

1. Загружает переменные окружения.
2. Создаёт ASP.NET Core application builder.
3. Подключает `Database` через dependency injection.
4. Настраивает CORS.
5. Подключает controllers.
6. Включает routing и authorization.
7. Запускает Web API.

### Парсер

Основная логика парсинга находится в `Parse.cs`.

Для каждого пользователя может быть создан отдельный экземпляр процесса парсинга. Состояние активных парсеров хранится в `ParsersMeneger`.

Общий поток работы:

```text
URL ранобэ
    │
    ▼
Запуск Parse
    │
    ▼
Загрузка страницы произведения
    │
    ▼
Получение списка глав
    │
    ▼
Последовательная загрузка глав
    │
    ▼
Получение текста абзацев
    │
    ▼
Создание моделей Ranobe / Chapter / Paragraf
    │
    ▼
Сохранение в PostgreSQL
    │
    ▼
Завершение процесса парсинга
```

Во время работы сохраняются данные о количестве найденных глав и количестве уже обработанных глав. Метод `getStatus()` вычисляет процент выполнения.

### Модель данных

Основные сущности, определённые в `dbConnect.cs`:

- `Ranobe` — произведение.
- `Chapter` — глава произведения.
- `Paragraf` — отдельный абзац главы.
- `User` — пользователь.
- `UserCheckChapter` — информация о прогрессе пользователя по главе.
- `UserSession` — пользовательская сессия.
- `Role` — роль пользователя.
- `RanobeParser` — данные о процессе/задаче парсинга.

Связь содержимого можно представить следующим образом:

```text
Ranobe
  │
  ├── Chapter
  │      │
  │      ├── Paragraf
  │      ├── Paragraf
  │      └── ...
  │
  └── Chapter
         └── ...
```

### Результаты API

`Result.cs` содержит обобщённую модель `Result<T>`, реализующую `IResult`.

У результата есть:

- HTTP status code;
- сообщение статуса;
- значение результата.

Для успешных операций предусмотрен фабричный метод `Succesful(...)`, для ошибок — `Fail(...)`.

Ответы API возвращаются в JSON.

---

## Требования

Для серверной части необходимы:

- .NET 10 SDK;
- PostgreSQL;
- Redis — для компонентов проекта, использующих Redis;
- доступ к сети для загрузки страниц, обрабатываемых парсером.

Для frontend используются файлы из каталога `frontend`.

---

## Настройка окружения

В корне проекта находится файл:

```text
.env.example
```

В нём предусмотрены следующие переменные:

```env
DB_CONNECTION_STRING=Host=localhost;Port=5432;Username=postgres;Password=password;Database=RanobeLocalLib;
JWT_KEY=your_jwt_key
REDIS_CONNECTION=YOUR_REDIS_CONNECTION_STRING
```

Создайте `.env` на основе `.env.example` и укажите собственные значения.

### Переменные окружения

| Переменная | Назначение |
|---|---|
| `DB_CONNECTION_STRING` | Строка подключения к PostgreSQL |
| `JWT_KEY` | Ключ, используемый для JWT |
| `REDIS_CONNECTION` | Строка подключения к Redis |

> Не добавляйте рабочие секреты и пароли в Git-репозиторий.

---

## Запуск Backend

Перейдите в каталог проекта:

```bash
cd RanobeLocalLib
```

Восстановите зависимости:

```bash
dotnet restore
```

Запустите приложение:

```bash
dotnet run
```

Для запуска в режиме разработки:

```bash
dotnet run --environment Development
```

Конкретный адрес HTTP/HTTPS определяется конфигурацией запуска ASP.NET Core и локальным окружением.

---

## PostgreSQL

Проект использует PostgreSQL через Entity Framework Core.

Подключение создаётся через переменную:

```env
DB_CONNECTION_STRING
```

Пример:

```env
DB_CONNECTION_STRING=Host=localhost;Port=5432;Username=postgres;Password=password;Database=RanobeLocalLib;
```

Контекст базы данных представлен классом `Database`.

В нём зарегистрированы наборы данных для:

```text
Ranobes
Chapters
Paragrafs
RanobeParsers
Users
UserCheckChapters
UserSessions
Roles
```

---

## Redis

Для Redis предусмотрена переменная:

```env
REDIS_CONNECTION
```

Настройка Redis может использоваться компонентами проекта, связанными с кэшированием.

---

## Frontend

Клиентская часть расположена в:

```text
frontend/
```

Backend настроен для взаимодействия с клиентом через CORS policy `NextJSPolicy`.

В текущей конфигурации разрешены следующие origins:

```text
http://localhost:3000
http://127.0.0.1:3000
http://192.168.1.186:3000
```

Frontend следует запускать из каталога `frontend` с использованием его текущей конфигурации и package manager, определённого файлами самого frontend-проекта.

---

## Работа с парсингом

Парсер запускается через `ParsersMeneger`.

Для запуска процесса используется URL ранобэ и идентификатор пользователя.

Логика процесса:

```text
ParsersMeneger.Start(url, userId)
        │
        ▼
Создание Parse
        │
        ▼
Добавление процесса в список активных
        │
        ▼
Загрузка страницы ранобэ
        │
        ▼
Поиск глав
        │
        ▼
Обработка глав
        │
        ▼
Сохранение результата
        │
        ▼
Удаление процесса из активных
```

Для получения текущего процесса используется:

```text
ParsersMeneger.GetParse(userId)
```

После завершения процесс удаляется через:

```text
ParsersMeneger.Remove(userId)
```

---

## Поддержка языков

Проект рассчитан на многоязычное развитие интерфейса.

Документация предоставляется на:

- 🇷🇺 Русском;
- 🇬🇧 English.

Поддержка дополнительных языков интерфейса может добавляться по мере развития frontend-части проекта.

---

## Структура данных чтения

Для отслеживания прогресса чтения используется `UserCheckChapter`.

Для пользователя сохраняются:

- идентификатор пользователя;
- идентификатор главы;
- идентификатор ранобэ;
- номер прочитанного абзаца;
- номер главы;
- дата изменения прогресса.

Это позволяет хранить состояние чтения отдельно для каждого пользователя.

---

## Разработка

Проект находится в активной разработке.

Структура проекта и отдельные компоненты могут изменяться по мере реализации новых возможностей.

Текущая архитектура предусматривает дальнейшее развитие:

- серверного API;
- клиентского интерфейса;
- системы пользователей;
- чтения ранобэ;
- парсинга;
- хранения данных;
- кэширования;
- многоязычного интерфейса.

---

## Лицензия

В текущем репозитории отдельный файл лицензии в корне проекта не представлен. Условия распространения следует уточнять по актуальному состоянию репозитория.

---

## Ссылки

- Репозиторий: https://github.com/Aleksey10832/RanobeLocalLib
- Автор: https://github.com/Aleksey10832

---

# 🇬🇧 English

## About

**RanobeLocalLib** is a web application for working with light novels: parsing novels and chapters, storing their content, managing users and reading progress.

The backend is built with **ASP.NET Core / .NET 10** and uses PostgreSQL through Entity Framework Core. The repository also contains a separate `frontend` directory for the client-side application.

> **The project is currently under development.** This README describes the current repository structure and implemented components and may be expanded as development continues.

## Features

- 📚 Parse a novel from a provided URL.
- 📖 Detect and process novel chapters.
- 📄 Extract chapter paragraphs.
- 💾 Store novels, chapters and paragraphs in PostgreSQL.
- 👤 User entities and authentication-related functionality.
- 🔐 Password hashing through ASP.NET Identity.
- 🎫 User sessions.
- 📑 Reading progress tracking.
- ⚙️ ASP.NET Core Web API.
- 🌐 CORS support for the frontend.
- 📊 Parsing progress tracking.
- 🗄️ Redis configuration.
- 🌍 Foundation for a multilingual user interface.

## Technology Stack

| Component | Technology |
|---|---|
| Backend | ASP.NET Core |
| Runtime | .NET 10 |
| Language | C# |
| ORM | Entity Framework Core |
| Database | PostgreSQL |
| Cache | Redis |
| HTML parsing | AngleSharp |
| Environment configuration | dotenv.net |
| Authentication | JWT Bearer / user sessions |
| Frontend | `frontend/` |
| API | ASP.NET Core Controllers |

## Project Structure

```text
RanobeLocalLib/
├── controllers/       # HTTP API controllers
├── frontend/          # client-side application
├── libs/              # local libraries
├── models/             # application models
├── utilities/          # utility components
├── old/                # previous/saved project parts
├── Properties/         # .NET project settings
├── Parse.cs            # novel parsing logic
├── Program.cs          # ASP.NET Core startup
├── Result.cs           # API result abstraction
├── dbConnect.cs        # EF Core database context and entities
├── update.csproj       # .NET project configuration
├── appsettings.json    # application settings
└── .env.example        # environment variable example
```

## Parsing Flow

```text
Novel URL
   │
   ▼
Start parser
   │
   ▼
Load novel page
   │
   ▼
Find chapters
   │
   ▼
Load chapter pages
   │
   ▼
Extract paragraphs
   │
   ▼
Create data models
   │
   ▼
Save to PostgreSQL
   │
   ▼
Finish parsing
```

The parser tracks the total number of chapters and the number of processed chapters. The parsing status can be represented as a percentage.

## Database Model

The main entities include:

- `Ranobe`
- `Chapter`
- `Paragraf`
- `User`
- `UserCheckChapter`
- `UserSession`
- `Role`
- `RanobeParser`

The content hierarchy is:

```text
Ranobe
  └── Chapter
        └── Paragraf
```

Reading progress is stored separately for users through `UserCheckChapter`.

## Configuration

Create a `.env` file based on `.env.example`:

```env
DB_CONNECTION_STRING=Host=localhost;Port=5432;Username=postgres;Password=password;Database=RanobeLocalLib;
JWT_KEY=your_jwt_key
REDIS_CONNECTION=YOUR_REDIS_CONNECTION_STRING
```

### Environment variables

| Variable | Purpose |
|---|---|
| `DB_CONNECTION_STRING` | PostgreSQL connection string |
| `JWT_KEY` | JWT-related key |
| `REDIS_CONNECTION` | Redis connection string |

Keep real secrets outside the repository.

## Backend Setup

Requirements:

- .NET 10 SDK;
- PostgreSQL;
- Redis for components using Redis;
- Internet access for pages processed by the parser.

Restore dependencies:

```bash
dotnet restore
```

Run the backend:

```bash
dotnet run
```

Run in Development environment:

```bash
dotnet run --environment Development
```

## Frontend

The frontend is located in:

```text
frontend/
```

The backend contains a CORS policy named `NextJSPolicy`.

The currently configured development origins are:

```text
http://localhost:3000
http://127.0.0.1:3000
http://192.168.1.186:3000
```

Run the frontend according to the package manager and scripts defined by the current files inside `frontend/`.

## API Result Model

`Result.cs` provides a generic `Result<T>` implementation of `IResult`.

It contains:

- HTTP status code;
- status message;
- returned value.

Successful responses are serialized as JSON values, while failed responses are returned as JSON error objects.

## Development Status

**RanobeLocalLib is under active development.**

The project architecture is intended to evolve together with:

- backend API development;
- frontend development;
- user management;
- novel reading;
- parsing;
- database storage;
- caching;
- multilingual UI support.

---

## Repository

https://github.com/Aleksey10832/RanobeLocalLib

## Author

https://github.com/Aleksey10832
