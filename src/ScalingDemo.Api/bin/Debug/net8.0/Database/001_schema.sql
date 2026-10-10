/* ------------------------------------------------------------------
   001_schema.sql — tables and indexes. Safe to run more than once.
   The database itself is created by DatabaseInitializer.
   ------------------------------------------------------------------ */

IF OBJECT_ID(N'dbo.BmiRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmiRecords
    (
        Id           INT           IDENTITY(1, 1) NOT NULL
                     CONSTRAINT PK_BmiRecords PRIMARY KEY,
        Name         NVARCHAR(100) NULL,
        Age          INT           NOT NULL
                     CONSTRAINT CK_BmiRecords_Age CHECK (Age BETWEEN 18 AND 120),
        HeightCm     DECIMAL(5, 1) NOT NULL
                     CONSTRAINT CK_BmiRecords_HeightCm CHECK (HeightCm BETWEEN 50 AND 260),
        WeightKg     DECIMAL(5, 1) NOT NULL
                     CONSTRAINT CK_BmiRecords_WeightKg CHECK (WeightKg BETWEEN 10 AND 500),
        Bmi          DECIMAL(5, 2) NOT NULL,
        Category     NVARCHAR(20)  NOT NULL
                     CONSTRAINT CK_BmiRecords_Category
                     CHECK (Category IN (N'Underweight', N'Normal', N'Overweight', N'Obese')),
        CreatedAtUtc DATETIME2(0)  NOT NULL
                     CONSTRAINT DF_BmiRecords_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
    );

    -- History is always read newest-first.
    CREATE INDEX IX_BmiRecords_CreatedAtUtc
        ON dbo.BmiRecords (CreatedAtUtc DESC, Id DESC);
END
GO
