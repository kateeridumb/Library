# 🔧 Исправление ошибки "dotnet could not be found" для SQL Server

## Проблема
```
Deployment failed during deploy process
Deploy > Create container: The executable `dotnet` could not be found.
```

## Причина
Railway пытается запустить SQL Server контейнер как .NET приложение и ищет `dotnet`, которого нет в образе SQL Server.

## Решение

### 1. Проверь Dockerfile.sqlserver

Убедись, что файл содержит только SQL Server команды, без упоминания .NET:

```dockerfile
FROM mcr.microsoft.com/mssql/server:2022-latest

ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=${SA_PASSWORD}
ENV MSSQL_PID=Express

EXPOSE 1433

CMD ["/opt/mssql/bin/sqlservr"]
```

### 2. Проверь настройки Deploy в Railway

1. Открой сервис `sqlserver` в Railway
2. Перейди в **"Deploy"** (5-й пункт меню)
3. **Start Command**: должен быть **ПУСТЫМ** (не `dotnet ...`)
4. Railway автоматически запустит SQL Server через `CMD` из Dockerfile

### 3. Проверь, что используется правильный Dockerfile

1. В **"Build"** убедись, что:
   - **Dockerfile Path**: `Dockerfile.sqlserver`
   - Или переименуй `Dockerfile.sqlserver` в `Dockerfile`

### 4. Убедись, что нет railway.json для SQL Server

Файл `railway.json` может указывать Railway использовать .NET команды. Для SQL Server он не нужен.

Если `railway.json` есть в корне и содержит:
```json
{
  "deploy": {
    "startCommand": "dotnet ..."
  }
}
```

То либо:
- Удали `railway.json` для SQL Server сервиса
- Или создай отдельный `railway.json` только для MVC/API сервисов

### 5. Перезапусти деплой

1. В Railway нажми **"Redeploy"** или **"Deploy"**
2. Проверь логи — должно быть:
   ```
   => [build] STEP 1/5: FROM mcr.microsoft.com/mssql/server:2022-latest
   => [build] STEP 2/5: ENV ACCEPT_EULA=Y
   ...
   => [runtime] CMD ["/opt/mssql/bin/sqlservr"]
   ```

## Проверка успешного деплоя

После успешного деплоя в логах должно быть:
```
SQL Server is now ready for client connections
```

А не ошибки про `dotnet`.

## Если всё ещё не работает

1. Убедись, что в **Variables** установлены:
   - `SA_PASSWORD` = твой пароль
   - `MSSQL_PID` = `Express`
   - `ACCEPT_EULA` = `Y`

2. Проверь, что `Dockerfile.sqlserver` запушен в GitHub

3. Попробуй переименовать `Dockerfile.sqlserver` → `Dockerfile` (Railway автоматически его подхватит)

4. Убедись, что в настройках Build выбран **Docker**, а не **Railpack**

