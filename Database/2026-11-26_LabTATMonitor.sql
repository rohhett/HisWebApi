/* =====================================================================
   Lab TAT Monitor  —  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)
   Design    : owner's "Lab TAT Monitor" prototype (Test + Priority + Shift + Working Calendar).

   Where the times come from (dbo.PatientInvestigationDetails, pathology tests only):
     order      = CreatedOn
     collected  = SampleCollectedOn
     received   = SampleReceivedByDepartmentOn, else SentToDepartmentOn, else SampleCollectedOn
                  (tests without department receiving never get a receipt time)
     entered    = ResultDoneOn
     verified   = ReportApprovedOn
     dispatched = DispatchedOn, else ReportPrintedOn
   Department = ServiceItemMaster.SubSubCategoryId under a SubCategory with LabTypeId = 1 (Pathology).
   Priority   = LabTATPriorityMaster row whose UrgentFlag = PatientInvestigationDetails.isUrgent
                (Routine = 0, Urgent = 1; Emergency / STAT have no flag in the sample yet).
   Test TAT already kept in ServiceItemMaster.tatInMin (or TatInHour) is used as a test-level rule by the API.
   The due-time calculation (rule precedence + working calendar) is done in C# (LabRepository.LabTAT.cs).

   Objects
     1.  TABLE      dbo.LabTATSetting            (one row per hospital: TAT start/end point, at-risk %, target %)
     2.  TABLE      dbo.LabTATPriorityMaster     (Routine / Urgent / Emergency / STAT + default clock)
     3.  TABLE      dbo.LabTATShiftMaster        (Morning / Evening / Night)
     4.  TABLE      dbo.LabTATRuleMaster         (Test / Department / Default × Priority × Shift → minutes + clock)
     5.  TABLE      dbo.LabTATWorkingHours       (per department, per weekday: open / closed, from–to)
     6.  TABLE      dbo.LabTATHolidayMaster      (date, name, department or all)
     7.  TABLE      dbo.LabTATDelayReasonMaster  (late reasons)
     8.  TABLE      dbo.LabTATDelayDetails       (late reason per sample)
     9.  SEED       settings, 4 priorities, 3 shifts, 4 default rules, 7 delay reasons (hospital 1)
     10. PROCEDURE  dbo.S_GetLabTATConfig        (8 result sets, cached by the API)
     11. PROCEDURE  dbo.S_GetLabTATSamples       (sample times for the dashboard / worklist / late register)
     12. PROCEDURE  dbo.IU_LabTATRule            (@Result >0 id, -1 same rule exists, 0 unknown id)
     13. PROCEDURE  dbo.U_LabTATRuleStatus       (@Result >0 id, 0 unknown id, -2 last default rule of a priority)
     14. PROCEDURE  dbo.U_LabTATPriorityClock    (@Result >0 id, 0 unknown id)
     15. PROCEDURE  dbo.U_LabTATShift            (@Result >0 id, 0 unknown id)
     16. PROCEDURE  dbo.IU_LabTATSetting         (@Result >0 id)
     17. PROCEDURE  dbo.IU_LabTATWorkingHours    (@Result >0 id)
     18. PROCEDURE  dbo.IU_LabTATHoliday         (@Result >0 id, -1 same date + department exists, 0 unknown id)
     19. PROCEDURE  dbo.U_LabTATHolidayStatus    (@Result >0 id, 0 unknown id)
     20. PROCEDURE  dbo.IU_LabTATDelayReason     (@Result >0 PatientInvestigationId, 0 unknown sample)
     21. MENU ROW   dbo.NavigationSubMenuMaster  ('Lab TAT Monitor' / lab-tat-monitor under tab 'Sample Processing')
     22. ROLE MAP   Laboratory + Admin roles get the page; their users get the right (UserMenuMaster)

   After running: restart the API; delete Redis keys _UserTabMenu_*, _RoleWiseMenuMapping_*, _UserWiseMenuMapping_*;
   users log in again to see "Lab TAT Monitor" under Sample Processing.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. LabTATSetting ---------- */
IF OBJECT_ID('dbo.LabTATSetting', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATSetting
    (
        LabTATSettingId     int IDENTITY(1,1) NOT NULL,
        HospId              int NOT NULL,
        TatStartPoint       nvarchar(20) NOT NULL CONSTRAINT DF_LabTATSetting_TatStartPoint DEFAULT ('received'),
        TatEndPoint         nvarchar(20) NOT NULL CONSTRAINT DF_LabTATSetting_TatEndPoint DEFAULT ('verified'),
        AtRiskPercent       int NOT NULL CONSTRAINT DF_LabTATSetting_AtRiskPercent DEFAULT (25),
        OnTimeTargetPercent int NOT NULL CONSTRAINT DF_LabTATSetting_OnTimeTargetPercent DEFAULT (90),
        CreatedBy           int NULL,
        CreatedOn           datetime NULL CONSTRAINT DF_LabTATSetting_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy      int NULL,
        LastModifiedOn      datetime NULL,
        IpAddress           nvarchar(20) NULL,
        CONSTRAINT PK_LabTATSetting PRIMARY KEY CLUSTERED (LabTATSettingId)
    );
END
GO

/* ---------- 2. LabTATPriorityMaster ---------- */
IF OBJECT_ID('dbo.LabTATPriorityMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATPriorityMaster
    (
        PriorityId      int IDENTITY(1,1) NOT NULL,
        HospId          int NOT NULL,
        PriorityName    nvarchar(50) NOT NULL,
        ClockType       nvarchar(10) NOT NULL CONSTRAINT DF_LabTATPriorityMaster_ClockType DEFAULT ('WORKING'),  -- WORKING | 24X7
        ColorCode       nvarchar(10) NULL,
        UrgentFlag      int NULL,            -- PatientInvestigationDetails.isUrgent value that means this priority
        SequenceNo      int NOT NULL CONSTRAINT DF_LabTATPriorityMaster_SequenceNo DEFAULT (0),
        IsActive        int NOT NULL CONSTRAINT DF_LabTATPriorityMaster_IsActive DEFAULT (1),
        CreatedBy       int NULL,
        CreatedOn       datetime NULL CONSTRAINT DF_LabTATPriorityMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int NULL,
        LastModifiedOn  datetime NULL,
        IpAddress       nvarchar(20) NULL,
        CONSTRAINT PK_LabTATPriorityMaster PRIMARY KEY CLUSTERED (PriorityId)
    );
END
GO

/* ---------- 3. LabTATShiftMaster ---------- */
IF OBJECT_ID('dbo.LabTATShiftMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATShiftMaster
    (
        ShiftId         int IDENTITY(1,1) NOT NULL,
        HospId          int NOT NULL,
        ShiftName       nvarchar(50) NOT NULL,
        FromTime        nvarchar(5) NOT NULL,    -- HH:mm
        ToTime          nvarchar(5) NOT NULL,    -- HH:mm (less than FromTime = crosses midnight)
        SequenceNo      int NOT NULL CONSTRAINT DF_LabTATShiftMaster_SequenceNo DEFAULT (0),
        IsActive        int NOT NULL CONSTRAINT DF_LabTATShiftMaster_IsActive DEFAULT (1),
        CreatedBy       int NULL,
        CreatedOn       datetime NULL CONSTRAINT DF_LabTATShiftMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int NULL,
        LastModifiedOn  datetime NULL,
        IpAddress       nvarchar(20) NULL,
        CONSTRAINT PK_LabTATShiftMaster PRIMARY KEY CLUSTERED (ShiftId)
    );
END
GO

/* ---------- 4. LabTATRuleMaster ---------- */
IF OBJECT_ID('dbo.LabTATRuleMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATRuleMaster
    (
        RuleId          int IDENTITY(1,1) NOT NULL,
        HospId          int NOT NULL,
        RuleLevel       nvarchar(10) NOT NULL,   -- TEST | DEPT | ALL
        InvestigationId int NOT NULL CONSTRAINT DF_LabTATRuleMaster_InvestigationId DEFAULT (0),  -- ServiceItemId when TEST
        DepartmentId    int NOT NULL CONSTRAINT DF_LabTATRuleMaster_DepartmentId DEFAULT (0),     -- SubSubCategoryId when DEPT
        PriorityId      int NOT NULL CONSTRAINT DF_LabTATRuleMaster_PriorityId DEFAULT (0),       -- 0 = any priority
        ShiftId         int NOT NULL CONSTRAINT DF_LabTATRuleMaster_ShiftId DEFAULT (0),          -- 0 = any shift
        TatMinutes      int NOT NULL,
        ClockType       nvarchar(10) NOT NULL CONSTRAINT DF_LabTATRuleMaster_ClockType DEFAULT ('INHERIT'), -- INHERIT | WORKING | 24X7
        Note            nvarchar(200) NULL,
        IsActive        int NOT NULL CONSTRAINT DF_LabTATRuleMaster_IsActive DEFAULT (1),
        CreatedBy       int NULL,
        CreatedOn       datetime NULL CONSTRAINT DF_LabTATRuleMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int NULL,
        LastModifiedOn  datetime NULL,
        IpAddress       nvarchar(20) NULL,
        CONSTRAINT PK_LabTATRuleMaster PRIMARY KEY CLUSTERED (RuleId)
    );
END
GO

/* ---------- 5. LabTATWorkingHours ---------- */
IF OBJECT_ID('dbo.LabTATWorkingHours', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATWorkingHours
    (
        WorkingHoursId  int IDENTITY(1,1) NOT NULL,
        HospId          int NOT NULL,
        DepartmentId    int NOT NULL,            -- SubSubCategoryId
        DayOfWeekNo     int NOT NULL,            -- 0 Sunday … 6 Saturday
        IsOpen          int NOT NULL CONSTRAINT DF_LabTATWorkingHours_IsOpen DEFAULT (1),
        FromTime        nvarchar(5) NULL,        -- HH:mm
        ToTime          nvarchar(5) NULL,        -- HH:mm, 24:00 allowed
        CreatedBy       int NULL,
        CreatedOn       datetime NULL CONSTRAINT DF_LabTATWorkingHours_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int NULL,
        LastModifiedOn  datetime NULL,
        IpAddress       nvarchar(20) NULL,
        CONSTRAINT PK_LabTATWorkingHours PRIMARY KEY CLUSTERED (WorkingHoursId)
    );
END
GO

/* ---------- 6. LabTATHolidayMaster ---------- */
IF OBJECT_ID('dbo.LabTATHolidayMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATHolidayMaster
    (
        HolidayId       int IDENTITY(1,1) NOT NULL,
        HospId          int NOT NULL,
        HolidayDate     date NOT NULL,
        HolidayName     nvarchar(100) NOT NULL,
        DepartmentId    int NOT NULL CONSTRAINT DF_LabTATHolidayMaster_DepartmentId DEFAULT (0),  -- 0 = all departments
        IsActive        int NOT NULL CONSTRAINT DF_LabTATHolidayMaster_IsActive DEFAULT (1),
        CreatedBy       int NULL,
        CreatedOn       datetime NULL CONSTRAINT DF_LabTATHolidayMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int NULL,
        LastModifiedOn  datetime NULL,
        IpAddress       nvarchar(20) NULL,
        CONSTRAINT PK_LabTATHolidayMaster PRIMARY KEY CLUSTERED (HolidayId)
    );
END
GO

/* ---------- 7. LabTATDelayReasonMaster ---------- */
IF OBJECT_ID('dbo.LabTATDelayReasonMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATDelayReasonMaster
    (
        DelayReasonId   int IDENTITY(1,1) NOT NULL,
        HospId          int NOT NULL,
        ReasonName      nvarchar(100) NOT NULL,
        SequenceNo      int NOT NULL CONSTRAINT DF_LabTATDelayReasonMaster_SequenceNo DEFAULT (0),
        IsActive        int NOT NULL CONSTRAINT DF_LabTATDelayReasonMaster_IsActive DEFAULT (1),
        CreatedBy       int NULL,
        CreatedOn       datetime NULL CONSTRAINT DF_LabTATDelayReasonMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int NULL,
        LastModifiedOn  datetime NULL,
        IpAddress       nvarchar(20) NULL,
        CONSTRAINT PK_LabTATDelayReasonMaster PRIMARY KEY CLUSTERED (DelayReasonId)
    );
END
GO

/* ---------- 8. LabTATDelayDetails ---------- */
IF OBJECT_ID('dbo.LabTATDelayDetails', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabTATDelayDetails
    (
        LabTATDelayId          int IDENTITY(1,1) NOT NULL,
        HospId                 int NOT NULL,
        PatientInvestigationId int NOT NULL,
        DelayReasonId          int NOT NULL,
        Remark                 nvarchar(256) NULL,
        CreatedBy              int NULL,
        CreatedOn              datetime NULL CONSTRAINT DF_LabTATDelayDetails_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy         int NULL,
        LastModifiedOn         datetime NULL,
        IpAddress              nvarchar(20) NULL,
        CONSTRAINT PK_LabTATDelayDetails PRIMARY KEY CLUSTERED (LabTATDelayId)
    );
    CREATE UNIQUE INDEX UX_LabTATDelayDetails_Investigation ON dbo.LabTATDelayDetails (HospId, PatientInvestigationId);
END
GO

/* ---------- 9. Seed (hospital 1; other hospitals get the same rows on first save of settings) ---------- */
DECLARE @h int = 1;

IF NOT EXISTS (SELECT 1 FROM dbo.LabTATSetting WHERE HospId = @h)
    INSERT INTO dbo.LabTATSetting (HospId, TatStartPoint, TatEndPoint, AtRiskPercent, OnTimeTargetPercent, CreatedBy, IpAddress)
    VALUES (@h, 'received', 'verified', 25, 90, 1, '127.0.0.1');

IF NOT EXISTS (SELECT 1 FROM dbo.LabTATPriorityMaster WHERE HospId = @h)
    INSERT INTO dbo.LabTATPriorityMaster (HospId, PriorityName, ClockType, ColorCode, UrgentFlag, SequenceNo, CreatedBy, IpAddress)
    VALUES (@h, 'Routine',   'WORKING', '#64748B', 0,    1, 1, '127.0.0.1'),
           (@h, 'Urgent',    'WORKING', '#D97706', 1,    2, 1, '127.0.0.1'),
           (@h, 'Emergency', '24X7',    '#DC2626', NULL, 3, 1, '127.0.0.1'),
           (@h, 'STAT',      '24X7',    '#7C3AED', NULL, 4, 1, '127.0.0.1');

IF NOT EXISTS (SELECT 1 FROM dbo.LabTATShiftMaster WHERE HospId = @h)
    INSERT INTO dbo.LabTATShiftMaster (HospId, ShiftName, FromTime, ToTime, SequenceNo, CreatedBy, IpAddress)
    VALUES (@h, 'Morning', '08:00', '14:00', 1, 1, '127.0.0.1'),
           (@h, 'Evening', '14:00', '20:00', 2, 1, '127.0.0.1'),
           (@h, 'Night',   '20:00', '08:00', 3, 1, '127.0.0.1');

-- Default rule per priority (prototype: Routine 4 h, Urgent 2 h, Emergency 1 h, STAT 45 min)
IF NOT EXISTS (SELECT 1 FROM dbo.LabTATRuleMaster WHERE HospId = @h AND RuleLevel = 'ALL')
    INSERT INTO dbo.LabTATRuleMaster (HospId, RuleLevel, PriorityId, ShiftId, TatMinutes, ClockType, Note, CreatedBy, IpAddress)
    SELECT @h, 'ALL', p.PriorityId, 0,
           CASE p.PriorityName WHEN 'Routine' THEN 240 WHEN 'Urgent' THEN 120 WHEN 'Emergency' THEN 60 ELSE 45 END,
           'INHERIT', 'Default', 1, '127.0.0.1'
    FROM dbo.LabTATPriorityMaster p WHERE p.HospId = @h;

IF NOT EXISTS (SELECT 1 FROM dbo.LabTATDelayReasonMaster WHERE HospId = @h)
    INSERT INTO dbo.LabTATDelayReasonMaster (HospId, ReasonName, SequenceNo, CreatedBy, IpAddress)
    VALUES (@h, 'Sample recollection', 1, 1, '127.0.0.1'),
           (@h, 'Analyzer breakdown / QC fail', 2, 1, '127.0.0.1'),
           (@h, 'Reagent out of stock', 3, 1, '127.0.0.1'),
           (@h, 'Repeat / rerun', 4, 1, '127.0.0.1'),
           (@h, 'Pending verification', 5, 1, '127.0.0.1'),
           (@h, 'Delay in sample receipt', 6, 1, '127.0.0.1'),
           (@h, 'Outsourced test', 7, 1, '127.0.0.1');
GO

/* ---------- 10. S_GetLabTATConfig ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetLabTATConfig
    @hospId int
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. setting
    SELECT TOP 1 LabTATSettingId, TatStartPoint, TatEndPoint, AtRiskPercent, OnTimeTargetPercent
    FROM dbo.LabTATSetting WHERE HospId = @hospId ORDER BY LabTATSettingId;

    -- 2. priorities
    SELECT PriorityId, PriorityName, ClockType, ISNULL(ColorCode, '#64748B') AS ColorCode, UrgentFlag, SequenceNo
    FROM dbo.LabTATPriorityMaster WHERE HospId = @hospId AND IsActive = 1 ORDER BY SequenceNo, PriorityId;

    -- 3. shifts
    SELECT ShiftId, ShiftName, FromTime, ToTime, SequenceNo
    FROM dbo.LabTATShiftMaster WHERE HospId = @hospId AND IsActive = 1 ORDER BY SequenceNo, ShiftId;

    -- 4. active rules
    SELECT RuleId, RuleLevel, InvestigationId, DepartmentId, PriorityId, ShiftId, TatMinutes, ClockType, ISNULL(Note, '') AS Note
    FROM dbo.LabTATRuleMaster WHERE HospId = @hospId AND IsActive = 1 ORDER BY RuleId;

    -- 5. working hours
    SELECT DepartmentId, DayOfWeekNo, IsOpen, ISNULL(FromTime, '') AS FromTime, ISNULL(ToTime, '') AS ToTime
    FROM dbo.LabTATWorkingHours WHERE HospId = @hospId ORDER BY DepartmentId, DayOfWeekNo;

    -- 6. holidays
    SELECT HolidayId, HolidayDate, HolidayName, DepartmentId
    FROM dbo.LabTATHolidayMaster WHERE HospId = @hospId AND IsActive = 1 ORDER BY HolidayDate DESC;

    -- 7. delay reasons
    SELECT DelayReasonId, ReasonName
    FROM dbo.LabTATDelayReasonMaster WHERE HospId = @hospId AND IsActive = 1 ORDER BY SequenceNo, DelayReasonId;

    -- 8. lab departments (pathology sub-sub-categories)
    SELECT ssc.SubSubCategoryId AS DepartmentId, LTRIM(RTRIM(ssc.SubSubCategoryName)) AS DepartmentName
    FROM dbo.SubSubCategoryMaster ssc
    JOIN dbo.SubCategoryMaster sc ON sc.SubCategoryId = ssc.SubCategoryId
    WHERE sc.LabTypeId = 1 AND ISNULL(ssc.IsActive, 1) = 1
    ORDER BY ssc.SubSubCategoryName;

    -- 9. lab tests with their Investigation Master TAT
    SELECT sim.ServiceItemId AS InvestigationId, LTRIM(RTRIM(sim.Name)) AS InvestigationName,
           ISNULL(sim.Code, '') AS InvestigationCode, ISNULL(sim.SubSubCategoryId, 0) AS DepartmentId,
           CASE WHEN ISNULL(sim.tatInMin, 0) > 0 THEN sim.tatInMin
                WHEN ISNULL(sim.TatInHour, 0) > 0 THEN sim.TatInHour * 60 ELSE 0 END AS MasterTatMinutes
    FROM dbo.ServiceItemMaster sim
    JOIN dbo.SubCategoryMaster sc ON sc.SubCategoryId = sim.SubCategoryId
    WHERE sc.LabTypeId = 1 AND ISNULL(sim.IsActive, 1) = 1
    ORDER BY sim.Name;
END
GO

/* ---------- 11. S_GetLabTATSamples ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetLabTATSamples
    @hospId   int,
    @fromDate datetime        -- samples ordered on or after this time (the API widens it a little)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT PID.PatientInvestigationId,
           ISNULL(PID.LabNo, 0) AS LabNo,
           ISNULL(PID.BarCode, '') AS BarCode,
           PID.InvestigationId,
           ISNULL(SIM.SubSubCategoryId, 0) AS DepartmentId,
           ISNULL(PID.isUrgent, 0) AS IsUrgent,
           ISNULL(PVD.Type, '') AS VisitType,
           ISNULL(PM.UHID, '') AS UHID,
           ISNULL(PM.PatientName, '') AS PatientName,
           PID.CreatedOn AS OrderedOn,
           PID.SampleCollectedOn AS CollectedOn,
           COALESCE(PID.SampleReceivedByDepartmentOn, PID.SentToDepartmentOn, PID.SampleCollectedOn) AS ReceivedOn,
           PID.ResultDoneOn AS EnteredOn,
           CASE WHEN ISNULL(PID.IsReportApproved, 0) = 1 THEN PID.ReportApprovedOn END AS VerifiedOn,
           CASE WHEN ISNULL(PID.IsDispatched, 0) = 1 THEN PID.DispatchedOn
                WHEN ISNULL(PID.isReportPrinted, 0) = 1 THEN PID.ReportPrintedOn END AS DispatchedOn,
           ISNULL(D.DelayReasonId, 0) AS DelayReasonId,
           ISNULL(R.ReasonName, '') AS DelayReason
    FROM dbo.PatientInvestigationDetails PID
    JOIN dbo.ServiceItemMaster SIM ON SIM.ServiceItemId = PID.InvestigationId
    JOIN dbo.SubCategoryMaster SC ON SC.SubCategoryId = SIM.SubCategoryId AND SC.LabTypeId = 1
    LEFT JOIN dbo.PatientVisitDetails PVD ON PVD.VisitId = PID.VisitId
    LEFT JOIN dbo.PatientMaster PM ON PM.PatientId = PID.PatientId
    LEFT JOIN dbo.LabTATDelayDetails D ON D.HospId = PID.HospId AND D.PatientInvestigationId = PID.PatientInvestigationId
    LEFT JOIN dbo.LabTATDelayReasonMaster R ON R.DelayReasonId = D.DelayReasonId
    WHERE PID.HospId = @hospId
      AND ISNULL(PID.IsCancel, 0) = 0
      AND ISNULL(PID.isSampleRejected, 0) = 0
      AND PID.CreatedOn >= @fromDate;
END
GO

/* ---------- 12. IU_LabTATRule ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabTATRule
    @hospId          int,
    @ruleId          int = 0,
    @ruleLevel       nvarchar(10),
    @investigationId int = 0,
    @departmentId    int = 0,
    @priorityId      int = 0,
    @shiftId         int = 0,
    @tatMinutes      int,
    @clockType       nvarchar(10) = 'INHERIT',
    @note            nvarchar(200) = NULL,
    @userId          int,
    @IpAddress       nvarchar(20) = NULL,
    @Result          int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ruleLevel = UPPER(LTRIM(RTRIM(@ruleLevel)));
    SET @clockType = UPPER(LTRIM(RTRIM(@clockType)));
    SET @note = NULLIF(LTRIM(RTRIM(@note)), '');
    IF @ruleLevel <> 'TEST' SET @investigationId = 0;
    IF @ruleLevel <> 'DEPT' SET @departmentId = 0;

    IF @ruleId = 0 AND EXISTS (SELECT 1 FROM dbo.LabTATRuleMaster
               WHERE HospId = @hospId AND IsActive = 1 AND RuleLevel = @ruleLevel
                 AND InvestigationId = @investigationId AND DepartmentId = @departmentId
                 AND PriorityId = @priorityId AND ShiftId = @shiftId)
    BEGIN
        SET @Result = -1; RETURN;
    END

    IF @ruleId = 0
    BEGIN
        INSERT INTO dbo.LabTATRuleMaster (HospId, RuleLevel, InvestigationId, DepartmentId, PriorityId, ShiftId, TatMinutes, ClockType, Note, IsActive, CreatedBy, CreatedOn, IpAddress)
        VALUES (@hospId, @ruleLevel, @investigationId, @departmentId, @priorityId, @shiftId, @tatMinutes, @clockType, @note, 1, @userId, GETDATE(), @IpAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        -- only minutes, clock and note change on an existing rule; its key stays
        UPDATE dbo.LabTATRuleMaster
        SET TatMinutes = @tatMinutes, ClockType = @clockType, Note = ISNULL(@note, Note),
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE RuleId = @ruleId AND HospId = @hospId AND IsActive = 1;
        SET @Result = CASE WHEN @@ROWCOUNT > 0 THEN @ruleId ELSE 0 END;
    END
END
GO

/* ---------- 13. U_LabTATRuleStatus (remove = IsActive 0) ---------- */
CREATE OR ALTER PROCEDURE dbo.U_LabTATRuleStatus
    @hospId    int,
    @ruleId    int,
    @userId    int,
    @IpAddress nvarchar(20) = NULL,
    @Result    int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @level nvarchar(10), @prio int, @shift int;
    SELECT @level = RuleLevel, @prio = PriorityId, @shift = ShiftId
    FROM dbo.LabTATRuleMaster WHERE RuleId = @ruleId AND HospId = @hospId AND IsActive = 1;

    IF @level IS NULL BEGIN SET @Result = 0; RETURN; END

    -- prototype rule: the default (all tests, any shift) rule of a priority cannot be removed, only its minutes changed
    IF @level = 'ALL' AND @prio > 0 AND @shift = 0
    BEGIN
        SET @Result = -2; RETURN;
    END

    UPDATE dbo.LabTATRuleMaster
    SET IsActive = 0, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE RuleId = @ruleId AND HospId = @hospId;
    SET @Result = @ruleId;
END
GO

/* ---------- 14. U_LabTATPriorityClock ---------- */
CREATE OR ALTER PROCEDURE dbo.U_LabTATPriorityClock
    @hospId     int,
    @priorityId int,
    @clockType  nvarchar(10),
    @userId     int,
    @IpAddress  nvarchar(20) = NULL,
    @Result     int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.LabTATPriorityMaster
    SET ClockType = UPPER(LTRIM(RTRIM(@clockType))), LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE PriorityId = @priorityId AND HospId = @hospId;
    SET @Result = CASE WHEN @@ROWCOUNT > 0 THEN @priorityId ELSE 0 END;
END
GO

/* ---------- 15. U_LabTATShift ---------- */
CREATE OR ALTER PROCEDURE dbo.U_LabTATShift
    @hospId    int,
    @shiftId   int,
    @fromTime  nvarchar(5),
    @toTime    nvarchar(5),
    @userId    int,
    @IpAddress nvarchar(20) = NULL,
    @Result    int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.LabTATShiftMaster
    SET FromTime = LTRIM(RTRIM(@fromTime)), ToTime = LTRIM(RTRIM(@toTime)),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE ShiftId = @shiftId AND HospId = @hospId;
    SET @Result = CASE WHEN @@ROWCOUNT > 0 THEN @shiftId ELSE 0 END;
END
GO

/* ---------- 16. IU_LabTATSetting ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabTATSetting
    @hospId              int,
    @tatStartPoint       nvarchar(20),
    @tatEndPoint         nvarchar(20),
    @atRiskPercent       int,
    @onTimeTargetPercent int,
    @userId              int,
    @IpAddress           nvarchar(20) = NULL,
    @Result              int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @id int = (SELECT TOP 1 LabTATSettingId FROM dbo.LabTATSetting WHERE HospId = @hospId ORDER BY LabTATSettingId);
    IF @id IS NULL
    BEGIN
        INSERT INTO dbo.LabTATSetting (HospId, TatStartPoint, TatEndPoint, AtRiskPercent, OnTimeTargetPercent, CreatedBy, CreatedOn, IpAddress)
        VALUES (@hospId, @tatStartPoint, @tatEndPoint, @atRiskPercent, @onTimeTargetPercent, @userId, GETDATE(), @IpAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.LabTATSetting
        SET TatStartPoint = @tatStartPoint, TatEndPoint = @tatEndPoint, AtRiskPercent = @atRiskPercent,
            OnTimeTargetPercent = @onTimeTargetPercent, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE LabTATSettingId = @id;
        SET @Result = @id;
    END
END
GO

/* ---------- 17. IU_LabTATWorkingHours (one weekday of one department) ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabTATWorkingHours
    @hospId       int,
    @departmentId int,
    @dayOfWeekNo  int,
    @isOpen       int,
    @fromTime     nvarchar(5) = NULL,
    @toTime       nvarchar(5) = NULL,
    @userId       int,
    @IpAddress    nvarchar(20) = NULL,
    @Result       int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @isOpen = 0 SELECT @fromTime = NULL, @toTime = NULL;
    DECLARE @id int = (SELECT TOP 1 WorkingHoursId FROM dbo.LabTATWorkingHours
                       WHERE HospId = @hospId AND DepartmentId = @departmentId AND DayOfWeekNo = @dayOfWeekNo);
    IF @id IS NULL
    BEGIN
        INSERT INTO dbo.LabTATWorkingHours (HospId, DepartmentId, DayOfWeekNo, IsOpen, FromTime, ToTime, CreatedBy, CreatedOn, IpAddress)
        VALUES (@hospId, @departmentId, @dayOfWeekNo, @isOpen, @fromTime, @toTime, @userId, GETDATE(), @IpAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.LabTATWorkingHours
        SET IsOpen = @isOpen, FromTime = @fromTime, ToTime = @toTime, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE WorkingHoursId = @id;
        SET @Result = @id;
    END
END
GO

/* ---------- 18. IU_LabTATHoliday ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabTATHoliday
    @hospId       int,
    @holidayId    int = 0,
    @holidayDate  date,
    @holidayName  nvarchar(100),
    @departmentId int = 0,
    @userId       int,
    @IpAddress    nvarchar(20) = NULL,
    @Result       int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @holidayName = LTRIM(RTRIM(@holidayName));
    IF EXISTS (SELECT 1 FROM dbo.LabTATHolidayMaster
               WHERE HospId = @hospId AND IsActive = 1 AND HolidayId <> @holidayId
                 AND HolidayDate = @holidayDate AND DepartmentId = @departmentId)
    BEGIN
        SET @Result = -1; RETURN;
    END

    IF @holidayId = 0
    BEGIN
        INSERT INTO dbo.LabTATHolidayMaster (HospId, HolidayDate, HolidayName, DepartmentId, IsActive, CreatedBy, CreatedOn, IpAddress)
        VALUES (@hospId, @holidayDate, @holidayName, @departmentId, 1, @userId, GETDATE(), @IpAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.LabTATHolidayMaster
        SET HolidayDate = @holidayDate, HolidayName = @holidayName, DepartmentId = @departmentId,
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE HolidayId = @holidayId AND HospId = @hospId AND IsActive = 1;
        SET @Result = CASE WHEN @@ROWCOUNT > 0 THEN @holidayId ELSE 0 END;
    END
END
GO

/* ---------- 19. U_LabTATHolidayStatus (remove = IsActive 0) ---------- */
CREATE OR ALTER PROCEDURE dbo.U_LabTATHolidayStatus
    @hospId    int,
    @holidayId int,
    @userId    int,
    @IpAddress nvarchar(20) = NULL,
    @Result    int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.LabTATHolidayMaster
    SET IsActive = 0, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE HolidayId = @holidayId AND HospId = @hospId AND IsActive = 1;
    SET @Result = CASE WHEN @@ROWCOUNT > 0 THEN @holidayId ELSE 0 END;
END
GO

/* ---------- 20. IU_LabTATDelayReason (late reason of one sample) ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabTATDelayReason
    @hospId                 int,
    @patientInvestigationId int,
    @delayReasonId          int,
    @remark                 nvarchar(256) = NULL,
    @userId                 int,
    @IpAddress              nvarchar(20) = NULL,
    @Result                 int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @remark = NULLIF(LTRIM(RTRIM(@remark)), '');

    IF NOT EXISTS (SELECT 1 FROM dbo.PatientInvestigationDetails WHERE PatientInvestigationId = @patientInvestigationId AND HospId = @hospId)
       OR NOT EXISTS (SELECT 1 FROM dbo.LabTATDelayReasonMaster WHERE DelayReasonId = @delayReasonId AND HospId = @hospId AND IsActive = 1)
    BEGIN
        SET @Result = 0; RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.LabTATDelayDetails WHERE HospId = @hospId AND PatientInvestigationId = @patientInvestigationId)
        UPDATE dbo.LabTATDelayDetails
        SET DelayReasonId = @delayReasonId, Remark = @remark, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE HospId = @hospId AND PatientInvestigationId = @patientInvestigationId;
    ELSE
        INSERT INTO dbo.LabTATDelayDetails (HospId, PatientInvestigationId, DelayReasonId, Remark, CreatedBy, CreatedOn, IpAddress)
        VALUES (@hospId, @patientInvestigationId, @delayReasonId, @remark, @userId, GETDATE(), @IpAddress);

    SET @Result = @patientInvestigationId;
END
GO

/* ---------- 21. Menu row (tab 'Sample Processing') ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'lab-tat-monitor')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Sample Processing' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Sample Processing" not found - add "Lab TAT Monitor" (lab-tat-monitor) from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'Lab TAT Monitor', 1, 1, 'lab-tat-monitor', GETDATE(), 1, '127.0.0.1', 10);
END
GO

/* ---------- 22. Laboratory and Admin roles get the page, and every user of those roles gets the right
              (same as 2026-11-26_LaboratoryRoleMenus / AdminRoleAllMenus / GrantPharmacyLabUserMenus) ---------- */
SET XACT_ABORT ON;
DECLARE @subMenuId int = (SELECT TOP 1 SubMenuId FROM dbo.NavigationSubMenuMaster WHERE LTRIM(RTRIM(URL)) = 'lab-tat-monitor');
IF @subMenuId IS NULL
    PRINT 'Menu row lab-tat-monitor missing - role and user rights not added.';
ELSE
BEGIN
    DECLARE @role TABLE (RoleId int PRIMARY KEY);
    INSERT @role SELECT MIN(RoleId) FROM dbo.RoleMaster WHERE LTRIM(RTRIM(RoleName)) IN ('Laboratory', 'Admin') GROUP BY LTRIM(RTRIM(RoleName));

    DECLARE @branch TABLE (HospId int, BranchId int, RoleId int);
    INSERT @branch
    SELECT HospId, BranchId, RoleId FROM dbo.RoleWiseMenuMappingMaster WHERE RoleId IN (SELECT RoleId FROM @role)
    UNION
    SELECT HospId, BranchId, RoleId FROM dbo.UserRoleMapping WHERE RoleId IN (SELECT RoleId FROM @role) AND IsActive = 1 AND TypeId = 1;

    BEGIN TRANSACTION;

    INSERT INTO dbo.RoleWiseMenuMappingMaster (HospId, BranchId, RoleId, SubMenuId, CreatedBy, CreatedOn)
    SELECT b.HospId, b.BranchId, b.RoleId, @subMenuId, 1, GETDATE()
    FROM @branch b
    WHERE NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster m
                      WHERE m.RoleId = b.RoleId AND m.BranchId = b.BranchId AND m.HospId = b.HospId AND m.SubMenuId = @subMenuId);
    PRINT CONCAT('Role menu rows added: ', @@ROWCOUNT);

    UPDATE u SET IsActive = 1, LastModifiedBy = 1, LastModifiedOn = GETDATE()
    FROM dbo.UserMenuMaster u
    JOIN dbo.UserRoleMapping ur ON ur.UserId = u.UserId AND ur.RoleId = u.RoleId AND ur.BranchId = u.BranchId AND ur.IsActive = 1 AND ur.TypeId = 1
    WHERE u.TypeId = 1 AND u.IsActive = 0 AND u.SubMenuId = @subMenuId AND u.RoleId IN (SELECT RoleId FROM @role);
    PRINT CONCAT('User rights re-activated: ', @@ROWCOUNT);

    INSERT INTO dbo.UserMenuMaster (TypeId, UserId, RoleId, BranchId, SubMenuId, HospId, IsActive, CreatedBy, CreatedOn)
    SELECT DISTINCT 1, ur.UserId, ur.RoleId, ur.BranchId, @subMenuId, b.HospId, 1, 1, GETDATE()
    FROM dbo.UserRoleMapping ur
    JOIN @branch b ON b.RoleId = ur.RoleId AND b.BranchId = ur.BranchId
    WHERE ur.IsActive = 1 AND ur.TypeId = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.UserMenuMaster u
                      WHERE u.TypeId = 1 AND u.UserId = ur.UserId AND u.RoleId = ur.RoleId
                        AND u.BranchId = ur.BranchId AND u.SubMenuId = @subMenuId);
    PRINT CONCAT('User rights added: ', @@ROWCOUNT);

    COMMIT TRANSACTION;
END
GO
