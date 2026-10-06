# Conference Room Booking API

REST API для пошуку, бронювання конференц-залів та розрахунку вартості оренди.
Побудовано на **ASP.NET Core 8** + **EF Core / PostgreSQL**, з автентифікацією через
**JWT** та рольовою/ownership-авторизацією, з дотриманням принципів чистої архітектури
(Clean Architecture) та практик з книги "Чистий код" Robert C. Martin.

## Як запустити в Rider

### 1. Підніміть PostgreSQL

```bash
docker run --name crb-postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
```
Або використайте власний локальний Postgres — узгодьте `ConnectionStrings:DefaultConnection`
в `appsettings.json`.

### 2. Налаштуйте секрети для локальної розробки

`Jwt:Key` і `AdminSeed:Password` у `appsettings.json` — лише плейсхолдери, їх **не можна**
комітити в git як реальні значення. Для локальної розробки:
```bash
cd src/ConferenceRoomBooking.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "будь-який-довгий-секрет-мінімум-32-символи"
dotnet user-secrets set "AdminSeed:Password" "ваш-пароль-для-дефолтного-адміна"
```
У продакшені — через змінні середовища чи секрет-менеджер хостингу, ніколи не в коді.

### 3. Встановіть .NET 8 SDK і EF Core CLI

```bash
dotnet tool install --global dotnet-ef
```

### 4. Відкрийте рішення в Rider

`ConferenceRoomBooking.sln` → Rider відновить NuGet-пакети автоматично.

### 5. Застосуйте міграції

```bash
dotnet ef database update --project src/ConferenceRoomBooking.Infrastructure --startup-project src/ConferenceRoomBooking.Api
```
Застосунок також сам викликає `Database.MigrateAsync()` при кожному старті — ручний крок
потрібен лише якщо хочете застосувати міграцію окремо від запуску Api.

### 6. Запустіть Api

```bash
dotnet run --project src/ConferenceRoomBooking.Api
```
При першому старті автоматично засіюються: 3 зали (Зал А/B/C) та один адміністратор
(`AdminSeed:Email`/`AdminSeed:Password` з конфігурації) — єдиний спосіб отримати доступ до
адмінських ендпоінтів, оскільки публічна реєстрація завжди створює лише `Customer`.

Swagger UI (`/swagger`) має кнопку **Authorize** — увійдіть через `POST /api/auth/login`,
вставте отриманий токен (без префіксу `Bearer `, його додасть сама форма), і зможете
викликати захищені ендпоінти прямо з документації.

### Тести
```bash
dotnet test
```
Повний набір — юніт- і інтеграційні тести — виконується проти **SQLite in-memory**,
без потреби в реальному Postgres (детальніше — розділ "Тестування").

## Архітектура

```
ConferenceRoomBooking.Api             ← контролери, Swagger, DI, middleware, JWT-автентифікація
        ↓ залежить від
ConferenceRoomBooking.Application     ← бізнес-логіка, DTO, сервіси, розрахунок ціни, авторизація
        ↓ залежить від
ConferenceRoomBooking.Domain          ← сутності (Room, Service, Booking, User), інтерфейси репозиторіїв
        ↑ реалізується в
ConferenceRoomBooking.Infrastructure  ← EF Core / PostgreSQL репозиторії, DbContext, JWT-генерація, сідер даних
```

Ключові принципи, яких тримається кожен шар:

- **Domain** нічого не знає про ASP.NET, EF Core, HTTP чи JWT. Сутності самі захищають
  власні інваріанти (`Room` не дає від'ємну ціну, `User` ніколи не зберігає сирий пароль —
  лише хеш, `Booking` знає, хто його власник (`UserId`), але не знає, *як* це перевіряється).
- **Application** містить use-cases (`RoomService`, `BookingService`, `ReportService`,
  `AuthService`) і працює лише через інтерфейси — `IRoomRepository`, `IUserRepository`,
  `ICurrentUserService`, `ITokenGenerator`. Завдяки цьому вся бізнес-логіка (включно з
  перевіркою "чи це моє бронювання") тестується без HTTP і без реальної БД.
  `ICurrentUserService` — свідоме архітектурне рішення: Application ніколи не бачить
  `HttpContext` напряму, лише абстракцію "хто зараз виконує запит".
- **Infrastructure** — єдине місце, яке знає про спосіб зберігання даних (`AppDbContext`,
  Postgres-репозиторії) і про механіку видачі токенів (`JwtTokenGenerator`).
- **Api** — тонкий шар: контролери, маршрутизація, `[Authorize]`/`[Authorize(Roles=...)]`,
  і один клас (`CurrentUserService`), який читає автентифікованого користувача з клеймів
  JWT і віддає його в Application через `ICurrentUserService`.
- **Репозиторії ніколи не кидають виняток "не знайдено"** — повертають `null`; що з цим
  робити — вирішує сервіс, а не шар доступу до даних.

## Автентифікація та авторизація

### Ролі

| Роль | Як отримати |
|---|---|
| `Customer` | `POST /api/auth/register` — єдиний спосіб, завжди створює `Customer` |
| `Admin` | Лише через `AdminSeed` при старті застосунку — публічного шляху стати адміном немає |

### Матриця доступу

| Ендпоінт | Хто має доступ |
|---|---|
| `POST /api/auth/register`, `POST /api/auth/login` | будь-хто (публічні) |
| `GET /api/rooms`, `GET /api/rooms/{id}`, `GET /api/rooms/available` | будь-хто (публічні) |
| `POST/PATCH/DELETE /api/rooms/*` | лише `Admin` |
| `POST /api/bookings` | будь-який автентифікований користувач |
| `POST /api/bookings/{id}/cancel` | **власник бронювання** або `Admin` |
| `GET /api/bookings`, `GET /api/bookings/{id}` | лише `Admin` |
| `GET /api/reports/*` | лише `Admin` |

### Три рівні відмови, і чому вони різні

- **`401 Unauthorized`** — немає токена, або токен недійсний/прострочений/підроблений.
  Застосунок не знає, хто ви.
  `[Authorize]`.
- **`403 Forbidden` (роль)** — застосунок знає, хто ви, але ваша роль не підходить для
  цього ендпоінта взагалі. `[Authorize(Roles = "Admin")]`.
- **`403 Forbidden` (власність)** — застосунок знає, хто ви, роль підходить, але цей
  конкретний ресурс — не ваш (`POST /api/bookings/{id}/cancel` чужого бронювання).
  Цю перевірку неможливо виразити атрибутом — вона залежить від уже завантаженого
  ресурсу, тож вона реалізована прямо в `BookingService.CancelBookingAsync` і кидає
  власний домейн-виняток (`ForbiddenException`).

### Технічні деталі, які легко упустити

- **`MapInboundClaims = false`** в налаштуваннях `AddJwtBearer` — без цього ASP.NET Core
  мовчки перемаплює клейм `"sub"` на `ClaimTypes.NameIdentifier` при читанні, і
  `CurrentUserService`, який читає саме `JwtRegisteredClaimNames.Sub`, завжди отримував би
  `null`.
- **`InvalidCredentialsException` (множина!)** — власний домейн-виняток. Є небезпечно
  схожий вбудований `System.Security.Authentication.InvalidCredentialException` (в
  однині) — переконайтесь, що в `AuthService.cs` немає `using System.Security.Authentication;`.
- **Розпливчасте повідомлення на логіні** ("Invalid email or password" для обох випадків —
  і невідомий email, і невірний пароль) — навмисний захист від user enumeration attack.
  Див. тести `Login_WrongPasswordAndUnknownEmail_AreIndistinguishable`.
- **`IPasswordHasher<User>.HashPassword(null!, password)`** — `user`-параметр ігнорується
  дефолтною PBKDF2-реалізацією; це official, задокументований спосіб використовувати
  хешер Identity без решти фреймворку.

## Модель даних (PostgreSQL)

| Таблиця | Ключові моменти |
|---|---|
| `Rooms` | `Id` (Guid, `ValueGeneratedNever()`), `Name`, `Capacity`, `BaseHourlyRate`, `IsDeleted` |
| `Services` | `RoomId` (FK → `Rooms`, `Cascade`) |
| `Users` | `Email` (унікальний індекс, нормалізований через `ToLowerInvariant()`), `PasswordHash`, `Role` |
| `Bookings` | `RoomId` (FK → `Rooms`, `Restrict`), `UserId` (FK → `Users`, `Restrict`), `SelectedServiceNames` (рядок через кому), `TotalPrice`, `Status` |

Обидва `Restrict` на `Bookings` — свідомий вибір: бронювання є історичним записом, і БД
не дозволяє видалити ні зал, ні користувача, поки на них є бронювання. Для `Services` —
`Cascade`, бо послуга без свого залу не має сенсу.

## Дані, з якими стартує застосунок

| Зал | Місткість | Базова ціна/год | Послуги |
|---|---|---|---|
| Зал А | 50 | 2000 ₴ | Проєктор (500 ₴), Wi-Fi (300 ₴), Звук (700 ₴) |
| Зал B | 100 | 3500 ₴ | Проєктор (500 ₴), Wi-Fi (300 ₴), Звук (700 ₴) |
| Зал C | 30 | 1500 ₴ | Проєктор (500 ₴), Wi-Fi (300 ₴) |

Плюс один адміністратор (`AdminSeed:Email`/`AdminSeed:Password`). Обидва сідери
ідемпотентні — повторні перезапуски не створюють дублікатів.

## Розрахунок вартості оренди

Реалізовано в `Application/Services/PricingCalculator.cs`.

| Проміжок | Коефіцієнт |
|---|---|
| 06:00–09:00 (ранкові) | ×0.90 |
| 09:00–12:00 (стандартні) | ×1.00 |
| 12:00–14:00 (пікові) | ×1.15 |
| 14:00–18:00 (стандартні) | ×1.00 |
| 18:00–23:00 (вечірні) | ×0.80 |
| 23:00–06:00 | ×1.00 (у ТЗ не описано — базовий тариф) |

Бронювання, що перетинає кілька проміжків, тарифікується по кожному сегменту окремо
(`breakdown` у відповіді API). Послуги — фіксована сума за бронювання.

## API

Повна документація — Swagger UI (`/swagger`).

### Автентифікація (`/api/auth`)
| Метод | Шлях | Опис |
|---|---|---|
| POST | `/api/auth/register` | Реєстрація нового `Customer`, повертає JWT |
| POST | `/api/auth/login` | Логін, повертає JWT |

### Зали (`/api/rooms`)
| Метод | Шлях | Доступ |
|---|---|---|
| POST | `/api/rooms` | `Admin` |
| GET | `/api/rooms` | публічний |
| GET | `/api/rooms/{id}` | публічний |
| PATCH | `/api/rooms/{id}` | `Admin` |
| DELETE | `/api/rooms/{id}` | `Admin` |
| GET | `/api/rooms/available` | публічний |

### Бронювання (`/api/bookings`)
| Метод | Шлях | Доступ |
|---|---|---|
| POST | `/api/bookings` | будь-який автентифікований |
| GET | `/api/bookings` | `Admin` |
| GET | `/api/bookings/{id}` | `Admin` |
| POST | `/api/bookings/{id}/cancel` | власник або `Admin` |

### Звіти (`/api/reports`)
| Метод | Шлях | Доступ |
|---|---|---|
| GET | `/api/reports/revenue` | `Admin` |
| GET | `/api/reports/service-popularity` | `Admin` |

### Приклад: реєстрація → бронювання

```http
POST /api/auth/register
Content-Type: application/json

{ "email": "customer@example.com", "password": "SomeStrongPassword123!" }
```
Відповідь містить `token` — додайте його як `Authorization: Bearer <token>` у наступний запит:
```http
POST /api/bookings
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json

{
  "roomId": "…GUID залу…",
  "startTime": "2027-09-01T08:00:00",
  "endTime": "2027-09-01T13:00:00",
  "selectedServices": ["Проєктор", "Wi-Fi"]
}
```

## Безпека та відмовостійкість

- **JWT-автентифікація** з рольовою (`Admin`/`Customer`) та ownership-авторизацією (див.
  розділ вище).
- **Хешування паролів** — `PasswordHasher<User>` (PBKDF2, той самий алгоритм, що й у
  повному ASP.NET Core Identity).
- **Захист від user enumeration** — однакове повідомлення на "невідомий email" і "невірний
  пароль".
- **Валідація вхідних даних** — DataAnnotations на всіх DTO.
- **Єдина обробка помилок** — `ExceptionHandlingMiddleware`, консистентний
  `application/problem+json`, маскування внутрішніх деталей на `500`.
- **Запобігання подвійному бронюванню** — перевірка перетину часу під блокуванням
  (`SemaphoreSlim` per room). Працює лише в межах одного інстансу застосунку — див.
  "Відомі обмеження".
- **Унікальний email на рівні БД** (`HasIndex(u => u.Email).IsUnique()`) — рятує від
  race condition при одночасній реєстрації, навіть якщо застосунок-рівня перевірка
  програє гонку.
- **Референційна цілісність на рівні БД** — FK з `Restrict`/`Cascade` (деталі вище).
- **Rate limiting** — `AspNetCoreRateLimit`, жорсткіше на `POST /api/bookings`.
- **М'яке видалення залів**, **CORS** з явним whitelisting для продакшн,
  **`DbContext` — Scoped, не Singleton**.

## Тестування

### Структура

- **Юніт-тести** (`PricingCalculatorTests`, `DomainEntityTests`) — чиста бізнес-логіка,
  без HTTP, без БД.
- **Інтеграційні тести** (`RoomsControllerTests`, `BookingsControllerTests`,
  `ReportsControllerTests`, `AuthControllerTests`, `AuthorizationTests`,
  `BookingOwnershipTests`, `BookingConcurrencyTests`) — реальні HTTP-запити через
  `WebApplicationFactory<Program>` проти всього застосунку (контролери, middleware, DI).

### Чому SQLite, а не Postgres, для тестів

Інтеграційні тести піднімають **named, shared-cache SQLite in-memory** базу
(`CustomWebApplicationFactory`) — реальний SQL-рушій (на відміну від EF Core InMemory
provider), без потреби в Docker/реальному Postgres для CI. Кожен тестовий клас отримує
власну, унікально названу базу; дані скидаються (`DELETE FROM ...` у правильному
FK-порядку) перед **кожним** тестовим методом.

Декілька реальних пасток, пройдених під час побудови цієї інфраструктури (повчально,
якщо торкатиметесь цього коду):
- "голий" `:memory:` ніколи не шариться між з'єднаннями, навіть з `Cache=Shared`, — базі
  потрібне явне ім'я через URI (`file:name?mode=memory&cache=shared`).
- `Database.EnsureDeletedAsync()` ненадійний для named in-memory баз — скидання даних
  зроблено через прямі `DELETE FROM` у порядку, що враховує FK (`Bookings` → `Users` →
  `Services` → `Rooms`).
- `[assembly: CollectionBehavior(DisableTestParallelization = true)]` — тестові класи не
  виконуються паралельно, щоб уникнути ресурсних конфліктів під навантаженням
  (`BookingConcurrencyTests` навмисно створює 10 одночасних запитів).

### CI

`.github/workflows/build-and-test.yml` — збірка й повний прогін тестів на кожен `push`/`pull
request` у `main` (GitHub Actions), без сервіс-контейнера БД — саме тому й обрано SQLite для
тестів. Branch protection на `main` налаштований так, щоб блокувати мердж, поки чек не
зелений.

## Відомі обмеження / що зробити далі

- **Подвійне бронювання блокується лише в межах одного інстансу застосунку**
  (`SemaphoreSlim`). При горизонтальному масштабуванні (кілька інстансів за
  балансувальником) цього недостатньо — потрібен або `EXCLUDE USING gist` на рівні
  Postgres для часових проміжків, або розподілене блокування (Redis-lock).
- **`SelectedServiceNames` зберігається рядком через кому**, а не в окремій таблиці
  `BookingServices` — свідомий компроміс заради простоти; вартість послуги на момент
  бронювання вже "заморожена" в `TotalPrice`.
- **Часові пояси** — часові мітки приймаються "як є" (local/unspecified); для
  мультирегіональної компанії варто зберігати таймзону разом із залом.
- **Відсутній "refresh token"** — JWT видається на фіксований час (`Jwt:ExpiryMinutes`,
  за замовчуванням 60 хв) без можливості оновити його без повторного логіну.
- **Немає зміни пароля/відновлення доступу** — `User.ChangePasswordHash(...)` вже
  існує на рівні домену, але жодного публічного ендпоінту під нього ще не підведено.