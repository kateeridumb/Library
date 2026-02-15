# Инструкция по деплою на Railway

## Шаг 1: Подготовка проекта на Railway

1. Зарегистрируйся на https://railway.app (через GitHub)
2. Создай новый проект (New Project)
3. Добавь 3 сервиса:
   - **SQL Server** (через Template → PostgreSQL → замени на SQL Server)
   - **LibraryMPT.Api** (через GitHub Deploy)
   - **LibraryMPT** (через GitHub Deploy)

## Шаг 2: Настройка SQL Server на Railway

### Вариант A: Через Railway Template (рекомендуется)

1. В проекте Railway нажми **"New"** → **"Database"** → **"Add PostgreSQL"** (мы заменим на SQL Server)
2. Или создай **"Empty Service"** и добавь Dockerfile

### Вариант B: Через Dockerfile (более гибко)

1. Создай **"Empty Service"** в Railway
2. Добавь файл `Dockerfile.sqlserver` в корень проекта:

```dockerfile
FROM mcr.microsoft.com/mssql/server:2022-latest

ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=${SA_PASSWORD}
ENV MSSQL_PID=Express

EXPOSE 1433
```

3. В настройках сервиса:
   - **Source**: GitHub (выбери репозиторий)
   - **Root Directory**: `/` (или создай отдельную папку для SQL Server)
   - **Dockerfile Path**: `Dockerfile.sqlserver`
   - **Start Command**: оставь пустым (SQL Server запустится автоматически)

### Настройка переменных окружения для SQL Server:

В настройках SQL Server сервиса добавь:
- `SA_PASSWORD` = `YourStrong@Password123` (или сгенерируй свой)
- `MSSQL_PID` = `Express`

## Шаг 3: Получение Connection String

После деплоя SQL Server:

1. Открой сервис SQL Server в Railway
2. Перейди в **"Variables"** или **"Connect"**
3. Railway покажет:
   - **Public Domain** (например: `sqlserver-production.up.railway.app`)
   - **Port** (обычно `1433`)
   - **Username** = `sa`
   - **Password** = значение из `SA_PASSWORD`

4. Connection String будет:
```
Server=sqlserver-production.up.railway.app,1433;Database=ElectronicLibraryv5;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;Encrypt=True;
```

## Шаг 4: Подключение через SSMS

1. Открой **SQL Server Management Studio (SSMS)**
2. В **"Server name"** введи:
   ```
   sqlserver-production.up.railway.app,1433
   ```
   (замени на свой Public Domain из Railway)

3. **Authentication**: SQL Server Authentication
   - **Login**: `sa`
   - **Password**: значение из переменной `SA_PASSWORD` в Railway

4. Нажми **Connect**

5. Если не подключается:
   - Проверь, что SQL Server сервис запущен в Railway
   - Убедись, что порт `1433` открыт (Railway должен показать его в "Networking")
   - Попробуй добавить `;TrustServerCertificate=True;Encrypt=True;` в connection string

## Шаг 5: Настройка приложений (MVC и API)

### В Railway для каждого сервиса (MVC и API) добавь переменные окружения:

1. Открой сервис **LibraryMPT.Api**
2. Перейди в **"Variables"**
3. Добавь:
   ```
   ConnectionStrings__LibraryDb = Server=sqlserver-production.up.railway.app,1433;Database=ElectronicLibraryv5;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;Encrypt=True;
   ```
   (замени на свой connection string)

4. Добавь JWT настройки:
   ```
   JwtSettings__Issuer = LibraryMPT
   JwtSettings__Audience = LibraryMPT.Api
   JwtSettings__Key = LibraryMPT-Super-Secret-Jwt-Key-Replace-In-Production-2026
   ```

5. Добавь CORS (замени на свой домен):
   ```
   Cors__AllowedOrigins__0 = https://your-mvc-app.up.railway.app
   ```

6. Для **LibraryMPT** (MVC) добавь:
   ```
   ApiSettings__BaseUrl = https://your-api-app.up.railway.app
   ConnectionStrings__LibraryDb = (тот же connection string)
   JwtSettings__Issuer = LibraryMPT
   JwtSettings__Audience = LibraryMPT.Api
   JwtSettings__Key = (тот же ключ)
   ```

## Шаг 6: Создание базы данных

После подключения через SSMS:

1. Выполни скрипт создания БД (из `Library.sql`)
2. Или создай БД вручную:
   ```sql
   CREATE DATABASE ElectronicLibraryv5;
   GO
   USE ElectronicLibraryv5;
   GO
   -- Затем выполни все CREATE TABLE из Library.sql
   ```

## Шаг 7: Деплой кода

1. Запушь код в GitHub
2. В Railway для каждого сервиса (MVC и API):
   - Выбери репозиторий
   - Выбери ветку (обычно `main` или `master`)
   - Railway автоматически задеплоит при каждом push

## Шаг 8: Настройка доменов

1. В каждом сервисе (MVC, API, SQL Server) перейди в **"Settings"** → **"Networking"**
2. Нажми **"Generate Domain"** для получения бесплатного домена
3. Или подключи свой домен через **"Custom Domain"**

## Важные замечания

⚠️ **Безопасность:**
- Никогда не коммить пароли в Git
- Используй переменные окружения Railway для всех секретов
- Сгенерируй сильный `SA_PASSWORD` (минимум 8 символов, буквы, цифры, спецсимволы)

⚠️ **Бесплатный tier Railway:**
- $5 кредитов в месяц
- SQL Server Express может быть ограничен по памяти
- После 30 дней без активности проект может заснуть

⚠️ **Порты:**
- Railway автоматически проксирует порты
- Используй Public Domain, а не IP
- Порт `1433` должен быть открыт автоматически

## Troubleshooting

**Не могу подключиться через SSMS:**
- Проверь, что SQL Server сервис запущен (зелёный индикатор в Railway)
- Проверь правильность Public Domain и порта
- Попробуй подключиться через Azure Data Studio (альтернатива SSMS)

**Ошибка "Login failed":**
- Проверь правильность пароля `SA_PASSWORD`
- Убедись, что используешь `sa` как username

**База данных не создаётся:**
- Убедись, что подключился к правильному серверу
- Проверь права доступа (должен быть `sa`)

**Приложение не подключается к БД:**
- Проверь connection string в переменных окружения Railway
- Убедись, что БД создана
- Проверь логи в Railway (View Logs)

