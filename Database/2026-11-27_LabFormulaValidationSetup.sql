/* =====================================================================
   Lab Formula + Result Validation setup (Formula Master redesign)  —  2026-11-27
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER / guarded migration)

   Why (owner, 2026-11-27): the old formula was one row per observation (shared by every test the
   observation is mapped to), had no gender or age, stored a jQuery string or an "id=rhs" string plus a
   token table, and had no result validations. The new design:
     * formulas belong to a TEST (investigation) and a TARGET observation, one row per gender
       ('B' both / 'M' male / 'F' female). Result entry takes the patient's gender row, else the 'B' row.
     * expression language (same in the API and the screen):
         numbers, obs(<ObservationId>), AGE (completed years), AGE_MONTHS, AGE_DAYS,
         + - * / ^ ( ) and min(a,b,..) max(a,b,..) pow(a,b) sqrt abs log (base 10) ln exp round(x[,d])
       e.g. eGFR (male)  142*pow(min(obs(12)/0.9,1),-0.302)*pow(max(obs(12)/0.9,1),-1.2)*pow(0.9938,AGE)
     * result validations per test: a check over 1..n selected observations
         SUM, DIFFERENCE (1st - rest), PRODUCT, RATIO (1st / rest), AVERAGE, MIN, MAX,
         EACH (every selected value must pass), EXPRESSION (own formula)
       compared with EQ, NE, GT, GE, LT, LE, BETWEEN, NOTBETWEEN (Value1 / Value2, Tolerance for EQ/NE)
       ActionType 1 = block the save, 2 = warn only. SkipIfBlank 1 = check only when every value is entered.

   Objects
     1. TABLE      dbo.LabInvestigationFormula                (new)
     2. TABLE      dbo.LabInvestigationValidation             (new)
     3. TABLE      dbo.LabInvestigationValidationObservation  (new)
     4. MIGRATION  LabFormulaMaster + LabFormulaMasterComponents → LabInvestigationFormula
                   (rebuilt from the component rows, one row per test where the target and every used
                   observation are mapped, Gender 'B')
     5. PROCEDURE  dbo.S_GetLabInvestigationFormulaSetup   (3 result sets: formulas, validations, validation observations)
     6. PROCEDURE  dbo.IU_LabInvestigationFormula          (@Result >0 id, -1 duplicate test+target+gender,
                                                            0 unknown id, -2 target not mapped to the test,
                                                            -3 a used observation not mapped, -4 target used in its own formula)
     7. PROCEDURE  dbo.D_LabInvestigationFormula           (@Result 1 deleted, 0 unknown id)
     8. PROCEDURE  dbo.IU_LabInvestigationValidation       (@Result >0 id, -1 duplicate name in the test,
                                                            0 unknown id, -2 an observation not mapped, -3 no observation)
     9. PROCEDURE  dbo.D_LabInvestigationValidation        (@Result 1 deleted, 0 unknown id)
    10. PROCEDURE  dbo.S_GetResultEntryFormulaSetup        (4 result sets for one patient test: patient age/gender,
                                                            formulas resolved by gender, validations, validation observations)
    11. PROCEDURE  dbo.S_getPatientTabularReportForResultEntry (Formula / FormulaRight columns now read the new
                                                            table, resolved by test + patient gender; nothing else changed)
    12. DROP       old procedures S_GetFormulaMasterByObservationId, IU_LabFormulaMaster, D_LabFormulaMasterComponents,
                   I_LabFormulaMasterComponents, S_getObservationFormulaByInvestigationId, D_deleteLabFormulaByObservationid
                   and old tables LabFormulaMaster, LabFormulaMasterComponents (after step 4)

   After running: restart the API (old Lab formula endpoints are removed, new ones added) and publish the
   frontend with it; delete Redis keys _LabFormulaMaster_Obs* and _ObservationFormula_Inv* (no longer used).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1. ---------------------------------------------------------------- */
IF OBJECT_ID('dbo.LabInvestigationFormula', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabInvestigationFormula
    (
        FormulaId        int IDENTITY(1,1) NOT NULL,
        HospId           int NULL,
        InvestigationId  int NOT NULL,
        ObservationId    int NOT NULL,                 -- target observation (result is calculated)
        Gender           char(1) NOT NULL CONSTRAINT DF_LabInvestigationFormula_Gender DEFAULT ('B'),
        Expression       nvarchar(2000) NOT NULL,      -- obs(<id>), AGE, functions …
        FormulaText      nvarchar(2000) NULL,          -- same expression with observation names (display only)
        DecimalPlaces    int NOT NULL CONSTRAINT DF_LabInvestigationFormula_DecimalPlaces DEFAULT (2),
        IsActive         int NOT NULL CONSTRAINT DF_LabInvestigationFormula_IsActive DEFAULT (1),
        CreatedBy        int NULL,
        CreatedOn        datetime NULL CONSTRAINT DF_LabInvestigationFormula_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy   int NULL,
        LastModifiedOn   datetime NULL,
        IpAddress        nvarchar(40) NULL,
        CONSTRAINT PK_LabInvestigationFormula PRIMARY KEY CLUSTERED (FormulaId),
        CONSTRAINT CK_LabInvestigationFormula_Gender CHECK (Gender IN ('B','M','F'))
    );
    CREATE INDEX IX_LabInvestigationFormula_Investigation ON dbo.LabInvestigationFormula (InvestigationId, ObservationId, Gender);
END
GO

/* 2. ---------------------------------------------------------------- */
IF OBJECT_ID('dbo.LabInvestigationValidation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabInvestigationValidation
    (
        ValidationId     int IDENTITY(1,1) NOT NULL,
        HospId           int NULL,
        InvestigationId  int NOT NULL,
        ValidationName   nvarchar(100) NOT NULL,
        Gender           char(1) NOT NULL CONSTRAINT DF_LabInvestigationValidation_Gender DEFAULT ('B'),
        CheckType        varchar(20) NOT NULL,         -- SUM DIFFERENCE PRODUCT RATIO AVERAGE MIN MAX EACH EXPRESSION
        Expression       nvarchar(2000) NULL,          -- CheckType EXPRESSION only
        ExpressionText   nvarchar(2000) NULL,
        Operator         varchar(12) NOT NULL,         -- EQ NE GT GE LT LE BETWEEN NOTBETWEEN
        Value1           decimal(18,4) NOT NULL,
        Value2           decimal(18,4) NULL,           -- BETWEEN / NOTBETWEEN upper value
        Tolerance        decimal(18,4) NOT NULL CONSTRAINT DF_LabInvestigationValidation_Tolerance DEFAULT (0),
        ActionType       int NOT NULL CONSTRAINT DF_LabInvestigationValidation_ActionType DEFAULT (1),  -- 1 block, 2 warn
        SkipIfBlank      int NOT NULL CONSTRAINT DF_LabInvestigationValidation_SkipIfBlank DEFAULT (1),
        ErrorMessage     nvarchar(500) NULL,
        IsActive         int NOT NULL CONSTRAINT DF_LabInvestigationValidation_IsActive DEFAULT (1),
        CreatedBy        int NULL,
        CreatedOn        datetime NULL CONSTRAINT DF_LabInvestigationValidation_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy   int NULL,
        LastModifiedOn   datetime NULL,
        IpAddress        nvarchar(40) NULL,
        CONSTRAINT PK_LabInvestigationValidation PRIMARY KEY CLUSTERED (ValidationId),
        CONSTRAINT CK_LabInvestigationValidation_Gender CHECK (Gender IN ('B','M','F'))
    );
    CREATE INDEX IX_LabInvestigationValidation_Investigation ON dbo.LabInvestigationValidation (InvestigationId);
END
GO

/* 3. ---------------------------------------------------------------- */
IF OBJECT_ID('dbo.LabInvestigationValidationObservation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabInvestigationValidationObservation
    (
        ValidationObservationId int IDENTITY(1,1) NOT NULL,
        ValidationId     int NOT NULL,
        ObservationId    int NOT NULL,
        SequenceNo       int NOT NULL,                 -- order matters for DIFFERENCE and RATIO
        CONSTRAINT PK_LabInvestigationValidationObservation PRIMARY KEY CLUSTERED (ValidationObservationId)
    );
    CREATE INDEX IX_LabInvestigationValidationObservation_Validation ON dbo.LabInvestigationValidationObservation (ValidationId, SequenceNo);
END
GO

/* 4. Migrate the old observation-wise formulas ------------------------ */
IF OBJECT_ID('dbo.LabFormulaMaster', 'U') IS NOT NULL AND OBJECT_ID('dbo.LabFormulaMasterComponents', 'U') IS NOT NULL
BEGIN
    ;WITH Expr AS
    (
        SELECT  fm.FormulaId, fm.Observationid AS TargetId, fm.CreatedBy, fm.CreatedOn, fm.IpAddress,
                Expression = (SELECT CASE WHEN c.Type = 'observationId' THEN 'obs(' + LTRIM(RTRIM(c.Component)) + ')'
                                          ELSE LTRIM(RTRIM(c.Component)) END
                              FROM dbo.LabFormulaMasterComponents c
                              WHERE c.FormulaId = fm.FormulaId
                              ORDER BY c.SequenceNo
                              FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
        FROM dbo.LabFormulaMaster fm
        WHERE EXISTS (SELECT 1 FROM dbo.LabFormulaMasterComponents c WHERE c.FormulaId = fm.FormulaId)
    ),
    Used AS
    (
        SELECT c.FormulaId, TRY_CONVERT(int, c.Component) AS ObservationId
        FROM dbo.LabFormulaMasterComponents c
        WHERE c.Type = 'observationId'
    ),
    Target AS   -- every test that maps the target and all of its used observations
    (
        SELECT e.*, iom.InvastigationId AS InvestigationId, iom.HospId
        FROM Expr e
        INNER JOIN dbo.InvastigationObservationMapping iom ON iom.ObservationId = e.TargetId AND iom.IsActive = 1
        WHERE NOT EXISTS (SELECT 1 FROM Used u
                          WHERE u.FormulaId = e.FormulaId
                            AND NOT EXISTS (SELECT 1 FROM dbo.InvastigationObservationMapping m
                                            WHERE m.InvastigationId = iom.InvastigationId
                                              AND m.ObservationId = u.ObservationId AND m.IsActive = 1))
    )
    INSERT INTO dbo.LabInvestigationFormula (HospId, InvestigationId, ObservationId, Gender, Expression, FormulaText,
                                             DecimalPlaces, IsActive, CreatedBy, CreatedOn, IpAddress)
    SELECT  t.HospId, t.InvestigationId, t.TargetId, 'B', t.Expression,
            NULL,   -- readable text is filled below
            2, 1, t.CreatedBy, t.CreatedOn, t.IpAddress
    FROM Target t
    WHERE NOT EXISTS (SELECT 1 FROM dbo.LabInvestigationFormula x
                      WHERE x.InvestigationId = t.InvestigationId AND x.ObservationId = t.TargetId AND x.Gender = 'B');

    -- readable text for the migrated rows (one REPLACE per used observation)
    DECLARE @fid int, @txt nvarchar(2000);
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT FormulaId, Expression FROM dbo.LabInvestigationFormula WHERE FormulaText IS NULL;
    OPEN cur;
    FETCH NEXT FROM cur INTO @fid, @txt;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @txt = REPLACE(@txt, 'obs(' + CAST(om.ObservationId AS varchar(12)) + ')', '[' + om.ObservationName + ']')
        FROM dbo.ObservationMaster om
        WHERE CHARINDEX('obs(' + CAST(om.ObservationId AS varchar(12)) + ')', @txt) > 0;
        UPDATE dbo.LabInvestigationFormula SET FormulaText = @txt WHERE FormulaId = @fid;
        FETCH NEXT FROM cur INTO @fid, @txt;
    END
    CLOSE cur; DEALLOCATE cur;
END
GO

/* 5. ---------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.S_GetLabInvestigationFormulaSetup
    @investigationId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  f.FormulaId, f.InvestigationId, f.ObservationId, ISNULL(om.ObservationName, '') AS ObservationName,
            f.Gender, f.Expression, ISNULL(f.FormulaText, '') AS FormulaText, f.DecimalPlaces, f.IsActive,
            ISNULL((SELECT TOP 1 um.Name FROM dbo.UserMaster um WHERE um.Id = f.CreatedBy), '') AS CreatedBy,
            ISNULL(FORMAT(f.CreatedOn, 'dd-MM-yyyy'), '') AS CreatedOn,
            ISNULL((SELECT TOP 1 um.Name FROM dbo.UserMaster um WHERE um.Id = f.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(f.LastModifiedOn, 'dd-MM-yyyy'), '') AS LastModifiedOn
    FROM dbo.LabInvestigationFormula f
    LEFT JOIN dbo.ObservationMaster om ON om.ObservationId = f.ObservationId
    WHERE f.InvestigationId = @investigationId
    ORDER BY om.ObservationName, CASE f.Gender WHEN 'B' THEN 0 WHEN 'M' THEN 1 ELSE 2 END;

    SELECT  v.ValidationId, v.InvestigationId, v.ValidationName, v.Gender, v.CheckType,
            ISNULL(v.Expression, '') AS Expression, ISNULL(v.ExpressionText, '') AS ExpressionText,
            v.Operator, v.Value1, v.Value2, v.Tolerance, v.ActionType, v.SkipIfBlank,
            ISNULL(v.ErrorMessage, '') AS ErrorMessage, v.IsActive,
            ISNULL((SELECT TOP 1 um.Name FROM dbo.UserMaster um WHERE um.Id = v.CreatedBy), '') AS CreatedBy,
            ISNULL(FORMAT(v.CreatedOn, 'dd-MM-yyyy'), '') AS CreatedOn,
            ISNULL((SELECT TOP 1 um.Name FROM dbo.UserMaster um WHERE um.Id = v.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(v.LastModifiedOn, 'dd-MM-yyyy'), '') AS LastModifiedOn
    FROM dbo.LabInvestigationValidation v
    WHERE v.InvestigationId = @investigationId
    ORDER BY v.ValidationName;

    SELECT  vo.ValidationId, vo.ObservationId, ISNULL(om.ObservationName, '') AS ObservationName, vo.SequenceNo
    FROM dbo.LabInvestigationValidationObservation vo
    INNER JOIN dbo.LabInvestigationValidation v ON v.ValidationId = vo.ValidationId
    LEFT JOIN dbo.ObservationMaster om ON om.ObservationId = vo.ObservationId
    WHERE v.InvestigationId = @investigationId
    ORDER BY vo.ValidationId, vo.SequenceNo;
END
GO

/* 6. ---------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabInvestigationFormula
    @formulaId              int = 0,
    @hospId                 int = NULL,
    @investigationId        int,
    @observationId          int,
    @gender                 char(1) = 'B',
    @expression             nvarchar(2000),
    @formulaText            nvarchar(2000) = NULL,
    @decimalPlaces          int = 2,
    @isActive               int = 1,
    @usedObservationIdsJson nvarchar(max) = NULL,   -- JSON array of observation ids used in the expression
    @userId                 int,
    @IpAddress              nvarchar(40) = NULL,
    @Result                 int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @gender = ISNULL(NULLIF(LTRIM(RTRIM(@gender)), ''), 'B');
    SET @expression = LTRIM(RTRIM(@expression));

    IF NOT EXISTS (SELECT 1 FROM dbo.InvastigationObservationMapping
                   WHERE InvastigationId = @investigationId AND ObservationId = @observationId AND IsActive = 1)
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    IF EXISTS (SELECT 1 FROM OPENJSON(ISNULL(@usedObservationIdsJson, '[]')) j
               WHERE NOT EXISTS (SELECT 1 FROM dbo.InvastigationObservationMapping m
                                 WHERE m.InvastigationId = @investigationId AND m.IsActive = 1
                                   AND m.ObservationId = TRY_CONVERT(int, j.[value])))
    BEGIN SET @Result = -3; SELECT @Result; RETURN; END

    IF EXISTS (SELECT 1 FROM OPENJSON(ISNULL(@usedObservationIdsJson, '[]')) j WHERE TRY_CONVERT(int, j.[value]) = @observationId)
    BEGIN SET @Result = -4; SELECT @Result; RETURN; END

    IF EXISTS (SELECT 1 FROM dbo.LabInvestigationFormula
               WHERE InvestigationId = @investigationId AND ObservationId = @observationId AND Gender = @gender
                 AND FormulaId <> ISNULL(@formulaId, 0))
    BEGIN SET @Result = -1; SELECT @Result; RETURN; END

    IF ISNULL(@formulaId, 0) = 0
    BEGIN
        INSERT INTO dbo.LabInvestigationFormula (HospId, InvestigationId, ObservationId, Gender, Expression, FormulaText,
                                                 DecimalPlaces, IsActive, CreatedBy, IpAddress)
        VALUES (@hospId, @investigationId, @observationId, @gender, @expression, NULLIF(LTRIM(RTRIM(@formulaText)), ''),
                ISNULL(@decimalPlaces, 2), ISNULL(@isActive, 1), @userId, @IpAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.LabInvestigationFormula
        SET ObservationId = @observationId, Gender = @gender, Expression = @expression,
            FormulaText = NULLIF(LTRIM(RTRIM(@formulaText)), ''), DecimalPlaces = ISNULL(@decimalPlaces, 2),
            IsActive = ISNULL(@isActive, 1), LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE FormulaId = @formulaId AND InvestigationId = @investigationId;
        SET @Result = CASE WHEN @@ROWCOUNT = 0 THEN 0 ELSE @formulaId END;
    END

    SELECT @Result;
END
GO

/* 7. ---------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.D_LabInvestigationFormula
    @formulaId int,
    @Result    int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.LabInvestigationFormula WHERE FormulaId = @formulaId;
    SET @Result = CASE WHEN @@ROWCOUNT = 0 THEN 0 ELSE 1 END;
    SELECT @Result;
END
GO

/* 8. ---------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabInvestigationValidation
    @validationId     int = 0,
    @hospId           int = NULL,
    @investigationId  int,
    @validationName   nvarchar(100),
    @gender           char(1) = 'B',
    @checkType        varchar(20),
    @expression       nvarchar(2000) = NULL,
    @expressionText   nvarchar(2000) = NULL,
    @operator         varchar(12),
    @value1           decimal(18,4),
    @value2           decimal(18,4) = NULL,
    @tolerance        decimal(18,4) = 0,
    @actionType       int = 1,
    @skipIfBlank      int = 1,
    @errorMessage     nvarchar(500) = NULL,
    @isActive         int = 1,
    @observationsJson nvarchar(max),               -- [{"observationId":1,"sequenceNo":1}, …]
    @userId           int,
    @IpAddress        nvarchar(40) = NULL,
    @Result           int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @validationName = LTRIM(RTRIM(@validationName));
    SET @gender = ISNULL(NULLIF(LTRIM(RTRIM(@gender)), ''), 'B');

    DECLARE @obs TABLE (ObservationId int, SequenceNo int);
    INSERT INTO @obs (ObservationId, SequenceNo)
    SELECT DISTINCT j.observationId, j.sequenceNo
    FROM OPENJSON(ISNULL(@observationsJson, '[]')) WITH (observationId int '$.observationId', sequenceNo int '$.sequenceNo') j
    WHERE j.observationId > 0;

    IF NOT EXISTS (SELECT 1 FROM @obs)
    BEGIN SET @Result = -3; SELECT @Result; RETURN; END

    IF EXISTS (SELECT 1 FROM @obs o
               WHERE NOT EXISTS (SELECT 1 FROM dbo.InvastigationObservationMapping m
                                 WHERE m.InvastigationId = @investigationId AND m.ObservationId = o.ObservationId AND m.IsActive = 1))
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    IF EXISTS (SELECT 1 FROM dbo.LabInvestigationValidation
               WHERE InvestigationId = @investigationId AND ValidationName = @validationName
                 AND ValidationId <> ISNULL(@validationId, 0))
    BEGIN SET @Result = -1; SELECT @Result; RETURN; END

    IF ISNULL(@validationId, 0) = 0
    BEGIN
        INSERT INTO dbo.LabInvestigationValidation (HospId, InvestigationId, ValidationName, Gender, CheckType, Expression,
                    ExpressionText, Operator, Value1, Value2, Tolerance, ActionType, SkipIfBlank, ErrorMessage, IsActive,
                    CreatedBy, IpAddress)
        VALUES (@hospId, @investigationId, @validationName, @gender, @checkType, NULLIF(LTRIM(RTRIM(@expression)), ''),
                NULLIF(LTRIM(RTRIM(@expressionText)), ''), @operator, @value1, @value2, ISNULL(@tolerance, 0),
                ISNULL(@actionType, 1), ISNULL(@skipIfBlank, 1), NULLIF(LTRIM(RTRIM(@errorMessage)), ''), ISNULL(@isActive, 1),
                @userId, @IpAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.LabInvestigationValidation
        SET ValidationName = @validationName, Gender = @gender, CheckType = @checkType,
            Expression = NULLIF(LTRIM(RTRIM(@expression)), ''), ExpressionText = NULLIF(LTRIM(RTRIM(@expressionText)), ''),
            Operator = @operator, Value1 = @value1, Value2 = @value2, Tolerance = ISNULL(@tolerance, 0),
            ActionType = ISNULL(@actionType, 1), SkipIfBlank = ISNULL(@skipIfBlank, 1),
            ErrorMessage = NULLIF(LTRIM(RTRIM(@errorMessage)), ''), IsActive = ISNULL(@isActive, 1),
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE ValidationId = @validationId AND InvestigationId = @investigationId;
        IF @@ROWCOUNT = 0 BEGIN SET @Result = 0; SELECT @Result; RETURN; END
        SET @Result = @validationId;
    END

    DELETE FROM dbo.LabInvestigationValidationObservation WHERE ValidationId = @Result;
    INSERT INTO dbo.LabInvestigationValidationObservation (ValidationId, ObservationId, SequenceNo)
    SELECT @Result, ObservationId, ROW_NUMBER() OVER (ORDER BY SequenceNo, ObservationId) FROM @obs;

    SELECT @Result;
END
GO

/* 9. ---------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.D_LabInvestigationValidation
    @validationId int,
    @Result       int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.LabInvestigationValidationObservation WHERE ValidationId = @validationId;
    DELETE FROM dbo.LabInvestigationValidation WHERE ValidationId = @validationId;
    SET @Result = CASE WHEN @@ROWCOUNT = 0 THEN 0 ELSE 1 END;
    SELECT @Result;
END
GO

/* 10. --------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.S_GetResultEntryFormulaSetup
    @patientInvestigationId int
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @investigationId int, @gender char(1), @dob date, @onDate date;

    -- age is taken on the day the test was booked, so a later edit gives the same answer
    SELECT TOP 1 @investigationId = pid.InvestigationId,
                 @gender = CASE LEFT(UPPER(ISNULL(pm.Gender, '')), 1) WHEN 'M' THEN 'M' WHEN 'F' THEN 'F' ELSE 'B' END,
                 @dob = pm.DOB,
                 @onDate = CAST(ISNULL(pid.CreatedOn, GETDATE()) AS date)
    FROM dbo.PatientInvestigationDetails pid
    INNER JOIN dbo.PatientMaster pm ON pm.PatientId = pid.PatientId
    WHERE pid.PatientInvestigationId = @patientInvestigationId;

    DECLARE @ageMonths int = CASE WHEN @dob IS NULL THEN NULL
                                  ELSE DATEDIFF(MONTH, @dob, @onDate) - CASE WHEN DAY(@onDate) < DAY(@dob) THEN 1 ELSE 0 END END;

    SELECT  ISNULL(@investigationId, 0) AS InvestigationId,
            ISNULL(@gender, 'B') AS Gender,
            CAST(@ageMonths / 12 AS int) AS AgeYears,
            @ageMonths AS AgeMonths,
            DATEDIFF(DAY, @dob, @onDate) AS AgeDays;

    -- one formula per target: the patient's gender row, else the Both row
    SELECT  f.FormulaId, f.ObservationId, ISNULL(om.ObservationName, '') AS ObservationName, f.Gender,
            f.Expression, ISNULL(f.FormulaText, '') AS FormulaText, f.DecimalPlaces
    FROM dbo.LabInvestigationFormula f
    LEFT JOIN dbo.ObservationMaster om ON om.ObservationId = f.ObservationId
    WHERE f.InvestigationId = @investigationId AND f.IsActive = 1
      AND (f.Gender = @gender
           OR (f.Gender = 'B' AND NOT EXISTS (SELECT 1 FROM dbo.LabInvestigationFormula g
                                              WHERE g.InvestigationId = f.InvestigationId AND g.ObservationId = f.ObservationId
                                                AND g.Gender = @gender AND g.Gender <> 'B' AND g.IsActive = 1)));

    SELECT  v.ValidationId, v.ValidationName, v.Gender, v.CheckType, ISNULL(v.Expression, '') AS Expression,
            ISNULL(v.ExpressionText, '') AS ExpressionText, v.Operator, v.Value1, v.Value2, v.Tolerance,
            v.ActionType, v.SkipIfBlank, ISNULL(v.ErrorMessage, '') AS ErrorMessage
    FROM dbo.LabInvestigationValidation v
    WHERE v.InvestigationId = @investigationId AND v.IsActive = 1 AND v.Gender IN ('B', @gender);

    SELECT  vo.ValidationId, vo.ObservationId, ISNULL(om.ObservationName, '') AS ObservationName, vo.SequenceNo
    FROM dbo.LabInvestigationValidationObservation vo
    INNER JOIN dbo.LabInvestigationValidation v ON v.ValidationId = vo.ValidationId
    LEFT JOIN dbo.ObservationMaster om ON om.ObservationId = vo.ObservationId
    WHERE v.InvestigationId = @investigationId AND v.IsActive = 1 AND v.Gender IN ('B', @gender)
    ORDER BY vo.ValidationId, vo.SequenceNo;
END
GO

/* 11. Result entry grid: Formula / FormulaRight now from LabInvestigationFormula (same column names) */
CREATE OR ALTER PROCEDURE [dbo].[S_getPatientTabularReportForResultEntry]
@patientInvestigationId INT
AS
BEGIN
 SELECT
   PM.UHID,
   PM.PatientName,
   PVD.CurrentAge,
   PM.Gender,
   PID.LabNo,
   PID.BarCode,
   FORMAT(PID.CreatedOn,'dd-MMM-yyyy hh:mm tt')BillDate,
   isnull((select top 1 rdm.DoctorName from ReferDoctorMaster rdm where rdm.ReferDoctorId=pvd.ReferDoctorId),'')referDoctorName,
   SIM.Name AS InvestigationName,
   OM.ObservationName,
   OM.ObservationId,
   ISNULL(OM.Prefix,'')Prefix,
   ISNULL(OM.Suffix,'')Suffix,
   ISNULL(PIR.ResultValue,ISNULL(IOR.DefaultValue,''))ResultValue,
   ISNULL(PIR.MinValue,ISNULL(CAST(IOR.MinValue AS NVARCHAR(256)),''))MinValue,
   ISNULL(PIR.MaxValue,ISNULL(CAST(IOR.MaxValue AS NVARCHAR(256)),''))MaxValue,
   ISNULL(PIR.DisplayRange, ISNULL(IOR.DisplayValue,'')) DisplayRange,
   ISNULL(PIR.Unit, ISNULL(IOR.Unit,'')) Unit,
   ISNULL(PIR.MachineResult,'') MachineResult,
   ISNULL(PIR.MachineUnit,'') MachineUnit,
   ISNULL(PIR.SampleRemark,'') SampleRemark,
   ISNULL(PIR.MachineDisplayRange,'') MachineDisplayRange,
   isnull((select top 1 Method from LabMethodMaster where MethodId=om.MethodId),'')MethodName,
   -- 2026-11-27: formula of this test for the target, patient gender row first, else Both
   ISNULL(CAST(IOM.ObservationId AS nvarchar(12)) + '=' + LF.Expression,'') AS Formula,
   ISNULL(LF.Expression,'') AS FormulaRight,
     ISNULL(STUFF((SELECT '#'+LOV.LOVName
   FROM ObservationCommentLOVsMapping OLOV
   INNER JOIN ObservationListOfValuesMaster LOV ON OLOV.TypeId=2
   AND OLOV.ObservationId=IOM.ObservationId
   AND OLOV.ItemId=LOV.LOVId
   AND LOV.IsActive=1 for xml path(''),TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, ''),'')ObservationLOVs,
   IOM.IsHeader,
   ISNULL(PID.InvestigationComment,isnull(sim.InvestigationComment,''))InvestigationComment,
   PID.IsAbnormalResult,
   PID.InvestigationId,
   PID.IsResultDone,
   PID.IsReportApproved,
   OM.FieldTypeId,
   PM.PatientId  ,
   PID.isUrgent  ,
   isnull(om.RoundUp,'')RoundUp,
   iom.IsBold,
   iom.IsUnderLine,
   iom.IsMandatory,
   PIR.IsResultBold
   FROM PatientInvestigationDetails  PID
 INNER JOIN InvastigationObservationMapping IOM ON PID.InvestigationId=IOM.InvastigationId AND IOM.IsActive=1 AND PID.PatientInvestigationId=@patientInvestigationId
 INNER JOIN ObservationMaster OM ON OM.ObservationId=IOM.ObservationId AND OM.IsActive=1
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId
 INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId
 INNER JOIN PatientMaster PM ON PM.PatientId=PID.PatientId
 LEFT JOIN PatientInvestigationResultsTabularReports PIR ON PIR.PatientInvestigationId=@patientInvestigationId AND PIR.ObservationId=IOM.ObservationId
 LEFT JOIN InvastigationObservationRangeMaster IOR ON
 IOR.ObservationId=IOM.ObservationId AND IOR.FromAge<=DATEDIFF(DAY,PM.DOB,GETDATE()) AND IOR.ToAge>=DATEDIFF(DAY,PM.DOB,GETDATE()) AND IOR.Gender IN ('B',LEFT(PM.Gender,1))
 OUTER APPLY (SELECT TOP 1 F.Expression
              FROM LabInvestigationFormula F
              WHERE F.InvestigationId=PID.InvestigationId AND F.ObservationId=IOM.ObservationId AND F.IsActive=1
                AND F.Gender IN ('B',LEFT(PM.Gender,1))
              ORDER BY CASE WHEN F.Gender='B' THEN 1 ELSE 0 END) LF
 ORDER BY IOM.MappingId
END
GO

/* 12. Drop the old observation-wise formula objects ------------------- */
DROP PROCEDURE IF EXISTS dbo.S_GetFormulaMasterByObservationId;
DROP PROCEDURE IF EXISTS dbo.IU_LabFormulaMaster;
DROP PROCEDURE IF EXISTS dbo.D_LabFormulaMasterComponents;
DROP PROCEDURE IF EXISTS dbo.I_LabFormulaMasterComponents;
DROP PROCEDURE IF EXISTS dbo.S_getObservationFormulaByInvestigationId;
DROP PROCEDURE IF EXISTS dbo.D_deleteLabFormulaByObservationid;
GO
IF OBJECT_ID('dbo.LabFormulaMasterComponents', 'U') IS NOT NULL DROP TABLE dbo.LabFormulaMasterComponents;
IF OBJECT_ID('dbo.LabFormulaMaster', 'U') IS NOT NULL DROP TABLE dbo.LabFormulaMaster;
GO
