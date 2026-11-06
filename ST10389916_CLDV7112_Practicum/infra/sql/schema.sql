IF OBJECT_ID('dbo.ProcessedEvents', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.ProcessedEvents (
		Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
		EventType NVARCHAR(100) NOT NULL,
		Amount DECIMAL(18, 2) NOT NULL,
		OccurredAt DATETIMEOFFSET NOT NULL,
		Data NVARCHAR(MAX) NOT NULL,
		ProcessedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_ProcessedEvents_ProcessedAt DEFAULT SYSUTCDATETIME(),
		CONSTRAINT CK_ProcessedEvents_Data_IsJson CHECK (ISJSON(Data) = 1)
	);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProcessedEvents_EventType_OccurredAt' AND object_id = OBJECT_ID('dbo.ProcessedEvents'))
BEGIN
	CREATE INDEX IX_ProcessedEvents_EventType_OccurredAt ON dbo.ProcessedEvents(EventType, OccurredAt);
END;

IF OBJECT_ID('dbo.UserAccounts', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.UserAccounts (
		Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
		Username NVARCHAR(100) NOT NULL,
		[Password] NVARCHAR(255) NOT NULL,
		[Role] NVARCHAR(30) NOT NULL,
		CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_UserAccounts_CreatedAt DEFAULT SYSUTCDATETIME(),
		CONSTRAINT UQ_UserAccounts_Username UNIQUE (Username),
		CONSTRAINT CK_UserAccounts_Role CHECK ([Role] IN ('Farmer', 'Agronomist', 'Admin'))
	);
END;

IF OBJECT_ID('dbo.DeviceRegistry', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.DeviceRegistry (
		DeviceId NVARCHAR(100) NOT NULL PRIMARY KEY,
		FieldId NVARCHAR(100) NOT NULL,
		OperationalStatus NVARCHAR(50) NOT NULL,
		LastCommand NVARCHAR(200) NULL,
		UpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_DeviceRegistry_UpdatedAt DEFAULT SYSUTCDATETIME()
	);
END;
