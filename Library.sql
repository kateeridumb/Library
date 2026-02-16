IF DB_ID('ElectronicLibraryv5') IS NOT NULL
    DROP DATABASE ElectronicLibraryv5;
GO

CREATE DATABASE ElectronicLibraryv5;
GO
USE ElectronicLibraryv5;
GO

CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'MasterKeyPassword123!';
GO
select * from roles

CREATE CERTIFICATE LibraryCert
WITH SUBJECT = 'Library Encryption Certificate';
GO

CREATE SYMMETRIC KEY LibraryKey
WITH ALGORITHM = AES_256
ENCRYPTION BY CERTIFICATE LibraryCert;
GO

CREATE TABLE Roles (
    RoleID INT IDENTITY(1,1) PRIMARY KEY,
    RoleName varchar(50) NOT NULL UNIQUE
);
GO

CREATE TABLE Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    Username varchar(100) NOT NULL UNIQUE,
    PasswordHash varchar(200) NOT NULL,
    PasswordSalt varchar(200) NOT NULL,
    FirstName varchar(100) NOT NULL,
    LastName varbinary(max) NOT NULL,
    Email varchar(200) NOT NULL UNIQUE,
    RoleID INT NOT NULL CONSTRAINT DF_Users_RoleID DEFAULT 3,
    IsBlocked BIT NOT NULL CONSTRAINT DF_Users_IsBlocked DEFAULT 0,
    FacultyID INT NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleID) REFERENCES Roles(RoleID)
);
GO

CREATE TABLE Authors (
    AuthorID INT IDENTITY(1,1) PRIMARY KEY,
    FirstName varchar(100) NOT NULL,
    LastName varchar(100) NOT NULL
);
GO

CREATE TABLE Categories (
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName varchar(200) NOT NULL UNIQUE
);
GO

CREATE TABLE Publisher (
    PublisherID INT IDENTITY(1,1) PRIMARY KEY,
    PublisherName varchar(255) NOT NULL UNIQUE
);
GO

CREATE TABLE Books (
    BookID INT IDENTITY(1,1) PRIMARY KEY,
    Title varchar(300) NOT NULL,
    Description varchar(500) NULL,
    PublishYear INT NULL,
    CategoryID INT NOT NULL,
    AuthorID INT NOT NULL,
    PublisherID INT NULL,
    FilePath VARCHAR(500) NULL,
    ImagePath VARCHAR(500) NULL,
    RequiresSubscription BIT NOT NULL CONSTRAINT DF_Books_RequiresSubscription DEFAULT 0,
    CONSTRAINT FK_Books_Category FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID),
    CONSTRAINT FK_Books_Author FOREIGN KEY (AuthorID) REFERENCES Authors(AuthorID),
    CONSTRAINT FK_Books_Publisher FOREIGN KEY (PublisherID) REFERENCES Publisher(PublisherID)
);
GO

CREATE TABLE AuditLog (
    AuditLogID INT IDENTITY(1,1) PRIMARY KEY,
    TableName varchar(100) NOT NULL,
    ActionType varchar(20) NOT NULL,
    UserName varchar(200) NOT NULL,
    OldData varchar(500) NULL,
    NewData varchar(500) NULL,
    AuditDate datetime NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE AuditLogBackup (
    AuditLogID INT IDENTITY(1,1) PRIMARY KEY,
    TableName varchar(100) NOT NULL,
    ActionType varchar(20) NOT NULL,
    UserName varchar(200) NOT NULL,
    OldData varchar(500) NULL,
    NewData varchar(500) NULL,
    AuditDate datetime NOT NULL
);
GO

CREATE TABLE Faculty (
    FacultyID INT IDENTITY(1,1) PRIMARY KEY,
    FacultyName varchar(200) NOT NULL UNIQUE
);
GO

CREATE TABLE UserRole (
    UserRoleID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL,
    RoleID INT NOT NULL,
    CONSTRAINT FK_UserRole_User FOREIGN KEY (UserID) REFERENCES Users(UserID),
    CONSTRAINT FK_UserRole_Role FOREIGN KEY (RoleID) REFERENCES Roles(RoleID),
    CONSTRAINT UQ_UserRole UNIQUE (UserID, RoleID)
);
GO

CREATE TABLE AuthorBook (
    AuthorBookID INT IDENTITY(1,1) PRIMARY KEY,
    AuthorID INT NOT NULL,
    BookID INT NOT NULL,
    CONSTRAINT FK_AuthorBook_Author FOREIGN KEY (AuthorID) REFERENCES Authors(AuthorID),
    CONSTRAINT FK_AuthorBook_Book FOREIGN KEY (BookID) REFERENCES Books(BookID),
    CONSTRAINT UQ_AuthorBook UNIQUE (AuthorID, BookID)
);
GO

CREATE TABLE BookLogs (
    LogID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL,
    BookID INT NOT NULL,
    ActionAt datetime NOT NULL DEFAULT GETDATE(),
    ActionType VARCHAR(20) NOT NULL,
    CONSTRAINT FK_BookLogs_User FOREIGN KEY (UserID) REFERENCES Users(UserID),
    CONSTRAINT FK_BookLogs_Book FOREIGN KEY (BookID) REFERENCES Books(BookID),
    CONSTRAINT CK_BookLogs_ActionType CHECK (ActionType IN ('READ', 'DOWNLOAD'))
);
GO

CREATE TABLE Reviews (
    ReviewID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL,
    BookID INT NOT NULL,
    Rating INT CHECK (Rating BETWEEN 1 AND 5),
    Comment varchar(500) NULL,
    CreatedAt datetime NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Reviews_User FOREIGN KEY (UserID) REFERENCES Users(UserID),
    CONSTRAINT FK_Reviews_Book FOREIGN KEY (BookID) REFERENCES Books(BookID)
);
GO

INSERT INTO Roles (RoleName)
VALUES ('Admin'), ('Librarian'), ('Student');
GO

DECLARE @Salt varchar(200) = CONVERT(varchar(200), NEWID());
DECLARE @Password varchar(100) = 'admin123';
DECLARE @PasswordHash varchar(200) = CONVERT(varchar(200), HASHBYTES('SHA2_512', @Password + @Salt));

OPEN SYMMETRIC KEY LibraryKey DECRYPTION BY CERTIFICATE LibraryCert;

INSERT INTO Users (Username, PasswordHash, PasswordSalt, FirstName, LastName, Email, RoleID)
VALUES (
    'admin',
    @PasswordHash,
    @Salt,
    'Admin',
    EncryptByKey(Key_GUID('LibraryKey'), 'Иванов'),
    'admin@library.com',
    1
);

CLOSE SYMMETRIC KEY LibraryKey;
GO

DECLARE @TableName varchar(128),
        @SQL varchar(max),
        @PKColumn varchar(128);

DECLARE table_cursor CURSOR FOR
SELECT name
FROM sys.tables
WHERE name NOT IN ('AuditLog','AuditLogBackup');

OPEN table_cursor
FETCH NEXT FROM table_cursor INTO @TableName

WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT TOP 1 @PKColumn = c.name
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    WHERE i.object_id = OBJECT_ID(@TableName) AND i.is_primary_key = 1;

    IF @PKColumn IS NOT NULL
    BEGIN
        SET @SQL = '
        CREATE OR ALTER TRIGGER trg_' + @TableName + '_Audit
        ON ' + QUOTENAME(@TableName) + '
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
                INSERT INTO AuditLog (TableName, ActionType, UserName, NewData)
                SELECT ''' + @TableName + ''', ''INSERT'', SYSTEM_USER,
                (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
                FROM inserted i;

            IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
                INSERT INTO AuditLog (TableName, ActionType, UserName, OldData, NewData)
                SELECT ''' + @TableName + ''', ''UPDATE'', SYSTEM_USER,
                (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
                FROM inserted i
                JOIN deleted d ON i.' + QUOTENAME(@PKColumn) + ' = d.' + QUOTENAME(@PKColumn) + ';

            IF NOT EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
                INSERT INTO AuditLog (TableName, ActionType, UserName, OldData)
                SELECT ''' + @TableName + ''', ''DELETE'', SYSTEM_USER,
                (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
                FROM deleted d;
        END;
        ';
        EXEC(@SQL);
    END

    FETCH NEXT FROM table_cursor INTO @TableName
END

CLOSE table_cursor
DEALLOCATE table_cursor;
GO

USE ElectronicLibraryv5;
GO

-- ===== Авторы =====
INSERT INTO Authors (FirstName, LastName)
VALUES
('Фёдор', 'Достоевский'),
('Лев', 'Толстой'),
('Михаил', 'Булгаков'),
('Александр', 'Пушкин'),
('Рэй', 'Брэдбери');
GO

-- ===== Категории =====
INSERT INTO Categories (CategoryName)
VALUES
('Классическая литература'),
('Роман'),
('Фантастика'),
('Философия');
GO

-- ===== Издательства =====
INSERT INTO Publisher (PublisherName)
VALUES
('Эксмо'),
('АСТ'),
('Азбука'),
('МИФ');
GO

-- ===== Книги =====
INSERT INTO Books
(Title, Description, PublishYear, CategoryID, AuthorID, PublisherID)
VALUES
(
    'Преступление и наказание',
    'Психологический роман о вине и искуплении',
    1866,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Философия'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Достоевский'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'Эксмо')
),
(
    'Война и мир',
    'Роман-эпопея о судьбах людей на фоне войны',
    1869,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Роман'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Толстой'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'АСТ')
),
(
    'Мастер и Маргарита',
    'Мистический роман о добре и зле',
    1967,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Классическая литература'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Булгаков'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'Азбука')
),
(
    'Евгений Онегин',
    'Роман в стихах',
    1833,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Классическая литература'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Пушкин'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'МИФ')
),
(
    '451 градус по Фаренгейту',
    'Антиутопия о запрете книг',
    1953,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Фантастика'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Брэдбери'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'АСТ')
);
GO

UPDATE Books
SET ImagePath = 'https://i.postimg.cc/L8n0hYsV/prestup.jpg'
WHERE BookID = 1;

UPDATE Books
SET ImagePath = 'https://i.postimg.cc/3xdbW4wp/vim.webp'
WHERE BookID = 2;

UPDATE Books
SET ImagePath = 'https://i.postimg.cc/pX062YPk/mim.webp'
WHERE BookID = 3;

UPDATE Books
SET ImagePath = 'https://i.postimg.cc/qMm5kXpL/evgen.webp'
WHERE BookID = 4;

UPDATE Books
SET ImagePath = 'https://i.postimg.cc/65Hm9LBh/451.webp'
WHERE BookID = 5;

INSERT INTO Categories (CategoryName)
VALUES ('Программирование');
GO

INSERT INTO Authors (FirstName, LastName)
VALUES
('Роберт', 'Мартин'),
('Эндрю', 'Хант'),
('Дэвид', 'Томас'),
('Мартин', 'Фаулер'),
('Герберт', 'Шилдт');
GO

INSERT INTO Books
(
    Title,
    Description,
    PublishYear,
    CategoryID,
    AuthorID,
    PublisherID,
    FilePath,
    ImagePath
)
VALUES
(
    'Чистый код',
    'Практики написания читаемого и поддерживаемого кода',
    2008,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Программирование'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Мартин'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'МИФ'),
    '/books/Robert_Sesil_Martin_Chistyiy_kod._Sozdanie_analiz_i_refaktoring.pdf',
    'https://i.postimg.cc/9MCbx7mx/clean-code.jpg'
),
(
    'Программист-прагматик',
    'Классическая книга о мышлении разработчика',
    1999,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Программирование'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Хант'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'АСТ'),
    '/books/Программист-прагматик, 2-е юбилейное издание.pdf',
    'https://i.postimg.cc/8zwL10Nh/pragmatic.webp'
),
(
    'Refactoring',
    'Улучшение существующего кода без изменения поведения',
    1999,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Программирование'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Фаулер'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'Азбука'),
    '/books/Fauler_Martin_Refaktoring.pdf',
    'https://i.postimg.cc/261Q97pB/refactoring.webp'
),
(
    'Java. Полное руководство',
    'Подробное руководство по языку Java',
    2018,
    (SELECT CategoryID FROM Categories WHERE CategoryName = 'Программирование'),
    (SELECT AuthorID FROM Authors WHERE LastName = 'Шилдт'),
    (SELECT PublisherID FROM Publisher WHERE PublisherName = 'Эксмо'),
    '/books/codelibs.ru_java-polnoe-rukovodstvo-12-e-izd.pdf',
    'https://i.postimg.cc/c4n3nJZd/java.jpg'
);
GO

CREATE TABLE Subscriptions (
    SubscriptionID INT IDENTITY(1,1) PRIMARY KEY,
    FacultyID INT NOT NULL,
    Name varchar(100) NOT NULL,
    StartDate datetime NOT NULL,
    EndDate datetime NOT NULL,
    CONSTRAINT FK_Subscriptions_Faculty FOREIGN KEY (FacultyID) REFERENCES Faculty(FacultyID)
);
GO

INSERT INTO Faculty (FacultyName)
VALUES 
    ('Информационные технологии'),
    ('Экономика и управление'),
    ('Гуманитарные науки'),
    ('Технические специальности')
GO

select * from Books

UPDATE Books
SET FilePath = '/books/Dostoevskyi_Prestuplenie_i_nakazanie.pdf'
WHERE BookID = 1;

UPDATE Books
SET FilePath = '/books/Tolstoy_Voina_i_mir.pdf'
WHERE BookID = 2;

UPDATE Books
SET FilePath = '/books/Bulgakov_Master_i_Margarita.pdf'
WHERE BookID = 3;

UPDATE Books
SET FilePath = '/books/Pushkin_Evgenyi_Onegin.pdf'
WHERE BookID = 4;

UPDATE Books
SET FilePath = '/books/451_градус_по_Фаренгейту__ata.pdf'
WHERE BookID = 5;

INSERT INTO Books
(Title, Description, PublishYear, CategoryID, AuthorID, PublisherID, FilePath, ImagePath, RequiresSubscription)
VALUES
(
    'Эксклюзивный курс по машинному обучению',
    'Углубленный курс по современным технологиям искусственного интеллекта и машинного обучения. Включает практические примеры и кейсы.',
    2024,
    (SELECT TOP 1 CategoryID FROM Categories WHERE CategoryName = 'Философия'),
    (SELECT TOP 1 AuthorID FROM Authors WHERE LastName = 'Достоевский'),
    (SELECT TOP 1 PublisherID FROM Publisher WHERE PublisherName = 'МИФ'),
    '/books/Mashinnoe-obuchenie-bez-lishnikh-slov.pdf',
    'https://i.postimg.cc/GhGFMCgy/aaaaaaaaaaaaaaaaaaaa.webp',
    1 
),
(
    'Премиум коллекция научных статей',
    'Собрание лучших научных статей по программированию, архитектуре ПО и современным технологиям разработки.',
    2024,
    (SELECT TOP 1 CategoryID FROM Categories WHERE CategoryName = 'Классическая литература'),
    (SELECT TOP 1 AuthorID FROM Authors WHERE LastName = 'Толстой'),
    (SELECT TOP 1 PublisherID FROM Publisher WHERE PublisherName = 'АСТ'),
    '/books/sbornik_stud.pdf',
    'https://i.postimg.cc/ZKp8fSs6/aaao.jpg',
    1 
);
GO

-- это обновление системы подписок

USE ElectronicLibraryv5;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'DurationDays')
BEGIN
    ALTER TABLE Subscriptions
    ADD DurationDays INT NULL;
    
    PRINT 'Поле DurationDays добавлено в таблицу Subscriptions';
END
ELSE
BEGIN
    PRINT 'Поле DurationDays уже существует';
END
GO

UPDATE Subscriptions
SET DurationDays = DATEDIFF(DAY, StartDate, EndDate)
WHERE DurationDays IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'FacultyID' AND is_nullable = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Subscriptions_Faculty')
    BEGIN
        ALTER TABLE Subscriptions
        DROP CONSTRAINT FK_Subscriptions_Faculty;
    END
    
    ALTER TABLE Subscriptions
    ALTER COLUMN FacultyID INT NULL;
    
    ALTER TABLE Subscriptions
    ADD CONSTRAINT FK_Subscriptions_Faculty 
    FOREIGN KEY (FacultyID) REFERENCES Faculty(FacultyID);
    
    PRINT 'Поле FacultyID теперь может быть NULL (для шаблонов подписок)';
END
ELSE
BEGIN
    PRINT 'Поле FacultyID уже может быть NULL';
END
GO

PRINT 'Миграция завершена успешно';

-- вот это прикол типа я поняла что для подписок надо чтоб стартдейт и энддейт были нуллейбл

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'FacultyID' AND is_nullable = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Subscriptions_Faculty')
    BEGIN
        ALTER TABLE Subscriptions
        DROP CONSTRAINT FK_Subscriptions_Faculty;
    END
    
    ALTER TABLE Subscriptions
    ALTER COLUMN FacultyID INT NULL;
    
    ALTER TABLE Subscriptions
    ADD CONSTRAINT FK_Subscriptions_Faculty 
    FOREIGN KEY (FacultyID) REFERENCES Faculty(FacultyID);
    
    PRINT 'Поле FacultyID теперь может быть NULL (для шаблонов подписок)';
END
ELSE
BEGIN
    PRINT 'Поле FacultyID уже может быть NULL';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'StartDate' AND is_nullable = 0)
BEGIN
    ALTER TABLE Subscriptions
    ALTER COLUMN StartDate datetime NULL;
    
    PRINT 'Поле StartDate теперь может быть NULL (для шаблонов подписок)';
END
ELSE
BEGIN
    PRINT 'Поле StartDate уже может быть NULL';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'EndDate' AND is_nullable = 0)
BEGIN
    ALTER TABLE Subscriptions
    ALTER COLUMN EndDate datetime NULL;
    
    PRINT 'Поле EndDate теперь может быть NULL (для шаблонов подписок)';
END
ELSE
BEGIN
    PRINT 'Поле EndDate уже может быть NULL';
END
GO

PRINT 'Миграция завершена успешно';

-- это для одобрения подписок библиотекарем 
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Subscriptions]') AND name = 'Status')
BEGIN
    ALTER TABLE Subscriptions
    ADD Status NVARCHAR(50) NULL;
    
    PRINT 'Поле Status добавлено в таблицу Subscriptions';
END
ELSE
BEGIN
    PRINT 'Поле Status уже существует в таблице Subscriptions';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Subscriptions]') AND name = 'RequestedByUserID')
BEGIN
    ALTER TABLE Subscriptions
    ADD RequestedByUserID INT NULL;
    
    ALTER TABLE Subscriptions
    ADD CONSTRAINT FK_Subscriptions_RequestedByUser
    FOREIGN KEY (RequestedByUserID) REFERENCES Users(UserID);
    
    PRINT 'Поле RequestedByUserID добавлено в таблицу Subscriptions';
END
ELSE
BEGIN
    PRINT 'Поле RequestedByUserID уже существует в таблице Subscriptions';
END
GO

UPDATE Subscriptions
SET Status = 'Approved'
WHERE FacultyID IS NOT NULL
  AND StartDate IS NOT NULL
  AND EndDate IS NOT NULL
  AND Status IS NULL;

PRINT 'Миграция завершена успешно';
GO


-- восстановление пароля

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordResetToken')
BEGIN
    ALTER TABLE Users
    ADD PasswordResetToken NVARCHAR(255) NULL;
    
    PRINT 'Поле PasswordResetToken добавлено в таблицу Users';
END
ELSE
BEGIN
    PRINT 'Поле PasswordResetToken уже существует в таблице Users';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordResetTokenExpiry')
BEGIN
    ALTER TABLE Users
    ADD PasswordResetTokenExpiry DATETIME NULL;
    
    PRINT 'Поле PasswordResetTokenExpiry добавлено в таблицу Users';
END
ELSE
BEGIN
    PRINT 'Поле PasswordResetTokenExpiry уже существует в таблице Users';
END
GO

PRINT 'Миграция завершена успешно';
GO

-- Миграция: Увеличение размера столбцов OldData и NewData в таблице AuditLog
-- Проблема: varchar(500) слишком мал для хранения JSON всех полей таблицы Users

-- Увеличиваем размер столбца OldData до NVARCHAR(MAX)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND name = 'OldData')
BEGIN
    ALTER TABLE AuditLog
    ALTER COLUMN OldData NVARCHAR(MAX) NULL;
    
    PRINT 'Столбец OldData увеличен до NVARCHAR(MAX)';
END
ELSE
BEGIN
    PRINT 'Столбец OldData не найден';
END
GO

-- Увеличиваем размер столбца NewData до NVARCHAR(MAX)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND name = 'NewData')
BEGIN
    ALTER TABLE AuditLog
    ALTER COLUMN NewData NVARCHAR(MAX) NULL;
    
    PRINT 'Столбец NewData увеличен до NVARCHAR(MAX)';
END
ELSE
BEGIN
    PRINT 'Столбец NewData не найден';
END
GO

PRINT 'Миграция завершена успешно';
GO


IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND name = 'OldData')
BEGIN
    ALTER TABLE AuditLog
    ALTER COLUMN OldData NVARCHAR(MAX) NULL;
    
    PRINT 'Столбец OldData увеличен до NVARCHAR(MAX)';
END
ELSE
BEGIN
    PRINT 'Столбец OldData не найден';
END
GO

-- размер столбца NewData NVARCHAR(MAX)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND name = 'NewData')
BEGIN
    ALTER TABLE AuditLog
    ALTER COLUMN NewData NVARCHAR(MAX) NULL;
    
    PRINT 'Столбец NewData увеличен до NVARCHAR(MAX)';
END
ELSE
BEGIN
    PRINT 'Столбец NewData не найден';
END
GO

PRINT 'Миграция завершена успешно';
GO


IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'trg_Users_Audit')
BEGIN
    DROP TRIGGER trg_Users_Audit;
    PRINT 'Старый триггер trg_Users_Audit удален';
END
GO

-- Создаем новый триггер, который исключает чувствительные поля
CREATE TRIGGER trg_Users_Audit
ON Users
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
	
    IF EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
        INSERT INTO AuditLog (TableName, ActionType, UserName, NewData)
        SELECT 'Users', 'INSERT', SYSTEM_USER,
        (SELECT 
            i.UserID,
            i.Username,
            i.FirstName,
            i.Email,
            i.RoleID,
            i.FacultyID,
            i.IsBlocked,
            i.PasswordResetTokenExpiry
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
        FROM inserted i;

    IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
        INSERT INTO AuditLog (TableName, ActionType, UserName, OldData, NewData)
        SELECT 'Users', 'UPDATE', SYSTEM_USER,
        (SELECT 
            d.UserID,
            d.Username,
            d.FirstName,
            d.Email,
            d.RoleID,
            d.FacultyID,
            d.IsBlocked,
            d.PasswordResetTokenExpiry
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT 
            i.UserID,
            i.Username,
            i.FirstName,
            i.Email,
            i.RoleID,
            i.FacultyID,
            i.IsBlocked,
            i.PasswordResetTokenExpiry
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
        FROM inserted i
        JOIN deleted d ON i.UserID = d.UserID;

    IF NOT EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
        INSERT INTO AuditLog (TableName, ActionType, UserName, OldData)
        SELECT 'Users', 'DELETE', SYSTEM_USER,
        (SELECT 
            d.UserID,
            d.Username,
            d.FirstName,
            d.Email,
            d.RoleID,
            d.FacultyID,
            d.IsBlocked
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
        FROM deleted d;
END
GO

PRINT 'Триггер trg_Users_Audit обновлен успешно';
GO

-- добавила закладки
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Bookmarks]') AND type in (N'U'))
BEGIN
    CREATE TABLE Bookmarks (
        BookmarkID INT IDENTITY(1,1) PRIMARY KEY,
        UserID INT NOT NULL,
        BookID INT NOT NULL,
        Page VARCHAR(50) NULL,
        Position VARCHAR(100) NULL,
        Title VARCHAR(200) NULL,
        Note VARCHAR(500) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Bookmarks_User FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
        CONSTRAINT FK_Bookmarks_Book FOREIGN KEY (BookID) REFERENCES Books(BookID) ON DELETE CASCADE
    );
    
    CREATE INDEX IX_Bookmarks_UserID ON Bookmarks(UserID);
    CREATE INDEX IX_Bookmarks_BookID ON Bookmarks(BookID);
    
   
END
ELSE
BEGIN
    PRINT 'Таблица уже существует';
END
GO

IF COL_LENGTH('Bookmarks', 'Title') IS NULL
BEGIN
    ALTER TABLE Bookmarks ADD Title VARCHAR(200) NULL;
END
GO

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
