/* ------------------------------------------------------------------
   002_procedures.sql — stored procedures used by the API.
   CREATE OR ALTER makes the script re-runnable. Each procedure must be
   the only statement in its batch, hence the GO separators.
   ------------------------------------------------------------------ */

CREATE OR ALTER PROCEDURE dbo.SP_Bmi_Create
    @Name     NVARCHAR(100) = NULL,
    @Age      INT,
    @HeightCm DECIMAL(5, 1),
    @WeightKg DECIMAL(5, 1),
    @Bmi      DECIMAL(5, 2),
    @Category NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.BmiRecords (Name, Age, HeightCm, WeightKg, Bmi, Category)
    VALUES (NULLIF(LTRIM(RTRIM(@Name)), N''), @Age, @HeightCm, @WeightKg, @Bmi, @Category);

    SELECT Id, Name, Age, HeightCm, WeightKg, Bmi, Category, CreatedAtUtc
    FROM dbo.BmiRecords
    WHERE Id = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_Bmi_GetById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Age, HeightCm, WeightKg, Bmi, Category, CreatedAtUtc
    FROM dbo.BmiRecords
    WHERE Id = @Id;
END
GO

/* Paged history. Result set 1: total row count. Result set 2: the requested page. */
CREATE OR ALTER PROCEDURE dbo.SP_Bmi_GetAll
    @PageNumber INT = 1,
    @PageSize   INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize   < 1 SET @PageSize   = 10;
    IF @PageSize   > 50 SET @PageSize  = 50;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.BmiRecords;

    SELECT Id, Name, Age, HeightCm, WeightKg, Bmi, Category, CreatedAtUtc
    FROM dbo.BmiRecords
    ORDER BY CreatedAtUtc DESC, Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.SP_Bmi_Delete
    @Id           INT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.BmiRecords
    WHERE Id = @Id;

    SET @RowsAffected = @@ROWCOUNT;
END
GO

/* One-row summary. COUNT(CASE ...) returns 0 (not NULL) on an empty table. */
CREATE OR ALTER PROCEDURE dbo.SP_Bmi_GetStats
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COUNT(*)                                                 AS Total,
        ISNULL(AVG(Bmi), 0)                                      AS AverageBmi,
        COUNT(CASE WHEN Category = N'Underweight' THEN 1 END)    AS Underweight,
        COUNT(CASE WHEN Category = N'Normal'      THEN 1 END)    AS [Normal],
        COUNT(CASE WHEN Category = N'Overweight'  THEN 1 END)    AS Overweight,
        COUNT(CASE WHEN Category = N'Obese'       THEN 1 END)    AS Obese
    FROM dbo.BmiRecords;
END
GO
