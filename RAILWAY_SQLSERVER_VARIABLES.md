# 🔐 Настройка Variables для SQL Server на Railway

## Твои переменные окружения:

В Railway для сервиса `sqlserver` в разделе **"Variables"** добавь:

### Переменная 1: SA_PASSWORD
- **Name**: `SA_PASSWORD`
- **Value**: `3rXz6LHjVJu6xNS`
- **Type**: `Plain Text` или `Secret` (рекомендуется Secret для безопасности)

### Переменная 2: MSSQL_PID
- **Name**: `MSSQL_PID`
- **Value**: `Express`
- **Type**: `Plain Text`

### Переменная 3: ACCEPT_EULA
- **Name**: `ACCEPT_EULA`
- **Value**: `Y`
- **Type**: `Plain Text`

## Connection String для приложений:

После успешного деплоя SQL Server, используй этот connection string в Variables для MVC и API:

```
Server=sqlserver-production.up.railway.app,1433;Database=ElectronicLibraryv5;User Id=sa;Password=3rXz6LHjVJu6xNS;TrustServerCertificate=True;Encrypt=True;
```

⚠️ **Замени `sqlserver-production.up.railway.app` на реальный Public Domain из Railway!**

## Подключение через SSMS:

- **Server name**: `sqlserver-production.up.railway.app,1433` (замени на свой домен)
- **Authentication**: SQL Server Authentication
- **Login**: `sa`
- **Password**: `3rXz6LHjVJu6xNS`

## ⚠️ Безопасность:

- Никогда не коммить пароли в Git
- Используй тип `Secret` для `SA_PASSWORD` в Railway (скрывает значение)
- После первого подключения создай отдельного пользователя БД (не используй `sa` для приложений)

