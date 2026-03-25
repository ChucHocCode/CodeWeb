/*
  SQL Server init script for airplane ticket system
  Converted from SQLite-style migration
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.[Transaction]', N'U') IS NOT NULL DROP TABLE dbo.[Transaction];
IF OBJECT_ID(N'dbo.[Payment]', N'U') IS NOT NULL DROP TABLE dbo.[Payment];
IF OBJECT_ID(N'dbo.[Booking]', N'U') IS NOT NULL DROP TABLE dbo.[Booking];
IF OBJECT_ID(N'dbo.[Seat]', N'U') IS NOT NULL DROP TABLE dbo.[Seat];
IF OBJECT_ID(N'dbo.[Ticket]', N'U') IS NOT NULL DROP TABLE dbo.[Ticket];
IF OBJECT_ID(N'dbo.[Flight]', N'U') IS NOT NULL DROP TABLE dbo.[Flight];
IF OBJECT_ID(N'dbo.[User]', N'U') IS NOT NULL DROP TABLE dbo.[User];
GO

CREATE TABLE dbo.[User] (
    [id] NVARCHAR(100) NOT NULL,
    [name] NVARCHAR(255) NOT NULL,
    [email] NVARCHAR(255) NOT NULL,
    [phone] NVARCHAR(30) NULL,
    [password] NVARCHAR(255) NOT NULL,
    [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_User_createdAt] DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY ([id])
);
GO

CREATE TABLE dbo.[Flight] (
    [id] NVARCHAR(100) NOT NULL,
    [flightNumber] NVARCHAR(50) NOT NULL,
    [airline] NVARCHAR(100) NOT NULL,
    [departureCity] NVARCHAR(100) NOT NULL,
    [arrivalCity] NVARCHAR(100) NOT NULL,
    [departureTime] DATETIME2 NOT NULL,
    [arrivalTime] DATETIME2 NOT NULL,
    [duration] INT NOT NULL,
    [basePrice] INT NOT NULL,
    [availableSeats] INT NOT NULL CONSTRAINT [DF_Flight_availableSeats] DEFAULT 180,
    [totalSeats] INT NOT NULL CONSTRAINT [DF_Flight_totalSeats] DEFAULT 180,
    [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_Flight_createdAt] DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_Flight] PRIMARY KEY ([id])
);
GO

CREATE TABLE dbo.[Seat] (
    [id] NVARCHAR(100) NOT NULL,
    [flightId] NVARCHAR(100) NOT NULL,
    [seatNumber] NVARCHAR(20) NOT NULL,
    [status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Seat_status] DEFAULT N'AVAILABLE',
    [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_Seat_createdAt] DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_Seat] PRIMARY KEY ([id]),
    CONSTRAINT [FK_Seat_Flight_flightId]
      FOREIGN KEY ([flightId]) REFERENCES dbo.[Flight]([id])
      ON DELETE CASCADE ON UPDATE NO ACTION
);
GO

CREATE TABLE dbo.[Booking] (
    [id] NVARCHAR(100) NOT NULL,
    [flightId] NVARCHAR(100) NOT NULL,
    [userId] NVARCHAR(100) NOT NULL,
    [passengerName] NVARCHAR(255) NOT NULL,
    [passengerEmail] NVARCHAR(255) NOT NULL,
    [passengerPhone] NVARCHAR(30) NOT NULL,
    [passengerDOB] DATETIME2 NULL,
    [passengerPassport] NVARCHAR(50) NULL,
    [seatNumber] NVARCHAR(20) NOT NULL,
    [price] INT NOT NULL,
    [status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Booking_status] DEFAULT N'PENDING',
    [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_Booking_createdAt] DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_Booking] PRIMARY KEY ([id]),
    CONSTRAINT [FK_Booking_Flight_flightId]
      FOREIGN KEY ([flightId]) REFERENCES dbo.[Flight]([id])
      ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT [FK_Booking_User_userId]
      FOREIGN KEY ([userId]) REFERENCES dbo.[User]([id])
      ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

CREATE TABLE dbo.[Payment] (
    [id] NVARCHAR(100) NOT NULL,
    [bookingId] NVARCHAR(100) NOT NULL,
    [userId] NVARCHAR(100) NOT NULL,
    [amount] INT NOT NULL,
    [method] NVARCHAR(50) NOT NULL,
    [status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Payment_status] DEFAULT N'PENDING',
    [transactionId] NVARCHAR(100) NULL,
    [description] NVARCHAR(1000) NULL,
    [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_Payment_createdAt] DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_Payment] PRIMARY KEY ([id]),
    CONSTRAINT [FK_Payment_Booking_bookingId]
      FOREIGN KEY ([bookingId]) REFERENCES dbo.[Booking]([id])
      ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT [FK_Payment_User_userId]
      FOREIGN KEY ([userId]) REFERENCES dbo.[User]([id])
      ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

CREATE TABLE dbo.[Ticket] (
    [code] NVARCHAR(50) NOT NULL,
    [passenger] NVARCHAR(255) NOT NULL,
    [price] INT NOT NULL,
    [flightDate] DATETIME2 NOT NULL,
    [flightTime] NVARCHAR(20) NOT NULL,
    [departureCity] NVARCHAR(100) NOT NULL,
    [arrivalCity] NVARCHAR(100) NOT NULL,
    [status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Ticket_status] DEFAULT N'ACTIVE',
    [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_Ticket_createdAt] DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_Ticket] PRIMARY KEY ([code])
);
GO

CREATE TABLE dbo.[Transaction] (
    [id] INT IDENTITY(1,1) NOT NULL,
    [ticketCode] NVARCHAR(50) NOT NULL,
    [type] NVARCHAR(30) NOT NULL,
    [amount] INT NOT NULL,
    [fee] INT NOT NULL,
    [status] NVARCHAR(30) NOT NULL,
    [description] NVARCHAR(1000) NULL,
    [createdAt] DATETIME2 NOT NULL CONSTRAINT [DF_Transaction_createdAt] DEFAULT SYSUTCDATETIME(),
    [updatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_Transaction] PRIMARY KEY ([id]),
    CONSTRAINT [FK_Transaction_Ticket_ticketCode]
      FOREIGN KEY ([ticketCode]) REFERENCES dbo.[Ticket]([code])
      ON DELETE CASCADE ON UPDATE NO ACTION
);
GO

CREATE UNIQUE INDEX [User_email_key] ON dbo.[User]([email]);
CREATE UNIQUE INDEX [Flight_flightNumber_key] ON dbo.[Flight]([flightNumber]);
CREATE INDEX [Seat_flightId_idx] ON dbo.[Seat]([flightId]);
CREATE UNIQUE INDEX [Seat_flightId_seatNumber_key] ON dbo.[Seat]([flightId], [seatNumber]);
CREATE INDEX [Booking_userId_idx] ON dbo.[Booking]([userId]);
CREATE INDEX [Booking_flightId_idx] ON dbo.[Booking]([flightId]);
CREATE INDEX [Payment_userId_idx] ON dbo.[Payment]([userId]);
CREATE INDEX [Payment_bookingId_idx] ON dbo.[Payment]([bookingId]);
CREATE UNIQUE INDEX [Ticket_code_key] ON dbo.[Ticket]([code]);
CREATE INDEX [Transaction_ticketCode_idx] ON dbo.[Transaction]([ticketCode]);
GO
