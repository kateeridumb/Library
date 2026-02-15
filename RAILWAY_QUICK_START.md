# 🚀 Быстрый старт: Railway + SSMS

## 1. Создай проект на Railway

1. Зайди на https://railway.app
2. Войди через GitHub
3. **New Project** → **Deploy from GitHub repo** (выбери свой репозиторий)

## 2. Добавь SQL Server

### Способ 1: Через Empty Service (рекомендуется)

1. В проекте нажми **"+ New"** → **"Empty Service"**
2. Назови его `sqlserver`
3. В настройках сервиса:
   - **Source**: GitHub (выбери репозиторий)
   - **Dockerfile Path**: `Dockerfile.sqlserver`
4. Добавь переменные окружения:
   - `SA_PASSWORD` = `YourStrong@Password123!` (придумай свой)
   - `MSSQL_PID` = `Express`

### Способ 2: Через Railway Template

1. **"+ New"** → **"Database"** → **"Add PostgreSQL"**
2. В настройках замени образ на SQL Server (но проще через Empty Service)

## 3. Получи Connection String

После деплоя SQL Server:

1. Открой сервис `sqlserver`
2. Перейди в **"Settings"** → **"Networking"**
3. Нажми **"Generate Domain"** (получишь что-то вроде `sqlserver-production.up.railway.app`)
4. Запиши:
   - **Server**: `sqlserver-production.up.railway.app,1433`
   - **Username**: `sa`
   - **Password**: значение из `SA_PASSWORD`

## 4. Подключись через SSMS

1. Открой **SQL Server Management Studio**
2. **Server name**: `sqlserver-production.up.railway.app,1433`
3. **Authentication**: SQL Server Authentication
   - **Login**: `sa`
   - **Password**: твой `SA_PASSWORD`
4. Нажми **Connect** ✅

## 5. Создай базу данных

В SSMS выполни:

```sql
CREATE DATABASE ElectronicLibraryv5;
GO
USE ElectronicLibraryv5;
GO
```

Затем выполни весь скрипт из `Library.sql` (или импортируй через SSMS).

## 6. Настрой MVC и API сервисы

### Для LibraryMPT.Api:

1. **"+ New"** → **"GitHub Repo"** → выбери репозиторий
2. **Root Directory**: `LibraryMPT.Api`
3. **Variables** (добавь):
   ```
   ConnectionStrings__LibraryDb = Server=sqlserver-production.up.railway.app,1433;Database=ElectronicLibraryv5;User Id=sa;Password=YourStrong@Password123!;TrustServerCertificate=True;Encrypt=True;
   JwtSettings__Issuer = LibraryMPT
   JwtSettings__Audience = LibraryMPT.Api
   JwtSettings__Key = LibraryMPT-Super-Secret-Jwt-Key-Replace-In-Production-2026
   ```

### Для LibraryMPT (MVC):

1. **"+ New"** → **"GitHub Repo"** → выбери репозиторий
2. **Root Directory**: `/` (корень)
3. **Variables** (добавь):
   ```
   ConnectionStrings__LibraryDb = Server=sqlserver-production.up.railway.app,1433;Database=ElectronicLibraryv5;User Id=sa;Password=YourStrong@Password123!;TrustServerCertificate=True;Encrypt=True;
   ApiSettings__BaseUrl = https://librarympt-api.up.railway.app
   JwtSettings__Issuer = LibraryMPT
   JwtSettings__Audience = LibraryMPT.Api
   JwtSettings__Key = LibraryMPT-Super-Secret-Jwt-Key-Replace-In-Production-2026
   ```

⚠️ **Важно**: Замени `librarympt-api.up.railway.app` на реальный домен API сервиса!

## 7. Получи домены

В каждом сервисе (MVC и API):
- **Settings** → **Networking** → **Generate Domain**
- Скопируй домен и обнови `ApiSettings__BaseUrl` в MVC
- Обнови `Cors__AllowedOrigins` в API

## 8. Готово! 🎉

Теперь:
- ✅ SQL Server работает на Railway
- ✅ Подключаешься через SSMS
- ✅ Приложения задеплоены
- ✅ Всё работает!

## 🔧 Troubleshooting

**SSMS не подключается:**
- Проверь, что SQL Server сервис запущен (зелёный статус)
- Убедись, что используешь правильный Public Domain
- Попробуй добавить `;TrustServerCertificate=True;Encrypt=True;` в connection string

**Приложение не видит БД:**
- Проверь, что БД создана в SSMS
- Проверь connection string в Variables
- Посмотри логи в Railway (View Logs)

