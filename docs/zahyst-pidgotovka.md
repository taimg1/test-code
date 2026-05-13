# Підготовка до захисту: VotingSystem

Цей документ описує архітектуру тестування, роль Docker, CI/CD і ключові рішення проєкту **VotingSystem** — щоб швидко відновити контекст перед захистом. За ідеєю він зближений до підходу **Final_Project**: окремі шари тестів, фабрика для інтеграції, фікстура з реальною БД, окремий workflow для навантаження.

---

## 1. Загальна картина проєкту

| Компонент | Призначення |
|-----------|-------------|
| `VotingSystem.Api` | ASP.NET Core Web API: вибори, кандидати, голосування, результати, health checks. |
| `tests/VotingSystem.Api.Tests` | **Юніт-тести** сервісного шару з in-memory БД. |
| `tests/VotingSystem.Api.Tests.Integration` | **Інтеграційні** тести HTTP + реальний PostgreSQL у Docker (Testcontainers). |
| `tests/VotingSystem.Api.Tests.Database` | **Тести БД**: обмеження, каскади, великий сід, агрегації. |
| `tests/VotingSystem.Api.Tests.Performance` | Скрипти **k6** (TypeScript): smoke / load / stress. |
| `VotingSystem.slnx` | Solution: API + усі тестові проєкти під `tests/`. |

**Чому так розділено:** юніт-тести дають швидкий зворотний зв’язок по бізнес-логіці; інтеграція перевіряє реальний pipeline HTTP → контролер → EF → PostgreSQL; database-тести — схему, індекси, обсяг даних; performance — поведінку під навантаженням окремо від «звичайного» CI.

---

## 2. Docker і де він з’являється

### 2.1. Testcontainers (юніт-інтеграція та database-тести)

У проєкті використовується **Testcontainers for .NET** з образом **`postgres:16`**:

- **`CustomWebApplicationFactory`** (`tests/...Integration/Infrastructure/`) — піднімає контейнер PostgreSQL, підміняє реєстрацію `VotingDbContext` на `UseNpgsql(connectionString від контейнера)`.
- **`VotingDatabaseFixture`** (`tests/...Database/`) — той самий підхід для тестів, які не запускають весь веб-хост, а працюють напряму з `VotingDbContext`.

**Навіщо Docker тут:** EF Core in-memory **не емулює** PostgreSQL (типи, обмеження, унікальні індекси, каскадні видалення). Щоб тести відображали продакшен-стек, БД піднімається як справжній інстанс у контейнері.

**Локально / на CI:** потрібен **запущений Docker** (daemon доступний). GitHub Actions `ubuntu-latest` має Docker — тести з Testcontainers там проходять.

### 2.2. GitHub Actions `services: postgres` (workflow k6)

У **`.github/workflows/k6.yml`** PostgreSQL піднімається як **service container** раннера (не Testcontainers): фіксований порт `5432`, healthcheck `pg_isready -U postgres`.

**Навіщо окремо:** k6 ганяє вже **зібраний і запущений** API процес (`dotnet run` у фоні). Testcontainers тут не обов’язковий — достатньо стандартного сервісу Actions, щоб API підключився до відомого `Host=localhost`.

---

## 3. Фабрика веб-додатку (`CustomWebApplicationFactory`)

**Клас:** `tests/VotingSystem.Api.Tests.Integration/Infrastructure/CustomWebApplicationFactory.cs`  
**База:** `WebApplicationFactory<Program>` з пакета `Microsoft.AspNetCore.Mvc.Testing`.

### Що вона робить

1. **Піднімає** `PostgreSqlContainer` і стартує його в `InitializeAsync`.
2. **`ConfigureWebHost`:** виставляє `Environment = Testing`, **видаляє** стандартну реєстрацію `DbContextOptions<VotingDbContext>` і додає **Npgsql** на рядок підключення з контейнера.
3. Після старту контейнера викликає **`MigrateAsync`** — накат міграцій на чисту БД тестів.

### Навіщо фабрика (коротко для захисту)

- Дає **реальний HTTP-клієнт** (`CreateClient()`) до того самого коду, що й у production, без ручного підняття Kestrel.
- Дозволяє **підмінити інфраструктуру** (тут — рядок підключення до тестової БД), не змінюючи продакшен-код.
- У поєднанні з **Testcontainers** — повний стек «API + PostgreSQL» в ізольованому середовищі для кожного запуску тестів.

**Паралель з Final_Project:** та сама ідея `WebApplicationFactory` + тестова БД + очищення/підготовка даних перед сценаріями.

---

## 4. Фікстура БД (`VotingDatabaseFixture`)

**Клас:** `tests/VotingSystem.Api.Tests.Database/VotingDatabaseFixture.cs`  
Реалізує **`IAsyncLifetime`**: при ініціалізації — старт PostgreSQL (Testcontainers), міграції, потім виклик **`VotingPerformanceSeed.SeedAsync`**.

### Навіщо окремо від інтеграційної фабрики

| Аспект | Інтеграція (`CustomWebApplicationFactory`) | Database fixture |
|--------|---------------------------------------------|------------------|
| Що тестується | HTTP, маршрути, серіалізація JSON | Прямі операції EF / сирий SQL, обмеження БД |
| Дані | Зазвичай **очищені** перед класом (`ElectionDbTestHelper.ClearAllAsync`) | Частина тестів спирається на **великий сід** (агрегації) |
| Хост ASP.NET | Так | Ні |

Тести на **унікальний індекс голосу** та **каскадне видалення** створюють **свої** невеликі набори даних поверх уже піднятого контейнера (новий `CreateDbContext()` на кожен тест — окремий контекст, одна спільна БД фікстури).

Тести **агрегації на сіді** (`ElectionAggregationSeedTests`) **не очищають** таблиці — вони перевіряють обсяг і важкі читання на даних, згенерованих `VotingPerformanceSeed`.

---

## 5. Допоміжні речі в тестах

### `ElectionDbTestHelper.ClearAllAsync`

Використовується в **інтеграційних** тестах перед сценаріями: видаляє `Votes` → `Candidates` → `Elections`, щоб тести не залежали від порядку виконання.

### `TestHelpers` (юніт-проєкт)

- **`CreateInMemoryContext`** — EF Core **InMemory** з унікальним ім’ям БД на тест.
- **`CreateService`** — `ElectionService` + in-memory кеш.

**Навіщо in-memory у юнітах:** швидкість і простота; бізнес-правила (статуси, кількість кандидатів, вікно дат голосування) не потребують PostgreSQL.

### `ElectionIntegrationTestData`

Фабричні методи для валідних DTO (наприклад, `CreateElectionDto`) — менше дублювання в інтеграційних тестах.

### AutoFixture (юніт)

У `ElectionService_CreateTests` використовується для **некритичних** рядкових полів (`Title`, `Description`), а дати й тип виборів задаються явно — це відповідає вимозі «шумові» поля генерувати, домен — контролювати.

---

## 6. Великий сід і performance

**`VotingSystem.Api/Data/VotingPerformanceSeed.cs`**

- Генерує порядку **~10k** рядків голосів і набір виборів/кандидатів (Bogus + детермінований `Random(42)` для відтворюваності).
- На початку: якщо в таблиці вже є вибори — **вихід** (ідемпотентність при повторному старті API).

**Де викликається**

1. **Фікстура database-тестів** — після міграцій (локальні тести обсягу/агрегації).
2. **`Program.cs`** — після `MigrateAsync`, якщо **не** `Testing` і змінна середовища **`SEED_PERFORMANCE_DATA=true`** (наприклад, у k6 workflow).

У **`Testing`** міграції та сід з `Program` не виконуються — ними керує фабрика/фікстура тестів, щоб не конфліктувати з інтеграційним життєвим циклом.

---

## 7. Health checks (важливо для k6 і моніторингу)

У **`Program.cs`**:

- **`/health/live`** — liveness: `Predicate = _ => false` означає, що жодна перевірка не реєструється як «ready» у цьому endpoint (мінімальний сигнал «процес живий»).
- **`/health/ready`** — readiness: усі зареєстровані checks (у т.ч. **`AddDbContextCheck<VotingDbContext>`** — «чи відповідає БД»).

**k6 workflow** чекає саме **`/health/ready`**, перш ніж слати навантаження — щоб API не приймав трафік до готовності БД (особливо після міграцій і сіду).

Додатково для CI: **`DISABLE_HTTPS_REDIRECT=true`**, **`ASPNETCORE_URLS`**, **`--no-launch-profile`**, щоб порт збігався з тим, на який очікує k6 (`5032` у workflow).

---

## 8. CI: `.github/workflows/ci.yml`

Послідовність:

1. Checkout, .NET SDK.
2. `dotnet restore` / `dotnet build` по **`VotingSystem.slnx`**.
3. Три кроки **`dotnet test`** на проєкти під **`tests/`**:
   - юніт;
   - інтеграція (Testcontainers + фабрика);
   - database (Testcontainers + фікстура).

**Що сказати на захисті:** один pipeline перевіряє швидкі тести, потім інтеграцію з реальною БД, потім обмеження й обсягові сценарії — піраміда тестів на практиці.

---

## 9. Окремий workflow: `.github/workflows/k6.yml`

- **Тригери:** `push`/`pull_request` на `main` (з обмеженням `paths` для API, performance-тестів і самого workflow), **`workflow_dispatch`** з вибором типу сценарію (smoke / load / stress).
- **PostgreSQL 16** як service + healthcheck **`pg_isready -U postgres`**.
- Збірка API, фоновий **`dotnet run`** з env (рядок підключення, Production, сід, без HTTPS redirect).
- Цикл очікування **`curl /health/ready`**.
- **`k6 run`** з `BASE_URL` і скриптом з `tests/VotingSystem.Api.Tests.Performance/scripts/`.
- Артефакт **`k6-results.json`**.

**Навіщо окремо від `ci.yml`:** навантажувальні тести довші, залежать від піднятого API і k6; їх ізолюють, щоб основний CI залишався швидшим і стабільнішим (аналогічно до типового розділення в курсових / production).

---

## 10. k6 (TypeScript)

- **`helpers/`** — HTTP-обгортки, `prepareActiveElection` (створення виборів, **мінімум два кандидати** — вимога домену перед `open`), голосування тощо.
- **`scripts/`** — `smoke-test`, `load-test`, `stress-test`.

Пороги (наприклад, `http_req_failed`, `http_req_duration`) задаються в `options` кожного скрипта — на захисті можна пояснити, як саме визначено «допустиму» якість під навантаженням.

---

## 11. Що порівняти з Final_Project (для усного коментаря)

Загальні **патерни**, які зазвичай збігаються:

- розділення **unit / integration / database**;
- **`WebApplicationFactory`** + підміна БД;
- **Testcontainers** для PostgreSQL у тестах;
- окремий pipeline або job для **performance**;
- **health checks** для готовності сервісу перед навантаженням;
- великий **seed** для тестів продуктивності та агрегацій.

Відмінності завжди в деталях домену (тут — вибори та голоси, у Final_Project — інший bounded context), але **підхід до тестування та Docker** формулюється однаково.

---

## 12. Типові запитання на захисті (шпаргалка)

1. **Чому не все в одному проєкті тестів?** — різна швидкість, різні залежності (in-memory vs Docker), різна мета (логіка vs контракт HTTP vs схема БД vs RPS).
2. **Навіщо фабрика, якщо є юніт-тести?** — юніти не перевіряють маршрутизацію, фільтри, серіалізацію, middleware, реальний Npgsql.
3. **Чому два способи Docker (Testcontainers vs services)?** — Testcontainers зручний для xUnit-життєвого циклу; для k6 достатньо статичного сервісу в Actions поруч з API.
4. **Що таке readiness?** — API слухає порт, але БД ще не готова; `/health/ready` це відсікає.
5. **Навіщо сід у API за змінною?** — щоб один і той самий код наповнення БД використовувався і в тестах фікстури, і перед k6 на CI, без дублювання скриптів SQL.

---

## 13. Корисні команди (локально)

Переконайтеся, що **Docker запущений**, якщо потрібні інтеграційні та database-тести.

```bash
cd /path/to/VotingSystem
dotnet restore VotingSystem.slnx
dotnet build VotingSystem.slnx -c Release
dotnet test VotingSystem.slnx -c Release --no-build
```

Окремо лише швидкі юніт-тести:

```bash
dotnet test tests/VotingSystem.Api.Tests/VotingSystem.Api.Tests.csproj -c Release
```

---

*Документ описує стан проєкту на момент створення; при зміні workflow або структури тестів варто оновити відповідні розділи.*
