-- SQL скрипт для добавления полей двухфакторной аутентификации в таблицу Users
USE ElectronicLibraryv5;
GO

-- Добавляем поля для двухфакторной аутентификации
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsTwoFactorEnabled')
BEGIN
    ALTER TABLE Users
    ADD IsTwoFactorEnabled BIT NOT NULL DEFAULT 0;
    PRINT 'Поле IsTwoFactorEnabled добавлено';
END
ELSE
BEGIN
    PRINT 'Поле IsTwoFactorEnabled уже существует';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'TwoFactorCode')
BEGIN
    ALTER TABLE Users
    ADD TwoFactorCode VARCHAR(10) NULL;
    PRINT 'Поле TwoFactorCode добавлено';
END
ELSE
BEGIN
    PRINT 'Поле TwoFactorCode уже существует';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'TwoFactorCodeExpiry')
BEGIN
    ALTER TABLE Users
    ADD TwoFactorCodeExpiry DATETIME NULL;
    PRINT 'Поле TwoFactorCodeExpiry добавлено';
END
ELSE
BEGIN
    PRINT 'Поле TwoFactorCodeExpiry уже существует';
END
GO

PRINT 'Миграция завершена успешно!';
GO

