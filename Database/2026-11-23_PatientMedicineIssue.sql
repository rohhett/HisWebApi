/* =====================================================================
   Patient Medicine Issue (pharmacy sale / IPD issue)      2026-11-23
   Target DB  : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Rules ported from OLD Store/PatientMedicineIssue (Store.saveStoreBilling) and IPD indents
   (PatientMedicineIndent, U_UpdateIndentItem, U_rejectIndentItem, U_UpdateIndent, U_rejectIndent);
   screen and workflow from the MediStock design "Medicine Sale / Issue".

   Objects
     1. COLUMN     dbo.PatientMaster.IsGeneralPatient        (walk-in / general patient, OLD isGeneralPatient)
     2. TABLE      dbo.StoreBills                            (one row per pharmacy bill / IPD issue)
     3. TABLE      dbo.PatientMedicineIndent (+ Items)        (ward indents - OLD columns + Priority, Remarks)
     4. RIGHT      UserRightsMaster 'CanSellNarcoDrugs'       (OLD right)
     5. SEQUENCES  type 5 Store-Bill(Issue) 'PH', 6 Store-Receipt(Issue) 'PHR', 26 WalkIn UHID 'WI'
                   (only when the type has no BranchSequenceMapping row yet)
     6. PROCS      I_PharmacyWalkInPatient       walk-in patient, UHID from sequence 26
                   S_SearchPatientForSale        OPD patients / admitted IPD patients
                   S_SearchItemForSale           items with stock in the store (FEFO first batch)
                   S_GetSaleItemBatches          FEFO batches of items / given stock ids
                   I_PharmacySaleLines           StoreBills + FTD lines + stock issue + indent bookkeeping
                   S_GetPharmacySaleList         sales / issue register
                   S_GetPharmacySaleDetails      one bill: header, lines, payments
                   S_GetPharmacyRequestCounts    EMR / indent / discharge counts for the inbox cards
                   S_GetPendingPatientIndents    ward indents for the store
                   S_GetPatientIndentItems       one indent with pending qty and stock
                   IU_PatientMedicineIndent      raise a ward indent (IPD tab, later screen)
                   U_RejectPatientIndent         reject a whole indent (OLD U_rejectIndent)
                   S_GetEmrMedicineRequests      OPD visits whose EMR consultation has a medicine list
                   S_GetEmrMedicineRequestItems  medicines of one EMR visit (raw JSON rows)
                   S_GetPharmacyUserRights       CanSellNarcoDrugs + pharmacy discount limit
                   S_GetPatientForSale           patient + admitted IPD visit check used by the save
                   U_PharmacyBillPrint           counts a bill slip print (StoreBills.PrintCount: original / duplicate)

   Visit / bill / transaction / receipt rows are written by the API with the existing procs
   I_PatientVisitDetails (type Store = 3), I_PatientBillDetails, I_FinancialTransactions (OPDSell 5 / IPDIssue 6),
   I_Receipts (@isStore = 1) and I_ReceiptsPaymentModeDetails.

   After running: nothing else (menu row 'medicine-sales' already exists).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. PatientMaster.IsGeneralPatient ---------- */
IF COL_LENGTH('dbo.PatientMaster', 'IsGeneralPatient') IS NULL
    ALTER TABLE dbo.PatientMaster ADD IsGeneralPatient int NOT NULL CONSTRAINT DF_PatientMaster_IsGeneralPatient DEFAULT (0);
GO

/* ---------- 2. StoreBills ---------- */
IF OBJECT_ID('dbo.StoreBills', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StoreBills
    (
        StoreBillId         int             IDENTITY(1,1) NOT NULL,
        HospId              int             NOT NULL,
        BranchId            int             NOT NULL,
        RoleId              int             NOT NULL,          -- store (session role in OLD)
        VisitId             int             NOT NULL,
        PatientId           int             NOT NULL,
        BillId              int             NULL,              -- PatientBillDetails
        FTID                int             NOT NULL,
        ReceiptId           int             NULL,
        BillNo              nvarchar(100)   NOT NULL,
        BillDate            datetime        NOT NULL CONSTRAINT DF_StoreBills_BillDate DEFAULT (GETDATE()),
        PatientTypeId       int             NOT NULL,          -- 1 OPD / walk-in, 2 IPD
        IsWalkIn            int             NOT NULL CONSTRAINT DF_StoreBills_IsWalkIn DEFAULT (0),
        SaleTypeId          int             NOT NULL CONSTRAINT DF_StoreBills_SaleTypeId DEFAULT (1),  -- 1 Sale on MRP, 2 Sale on Pur. Cost
        SourceTypeId        int             NOT NULL CONSTRAINT DF_StoreBills_SourceTypeId DEFAULT (0), -- 0 Counter, 1 EMR, 2 Ward Indent, 3 Discharge
        SourceId            int             NULL,              -- EMR visit id / IndentId
        DoctorId            int             NULL,
        DoctorRef           nvarchar(200)   NULL,
        Witness             nvarchar(200)   NULL,
        Remarks             nvarchar(500)   NULL,
        TotalItems          int             NOT NULL,
        TotalUnits          int             NOT NULL,
        GrossAmount         decimal(18,2)   NOT NULL,
        ItemDiscountAmount  decimal(18,2)   NOT NULL,
        BillDiscountPer     decimal(18,4)   NOT NULL,
        BillDiscountAmount  decimal(18,2)   NOT NULL,
        TaxAmount           decimal(18,2)   NOT NULL,
        RoundOff            decimal(18,2)   NOT NULL,
        NetAmount           decimal(18,2)   NOT NULL,
        PaidAmount          decimal(18,2)   NOT NULL,
        BalanceAmount       decimal(18,2)   NOT NULL,
        IsCancel            int             NOT NULL CONSTRAINT DF_StoreBills_IsCancel DEFAULT (0),
        CancelReason        nvarchar(500)   NULL,
        CreatedBy           int             NOT NULL,
        CreatedOn           datetime        NOT NULL CONSTRAINT DF_StoreBills_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy      int             NULL,
        LastModifiedOn      datetime        NULL,
        IpAddress           nvarchar(100)   NULL,
        CONSTRAINT PK_StoreBills PRIMARY KEY CLUSTERED (StoreBillId)
    );
    CREATE INDEX IX_StoreBills_Branch_Date ON dbo.StoreBills (BranchId, RoleId, BillDate);
    CREATE INDEX IX_StoreBills_FTID ON dbo.StoreBills (FTID);
END
GO

/* 2b. print count for the bill slip: first print = original, later prints = "Reprint #n · Duplicate" (design) */
IF COL_LENGTH('dbo.StoreBills', 'PrintCount') IS NULL
    ALTER TABLE dbo.StoreBills ADD PrintCount int NOT NULL CONSTRAINT DF_StoreBills_PrintCount DEFAULT (0);
IF COL_LENGTH('dbo.StoreBills', 'LastPrintedBy') IS NULL
    ALTER TABLE dbo.StoreBills ADD LastPrintedBy int NULL;
IF COL_LENGTH('dbo.StoreBills', 'LastPrintedOn') IS NULL
    ALTER TABLE dbo.StoreBills ADD LastPrintedOn datetime NULL;
GO

/* ---------- 3. Ward indents ---------- */
IF OBJECT_ID('dbo.PatientMedicineIndent', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientMedicineIndent
    (
        IndentId        int             IDENTITY(1,1) NOT NULL,
        HospId          int             NOT NULL,
        BranchId        int             NOT NULL,
        IndentNo        int             NOT NULL,
        PatientId       int             NOT NULL,
        VisitId         int             NOT NULL,
        ToBranchId      int             NOT NULL,
        ToDeptId        int             NOT NULL,              -- store role
        Status          nvarchar(50)    NOT NULL CONSTRAINT DF_PMI_Status DEFAULT ('Open'),
        StatusId        int             NOT NULL CONSTRAINT DF_PMI_StatusId DEFAULT (1),   -- 1 Open, 2 Close, 3 Rejected
        IndentTypeId    int             NOT NULL CONSTRAINT DF_PMI_IndentTypeId DEFAULT (1),
        InentType       nvarchar(200)   NULL,
        Priority        nvarchar(20)    NOT NULL CONSTRAINT DF_PMI_Priority DEFAULT ('Routine'),  -- design: Routine / Urgent
        Remarks         nvarchar(500)   NULL,
        IsPrinted       int             NOT NULL CONSTRAINT DF_PMI_IsPrinted DEFAULT (0),
        IsSell          int             NOT NULL CONSTRAINT DF_PMI_IsSell DEFAULT (0),
        IsCancel        int             NOT NULL CONSTRAINT DF_PMI_IsCancel DEFAULT (0),
        CancelReseon    nvarchar(500)   NULL,
        CreatedBy       int             NOT NULL,
        CreatedOn       datetime        NOT NULL CONSTRAINT DF_PMI_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int             NULL,
        LastModifiedOn  datetime        NULL,
        IpAddress       nvarchar(100)   NULL,
        UniqueId        nvarchar(100)   NULL,
        CONSTRAINT PK_PatientMedicineIndent PRIMARY KEY CLUSTERED (IndentId)
    );
    CREATE INDEX IX_PMI_Store ON dbo.PatientMedicineIndent (ToBranchId, ToDeptId, StatusId);
END
GO

IF OBJECT_ID('dbo.PatientMedicineIndentItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientMedicineIndentItems
    (
        IndentItemId     int             IDENTITY(1,1) NOT NULL,
        IndentId         int             NOT NULL,
        ItemId           int             NOT NULL,
        RequestedQTY     int             NOT NULL,
        IssuedQTY        int             NOT NULL CONSTRAINT DF_PMII_IssuedQTY DEFAULT (0),
        IssuedItemId     nvarchar(max)   NULL,
        RejectedQTY      int             NOT NULL CONSTRAINT DF_PMII_RejectedQTY DEFAULT (0),
        RejectionRemarks nvarchar(256)   NULL,
        ReturnQty        int             NOT NULL CONSTRAINT DF_PMII_ReturnQty DEFAULT (0),
        Dose             nvarchar(100)   NULL,
        Route            nvarchar(100)   NULL,
        Times            nvarchar(100)   NULL,
        Duration         nvarchar(100)   NULL,
        Meal             nvarchar(100)   NULL,
        Remarks          nvarchar(max)   NULL,
        FTDId            nvarchar(max)   NULL,
        CreatedBy        int             NOT NULL,
        CreatedOn        datetime        NOT NULL CONSTRAINT DF_PMII_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy   int             NULL,
        LastModifiedOn   datetime        NULL,
        IpAddress        nvarchar(100)   NULL,
        UniqueId         nvarchar(100)   NULL,
        CONSTRAINT PK_PatientMedicineIndentItems PRIMARY KEY CLUSTERED (IndentItemId)
    );
    CREATE INDEX IX_PMII_Indent ON dbo.PatientMedicineIndentItems (IndentId);
END
GO

/* ---------- 4. Right CanSellNarcoDrugs (OLD) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.UserRightsMaster WHERE Name = 'CanSellNarcoDrugs')
    INSERT INTO dbo.UserRightsMaster (HospId, Name, DisplayName, Description, IsActive, CreatedOn)
    VALUES (1, 'CanSellNarcoDrugs', 'Sell Narcotic Drugs', 'User can sell / issue items whose drug category is narcotic (Medicine Sale / Issue).', 1, GETDATE());
GO

/* ---------- 5. Sequences (all branches, all roles) - only when the type has no mapping yet ---------- */
DECLARE @seq TABLE (TypeId int, TypeName nvarchar(100), Name nvarchar(50), Prefix nvarchar(10), FYFormatId int, Len int, Preview nvarchar(50));
INSERT INTO @seq VALUES (5,  'Store-Bill(Issue)',    'PH',  'PH',  1, 5, 'PH/26-27/00001'),
                        (6,  'Store-Receipt(Issue)', 'PHR', 'PHR', 1, 5, 'PHR/26-27/00001'),
                        (26, 'WalkIn UHID',          'WI',  'WI',  0, 8, 'WI/00000001');
DECLARE @t int, @tn nvarchar(100), @nm nvarchar(50), @pf nvarchar(10), @fy int, @ln int, @pv nvarchar(50), @sid int;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT TypeId, TypeName, Name, Prefix, FYFormatId, Len, Preview FROM @seq;
OPEN c; FETCH NEXT FROM c INTO @t, @tn, @nm, @pf, @fy, @ln, @pv;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.BranchSequenceMapping WHERE TypeId = @t)
    BEGIN
        SELECT TOP 1 @sid = SequenceId FROM dbo.SequenceMaster WHERE TypeId = @t AND Name = @nm;
        IF @sid IS NULL
        BEGIN
            INSERT INTO dbo.SequenceMaster (HospId, Name, TypeId, TypeName, Prefix, FirstSeprator, FYFormatId, FYFormat,
                                            SecondSeprator, Length, Preview, IsActive, CreatedBy, CreatedOn, IpAddress, UniqueId)
            VALUES (1, @nm, @t, @tn, @pf, '/', @fy, CASE WHEN @fy = 1 THEN '2' ELSE NULL END, CASE WHEN @fy = 1 THEN '/' ELSE '' END, @ln, @pv, 1, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
            SET @sid = SCOPE_IDENTITY();
        END
        INSERT INTO dbo.BranchSequenceMapping (BranchId, RoleId, TypeId, SequenceId, CreatedBy, CreatedOn, IpAddress, UniqueId)
        VALUES (0, 0, @t, @sid, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
    END
    SET @sid = NULL;
    FETCH NEXT FROM c INTO @t, @tn, @nm, @pf, @fy, @ln, @pv;
END
CLOSE c; DEALLOCATE c;

/* walk-in UHID has no financial year: one separator only (WI/00000001), like the main UHID sequence */
UPDATE dbo.SequenceMaster SET SecondSeprator = '', LastModifiedBy = 1, LastModifiedOn = GETDATE()
WHERE TypeId = 26 AND Name = 'WI' AND FYFormatId = 0 AND ISNULL(SecondSeprator, '') <> '';
GO

/* ---------- 6a. Walk-in patient (OLD IU_PatientMaster @isGeneralPatient = 1 -> own UHID sequence) ---------- */
CREATE OR ALTER PROCEDURE dbo.I_PharmacyWalkInPatient
    @hospId int, @branchId int, @title nvarchar(20), @firstName nvarchar(100),
    @ageYears int = 0, @ageMonths int = 0, @ageDays int = 0, @gender nvarchar(20) = NULL,
    @contactNumber nvarchar(20) = NULL, @address nvarchar(1000) = NULL,
    @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @uhid nvarchar(50), @countryId int, @stateId int, @corporateId int;
    DECLARE @t TABLE (v nvarchar(100));
    INSERT INTO @t EXEC dbo.getBranchSequenceNumber @branchId, 0, 26, @uhid OUTPUT;
    IF ISNULL(@uhid, '') = '' BEGIN SET @Result = -3; SELECT @Result; RETURN; END

    SELECT TOP 1 @countryId = DefaultCountryId, @stateId = DefaultStateId, @corporateId = DefaultCorporateId
    FROM dbo.BranchMaster WHERE BranchId = @branchId;

    DECLARE @dob date = DATEADD(DAY, -ISNULL(@ageDays, 0), DATEADD(MONTH, -ISNULL(@ageMonths, 0), DATEADD(YEAR, -ISNULL(@ageYears, 0), CAST(GETDATE() AS date))));

    INSERT INTO dbo.PatientMaster (HospId, BranchId, UHID, Title, FirstName, PatientName, AgeYears, AgeMonths, AgeDays, Age, DOB,
                                   Gender, ContactNumber, Address, CountryId, StateId, InsuranceCompanyId, CorporateId,
                                   CreatedBy, IpAddress, IsGeneralPatient)
    VALUES (@hospId, @branchId, @uhid, UPPER(@title), UPPER(LTRIM(RTRIM(@firstName))),
            UPPER(LTRIM(RTRIM(CONCAT(@title, ' ', LTRIM(RTRIM(@firstName)))))),
            @ageYears, @ageMonths, @ageDays, CONCAT(@ageYears, 'Y ', @ageMonths, 'M ', @ageDays, 'D'), @dob,
            UPPER(@gender), @contactNumber, UPPER(@address), @countryId, @stateId, 0, ISNULL(@corporateId, 1),
            @userId, @ipAddress, 1);
    SET @Result = SCOPE_IDENTITY();
    SELECT @Result;
END
GO

/* ---------- 6b. Patient search: 1 = OPD (registered patients), 2 = IPD (admitted, not discharged) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_SearchPatientForSale
    @hospId int, @branchId int, @patientTypeId int, @search nvarchar(100) = NULL, @top int = 30
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');
    IF @patientTypeId = 2
    BEGIN
        SELECT TOP (@top) pm.PatientId, pm.UHID, pm.PatientName, pm.Age, pm.Gender, pm.ContactNumber,
               pvd.VisitId, pvd.VisitNo AS IpdNo, pvd.AdmissionDate, pvd.CorporateId, cm.CorporateName,
               pvd.DoctorId, dm.CompleteName AS DoctorName,
               CONCAT(wnm.WardName, ' / ', bm.RoomName, ' / ', bm.BedNo) AS WardBed,
               ISNULL(pm.IsGeneralPatient, 0) AS IsGeneralPatient
        FROM dbo.PatientVisitDetails pvd
        INNER JOIN dbo.PatientMaster pm ON pm.PatientId = pvd.PatientId
        LEFT JOIN dbo.CorporateMaster cm ON cm.CorporateId = pvd.CorporateId
        LEFT JOIN dbo.DoctorMaster dm ON dm.DoctorId = pvd.DoctorId
        LEFT JOIN dbo.BedMaster bm ON bm.BedId = pvd.BedId
        LEFT JOIN dbo.WardNameMaster wnm ON wnm.WardNameId = bm.WardNameId
        WHERE pvd.HospId = @hospId AND pvd.BranchId = @branchId AND pvd.TypeId = 2
          AND ISNULL(pvd.IsCancel, 0) = 0 AND ISNULL(pvd.IsDischarged, 0) = 0 AND pvd.StatusId = 1
          AND (@search IS NULL OR pm.UHID LIKE '%' + @search + '%' OR pm.PatientName LIKE '%' + @search + '%'
               OR pm.ContactNumber LIKE '%' + @search + '%' OR pvd.VisitNo LIKE '%' + @search + '%'
               OR wnm.WardName LIKE '%' + @search + '%' OR bm.BedNo LIKE '%' + @search + '%')
        ORDER BY pvd.VisitId DESC;
    END
    ELSE
    BEGIN
        IF @search IS NULL OR LEN(@search) < 2 BEGIN SELECT TOP 0 1 AS PatientId; RETURN; END
        SELECT TOP (@top) pm.PatientId, pm.UHID, pm.PatientName, pm.Age, pm.Gender, pm.ContactNumber,
               CAST(NULL AS int) AS VisitId, CAST(NULL AS nvarchar(50)) AS IpdNo, CAST(NULL AS date) AS AdmissionDate,
               ISNULL(pm.CorporateId, 1) AS CorporateId, cm.CorporateName,
               lastVisit.DoctorId, dm.CompleteName AS DoctorName, CAST(NULL AS nvarchar(200)) AS WardBed,
               ISNULL(pm.IsGeneralPatient, 0) AS IsGeneralPatient
        FROM dbo.PatientMaster pm
        LEFT JOIN dbo.CorporateMaster cm ON cm.CorporateId = pm.CorporateId
        OUTER APPLY (SELECT TOP 1 v.DoctorId FROM dbo.PatientVisitDetails v
                     WHERE v.PatientId = pm.PatientId AND v.TypeId = 1 AND ISNULL(v.DoctorId, 0) > 0 ORDER BY v.VisitId DESC) lastVisit
        LEFT JOIN dbo.DoctorMaster dm ON dm.DoctorId = lastVisit.DoctorId
        WHERE pm.HospId = @hospId
          AND (pm.UHID LIKE '%' + @search + '%' OR pm.PatientName LIKE '%' + @search + '%' OR pm.ContactNumber LIKE '%' + @search + '%')
        ORDER BY pm.PatientId DESC;
    END
END
GO

/* ---------- 6c. Item search (OLD S_SearchMadicineItem: 1 name, 2 barcode, 3 salt) - one row per item, FEFO first batch ---------- */
CREATE OR ALTER PROCEDURE dbo.S_SearchItemForSale
    @hospId int, @branchId int, @roleId int, @search nvarchar(100), @searchType int = 1, @top int = 30
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = LTRIM(RTRIM(ISNULL(@search, '')));
    DECLARE @today date = CAST(GETDATE() AS date);

    ;WITH lots AS
    (
        SELECT st.StockId, st.ItemId, st.BatchNo, st.ExpiryDate, st.CurrentStock, st.MRP, st.UnitCost, st.GSTPercentage,
               ROW_NUMBER() OVER (PARTITION BY st.ItemId ORDER BY st.ExpiryDate, st.StockId) AS rn,
               SUM(st.CurrentStock) OVER (PARTITION BY st.ItemId) AS TotalStock,
               COUNT(*) OVER (PARTITION BY st.ItemId) AS BatchCount
        FROM dbo.Stock st
        INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = st.ItemId
        WHERE st.HospId = @hospId AND st.BranchId = @branchId AND st.RoleId = @roleId AND st.IsActive = 1
          AND st.CurrentStock > 0 AND st.ExpiryDate > @today
          AND (   (@searchType = 1 AND (sim.Name LIKE '%' + @search + '%' OR st.ItemName LIKE '%' + @search + '%'))
               OR (@searchType = 2 AND (st.Barcode = @search OR sim.Barcode = @search OR sim.Code = @search))
               OR (@searchType = 3 AND sim.ItemDiscription LIKE '%' + @search + '%'))
    )
    SELECT TOP (@top)
        l.ItemId, sim.Name AS ItemName, ISNULL(sim.ItemDiscription, '') AS SaltName, ISNULL(mm.ManufacturerName, '') AS ManufacturerName,
        ISNULL(sim.SaleUnitName, '') AS SaleUnitName, ISNULL(dcm.DrugName, '') AS DrugCategory, ISNULL(dcm.isNarcoticDrug, 0) AS IsNarcotic,
        ISNULL(dcm.ColorPicker, '') AS ColorPicker, ISNULL(sim.StatusId, 0) AS HoldStatusId, ISNULL(sim.StatusType, '') AS HoldStatus,
        ISNULL(sim.MedicineHoldRemorks, '') AS HoldRemarks, ISNULL(sim.CorporateIds, '') AS CorporateIds,
        l.TotalStock, l.BatchCount, l.StockId, l.BatchNo, l.ExpiryDate, l.MRP, l.UnitCost, l.GSTPercentage
    FROM lots l
    INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = l.ItemId
    LEFT JOIN dbo.ManufacturerMaster mm ON mm.ManufacturerId = sim.ManufacturerId
    LEFT JOIN dbo.DrugCategoryMasters dcm ON dcm.DrugId = sim.DrugCategoryId
    WHERE l.rn = 1
    ORDER BY CASE WHEN sim.Name LIKE @search + '%' THEN 0 ELSE 1 END, sim.Name;
END
GO

/* ---------- 6d. FEFO batches (OLD S_GetStockByItemIds): by item ids, or the exact stock ids being saved ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetSaleItemBatches
    @hospId int, @branchId int, @roleId int, @itemIds nvarchar(max) = NULL, @stockIds nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today date = CAST(GETDATE() AS date);
    SELECT st.StockId, st.ItemId, sim.Name AS ItemName, ISNULL(sim.Code, '') AS ItemCode, sim.SubSubCategoryId,
           st.BatchNo, st.ExpiryDate, st.CurrentStock, st.MRP, ISNULL(st.UnitCost, 0) AS UnitCost, ISNULL(st.GSTPercentage, 0) AS GSTPercentage,
           st.Packing, st.IsActive, st.BranchId, st.RoleId,
           ISNULL(dcm.isNarcoticDrug, 0) AS IsNarcotic, ISNULL(sim.CorporateIds, '') AS CorporateIds,
           ISNULL(sim.StatusId, 0) AS HoldStatusId, ISNULL(sim.StatusType, '') AS HoldStatus,
           CASE WHEN st.ExpiryDate > @today THEN 0 ELSE 1 END AS IsExpired
    FROM dbo.Stock st
    INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = st.ItemId
    LEFT JOIN dbo.DrugCategoryMasters dcm ON dcm.DrugId = sim.DrugCategoryId
    WHERE st.HospId = @hospId AND st.BranchId = @branchId AND st.RoleId = @roleId
      AND (   (@stockIds IS NOT NULL AND st.StockId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(@stockIds, ',')))
           OR (@stockIds IS NULL AND st.ItemId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(ISNULL(@itemIds, ''), ','))
               AND st.IsActive = 1 AND st.CurrentStock > 0 AND st.ExpiryDate > @today))
    ORDER BY st.ItemId, st.ExpiryDate, st.StockId;
END
GO

/* ---------- 6e. Bill lines in one call (inside the API transaction) ----------
   @Result > 0  StoreBillId
          -n    line n: stock not available (not active / expired / short)            (OLD "Stock not available for item")
          -(10000+n) line n: indent item over-issued (issue + reject > requested)     (OLD "Issue and reject quantity ...")
          -9000 indent is not open for this store
          -9001 reject line points to another indent / over-rejects
          -3    bill number sequence missing                                            */
CREATE OR ALTER PROCEDURE dbo.I_PharmacySaleLines
    @hospId int, @branchId int, @roleId int, @visitId int, @patientId int, @billId int = NULL, @ftid int, @receiptId int = NULL,
    @corporateId int = NULL, @doctorId int = NULL, @patientTypeId int, @isWalkIn int = 0, @saleTypeId int = 1,
    @sourceTypeId int = 0, @sourceId int = NULL, @doctorRef nvarchar(200) = NULL, @witness nvarchar(200) = NULL, @remarks nvarchar(500) = NULL,
    @totalItems int, @totalUnits int, @grossAmount decimal(18,2), @itemDiscountAmount decimal(18,2), @billDiscountPer decimal(18,4),
    @billDiscountAmount decimal(18,2), @taxAmount decimal(18,2), @roundOff decimal(18,2), @netAmount decimal(18,2),
    @paidAmount decimal(18,2), @itemsJson nvarchar(max), @rejectsJson nvarchar(max) = NULL,
    @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today date = CAST(GETDATE() AS date);

    DECLARE @lines TABLE (LineNum int, StockId int, ItemId int, ItemName nvarchar(800), SubSubCategoryId int, ItemCode nvarchar(256),
                          Qty int, Rate decimal(18,6), GrossAmt decimal(18,6), DiscPer decimal(18,6), DiscAmt decimal(18,6),
                          GstPer decimal(18,6), TaxAmt decimal(18,6), NetAmt decimal(18,6), IndentItemId int);
    INSERT INTO @lines
    SELECT LineNum, StockId, ItemId, ItemName, SubSubCategoryId, ItemCode, Qty, Rate, GrossAmt, DiscPer, DiscAmt, GstPer, TaxAmt, NetAmt, NULLIF(IndentItemId, 0)
    FROM OPENJSON(@itemsJson) WITH (LineNum int '$.lineNo', StockId int '$.stockId', ItemId int '$.itemId', ItemName nvarchar(800) '$.itemName',
                                    SubSubCategoryId int '$.subSubCategoryId', ItemCode nvarchar(256) '$.itemCode', Qty int '$.qty',
                                    Rate decimal(18,6) '$.rate', GrossAmt decimal(18,6) '$.grossAmt', DiscPer decimal(18,6) '$.discPer',
                                    DiscAmt decimal(18,6) '$.discAmt', GstPer decimal(18,6) '$.gstPer', TaxAmt decimal(18,6) '$.taxAmt',
                                    NetAmt decimal(18,6) '$.netAmt', IndentItemId int '$.indentItemId');

    IF @sourceTypeId = 2 AND NOT EXISTS (SELECT 1 FROM dbo.PatientMedicineIndent WHERE IndentId = @sourceId AND ToBranchId = @branchId
                                         AND ToDeptId = @roleId AND StatusId = 1 AND IsCancel = 0 AND VisitId = @visitId)
    BEGIN SET @Result = -9000; SELECT @Result; RETURN; END

    /* bill number: sequence 5 (getBranchSequenceNumber also SELECTs for type 5, so capture that row) */
    DECLARE @billNo nvarchar(100);
    DECLARE @seq TABLE (v nvarchar(100));
    INSERT INTO @seq EXEC dbo.getBranchSequenceNumber @branchId, @roleId, 5, @billNo OUTPUT;
    IF ISNULL(@billNo, '') = '' BEGIN SET @Result = -3; SELECT @Result; RETURN; END

    /* per line: stock issue (atomic check + decrement, OLD S_CheckStockAvailableQuantityByStockId + U_UpdateStockQuantity) */
    DECLARE @n int, @stockId int, @qty int, @indentItemId int, @ftdId int;
    DECLARE lc CURSOR LOCAL FAST_FORWARD FOR SELECT LineNum, StockId, Qty, IndentItemId FROM @lines ORDER BY LineNum;
    OPEN lc; FETCH NEXT FROM lc INTO @n, @stockId, @qty, @indentItemId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        UPDATE dbo.Stock
        SET CurrentStock = CurrentStock - @qty, IssuedQTY = ISNULL(IssuedQTY, 0) + @qty, LastModifiedBy = @userId, LastModifiedOn = GETDATE()
        WHERE StockId = @stockId AND BranchId = @branchId AND RoleId = @roleId AND IsActive = 1
          AND CurrentStock >= @qty AND ExpiryDate > @today AND @qty > 0;
        IF @@ROWCOUNT = 0 BEGIN CLOSE lc; DEALLOCATE lc; SET @Result = -@n; SELECT @Result; RETURN; END

        INSERT INTO dbo.FinancialTransactionDetails
            (HospId, BranchId, FTID, VisitId, BillId, PatientId, ServiceItemId, SubSubCategoryId, ServiceName, ServiceCode, DoctorId, CorporateId,
             Rate, Qty, GrossAmt, DiscPer, DiscAmt, TotalTaxPer, TotalTaxAmt, NetAmt, IsCorporateNonPayable, IsUnderPackage, DiscountReason,
             RateListId, stockId, CreatedBy, IpAddress, RefundedFromFTDID, PackageId, BillingDate,
             S_Rate, S_Qty, S_GrossAmt, S_DiscPer, S_DiscAmt, S_TotalTaxPer, S_TotalTaxAmt, S_NetAmt, GSTPer, GSTAmt)
        SELECT @hospId, @branchId, @ftid, @visitId, @billId, @patientId, l.ItemId, l.SubSubCategoryId, l.ItemName, l.ItemCode, @doctorId, @corporateId,
               l.Rate, l.Qty, l.GrossAmt, l.DiscPer, l.DiscAmt, l.GstPer, l.TaxAmt, l.NetAmt, 0, 0, NULL,
               0, l.StockId, @userId, @ipAddress, 0, 0, @today,
               l.Rate, l.Qty, l.GrossAmt, l.DiscPer, l.DiscAmt, l.GstPer, l.TaxAmt, l.NetAmt, l.GstPer, l.TaxAmt
        FROM @lines l WHERE l.LineNum = @n;
        SET @ftdId = SCOPE_IDENTITY();

        IF @indentItemId IS NOT NULL
        BEGIN
            UPDATE i SET IssuedQTY = i.IssuedQTY + @qty,
                         FTDId = CONCAT(ISNULL(i.FTDId + ',', ''), @ftdId),
                         LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
            FROM dbo.PatientMedicineIndentItems i
            WHERE i.IndentItemId = @indentItemId AND i.IndentId = @sourceId
              AND i.IssuedQTY + i.RejectedQTY + @qty <= i.RequestedQTY;
            IF @@ROWCOUNT = 0 BEGIN CLOSE lc; DEALLOCATE lc; SET @Result = -(10000 + @n); SELECT @Result; RETURN; END
        END
        FETCH NEXT FROM lc INTO @n, @stockId, @qty, @indentItemId;
    END
    CLOSE lc; DEALLOCATE lc;

    /* rejected indent items (OLD U_rejectIndentItem) + indent status (OLD U_UpdateIndent) */
    IF @sourceTypeId = 2
    BEGIN
        IF @rejectsJson IS NOT NULL
        BEGIN
            DECLARE @rej TABLE (IndentItemId int, Qty int, Reason nvarchar(256));
            INSERT INTO @rej SELECT IndentItemId, Qty, Reason FROM OPENJSON(@rejectsJson)
                WITH (IndentItemId int '$.indentItemId', Qty int '$.qty', Reason nvarchar(256) '$.reason');
            IF EXISTS (SELECT 1 FROM @rej r LEFT JOIN dbo.PatientMedicineIndentItems i ON i.IndentItemId = r.IndentItemId AND i.IndentId = @sourceId
                       WHERE i.IndentItemId IS NULL OR r.Qty <= 0 OR i.IssuedQTY + i.RejectedQTY + r.Qty > i.RequestedQTY)
            BEGIN SET @Result = -9001; SELECT @Result; RETURN; END
            UPDATE i SET RejectedQTY = i.RejectedQTY + r.Qty, RejectionRemarks = r.Reason,
                         LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
            FROM dbo.PatientMedicineIndentItems i INNER JOIN @rej r ON r.IndentItemId = i.IndentItemId;
        END
        UPDATE pmi SET StatusId = CASE WHEN s.Req > s.Done THEN 1 ELSE 2 END,
                       Status   = CASE WHEN s.Req > s.Done THEN 'Open' ELSE 'Close' END,
                       IsSell = 1, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        FROM dbo.PatientMedicineIndent pmi
        CROSS APPLY (SELECT SUM(RequestedQTY) Req, SUM(IssuedQTY + RejectedQTY) Done FROM dbo.PatientMedicineIndentItems WHERE IndentId = pmi.IndentId) s
        WHERE pmi.IndentId = @sourceId;
    END

    INSERT INTO dbo.StoreBills (HospId, BranchId, RoleId, VisitId, PatientId, BillId, FTID, ReceiptId, BillNo, PatientTypeId, IsWalkIn, SaleTypeId,
                                SourceTypeId, SourceId, DoctorId, DoctorRef, Witness, Remarks, TotalItems, TotalUnits, GrossAmount, ItemDiscountAmount,
                                BillDiscountPer, BillDiscountAmount, TaxAmount, RoundOff, NetAmount, PaidAmount, BalanceAmount, CreatedBy, IpAddress)
    VALUES (@hospId, @branchId, @roleId, @visitId, @patientId, @billId, @ftid, NULLIF(@receiptId, 0), @billNo, @patientTypeId, @isWalkIn, @saleTypeId,
            @sourceTypeId, @sourceId, NULLIF(@doctorId, 0), NULLIF(LTRIM(RTRIM(@doctorRef)), ''), NULLIF(LTRIM(RTRIM(@witness)), ''), NULLIF(LTRIM(RTRIM(@remarks)), ''),
            @totalItems, @totalUnits, @grossAmount, @itemDiscountAmount, @billDiscountPer, @billDiscountAmount, @taxAmount, @roundOff, @netAmount,
            @paidAmount, @netAmount - @paidAmount, @userId, @ipAddress);
    SET @Result = SCOPE_IDENTITY();
    SELECT @Result;
END
GO

/* ---------- 6f. Sales / Issue register ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPharmacySaleList
    @hospId int, @branchId int, @roleId int = NULL, @fromDate date, @toDate date, @patientTypeId int = NULL, @search nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');
    SELECT sb.StoreBillId, sb.BillNo, sb.BillDate, sb.PatientTypeId, CASE sb.PatientTypeId WHEN 2 THEN 'IPD' ELSE 'OPD' END AS PatientType,
           sb.IsWalkIn, sb.SourceTypeId,
           CASE sb.SourceTypeId WHEN 1 THEN 'EMR' WHEN 2 THEN 'Indent' WHEN 3 THEN 'Discharge' ELSE 'Counter' END AS Source,
           sb.PatientId, pm.UHID, pm.PatientName, pvd.VisitNo, rm.RoleName AS StoreName,
           sb.TotalItems, sb.TotalUnits, sb.NetAmount, sb.PaidAmount, sb.BalanceAmount, sb.IsCancel,
           ISNULL(r.ReceiptNo, '') AS ReceiptNo, um.Name AS CreatedByName, sb.PrintCount
    FROM dbo.StoreBills sb
    INNER JOIN dbo.PatientMaster pm ON pm.PatientId = sb.PatientId
    LEFT JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = sb.VisitId
    LEFT JOIN dbo.RoleMaster rm ON rm.RoleId = sb.RoleId
    LEFT JOIN dbo.Receipts r ON r.ReceiptID = sb.ReceiptId
    LEFT JOIN dbo.UserMaster um ON um.Id = sb.CreatedBy
    WHERE sb.HospId = @hospId AND sb.BranchId = @branchId AND (@roleId IS NULL OR sb.RoleId = @roleId)
      AND CAST(sb.BillDate AS date) BETWEEN @fromDate AND @toDate
      AND (@patientTypeId IS NULL OR sb.PatientTypeId = @patientTypeId)
      AND (@search IS NULL OR sb.BillNo LIKE '%' + @search + '%' OR pm.UHID LIKE '%' + @search + '%'
           OR pm.PatientName LIKE '%' + @search + '%' OR pvd.VisitNo LIKE '%' + @search + '%')
    ORDER BY sb.StoreBillId DESC;
END
GO

/* ---------- 6g. One bill: header, lines, payments ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPharmacySaleDetails
    @hospId int, @storeBillId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT sb.*, CASE sb.PatientTypeId WHEN 2 THEN 'IPD' ELSE 'OPD' END AS PatientType,
           pm.UHID, pm.PatientName, pm.Age, pm.Gender, pm.ContactNumber, pvd.VisitNo, rm.RoleName AS StoreName,
           cm.CorporateName, dm.CompleteName AS DoctorName, ISNULL(r.ReceiptNo, '') AS ReceiptNo, um.Name AS CreatedByName,
           -- bill slip (2026-11-23): ward / bed of the admission, branch letterhead, last print
           CASE WHEN sb.PatientTypeId = 2 AND bed.BedId IS NOT NULL THEN CONCAT(wnm.WardName, ' / ', bed.BedNo) ELSE '' END AS WardBed,
           ISNULL(bm.BranchName, '') AS BranchName, ISNULL(bm.Address, '') AS BranchAddress,
           ISNULL(bm.ContactNo1, '') AS BranchPhone, ISNULL(bm.Email, '') AS BranchEmail,
           ISNULL(lp.Name, '') AS LastPrintedByName
    FROM dbo.StoreBills sb
    INNER JOIN dbo.PatientMaster pm ON pm.PatientId = sb.PatientId
    LEFT JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = sb.VisitId
    LEFT JOIN dbo.RoleMaster rm ON rm.RoleId = sb.RoleId
    LEFT JOIN dbo.CorporateMaster cm ON cm.CorporateId = pvd.CorporateId
    LEFT JOIN dbo.DoctorMaster dm ON dm.DoctorId = sb.DoctorId
    LEFT JOIN dbo.Receipts r ON r.ReceiptID = sb.ReceiptId
    LEFT JOIN dbo.UserMaster um ON um.Id = sb.CreatedBy
    LEFT JOIN dbo.UserMaster lp ON lp.Id = sb.LastPrintedBy
    LEFT JOIN dbo.BedMaster bed ON bed.BedId = pvd.BedId
    LEFT JOIN dbo.WardNameMaster wnm ON wnm.WardNameId = bed.WardNameId
    LEFT JOIN dbo.BranchMaster bm ON bm.BranchId = sb.BranchId
    WHERE sb.HospId = @hospId AND sb.StoreBillId = @storeBillId;

    SELECT ftd.FTDId, ftd.ServiceItemId AS ItemId, ftd.ServiceName AS ItemName, st.BatchNo, st.ExpiryDate, ftd.Qty, ftd.Rate,
           ftd.GrossAmt, ftd.DiscPer, ftd.DiscAmt, ftd.TotalTaxPer AS GstPer, ftd.TotalTaxAmt AS TaxAmt, ftd.NetAmt, ftd.stockId AS StockId,
           ISNULL(dcm.isNarcoticDrug, 0) AS IsNarcotic
    FROM dbo.StoreBills sb
    INNER JOIN dbo.FinancialTransactionDetails ftd ON ftd.FTID = sb.FTID
    LEFT JOIN dbo.Stock st ON st.StockId = ftd.stockId
    LEFT JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = ftd.ServiceItemId
    LEFT JOIN dbo.DrugCategoryMasters dcm ON dcm.DrugId = sim.DrugCategoryId
    WHERE sb.HospId = @hospId AND sb.StoreBillId = @storeBillId
    ORDER BY ftd.FTDId;

    SELECT rpmd.PaymentModeId, pmm.PaymentModeName AS PaymentMode, rpmd.Amount, ISNULL(rpmd.ReferenceNo, '') AS ReferenceNo
    FROM dbo.StoreBills sb
    INNER JOIN dbo.ReceiptsPaymentModeDetails rpmd ON rpmd.ReceiptID = sb.ReceiptId
    LEFT JOIN dbo.PaymentModeMaster pmm ON pmm.PaymentModeId = rpmd.PaymentModeId
    WHERE sb.HospId = @hospId AND sb.StoreBillId = @storeBillId;
END
GO

/* ---------- 6h. Request inbox counts ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPharmacyRequestCounts
    @hospId int, @branchId int, @roleId int, @emrFromDate date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(DISTINCT h.VisitId)
           FROM dbo.PatientDoctorHeadersData h
           INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = h.VisitId AND pvd.TypeId = 1 AND pvd.BranchId = @branchId
           WHERE ISJSON(h.HeaderValue) = 1 AND JSON_VALUE(h.HeaderValue, '$[0].medicineName') IS NOT NULL
             AND CAST(h.CreatedOn AS date) >= @emrFromDate
             AND NOT EXISTS (SELECT 1 FROM dbo.StoreBills sb WHERE sb.SourceTypeId = 1 AND sb.SourceId = h.VisitId AND sb.IsCancel = 0)) AS EmrCount,
        (SELECT COUNT(*) FROM dbo.PatientMedicineIndent WHERE ToBranchId = @branchId AND ToDeptId = @roleId AND StatusId = 1 AND IsCancel = 0) AS IndentCount,
        (SELECT COUNT(*) FROM dbo.PatientMedicineIndent WHERE ToBranchId = @branchId AND ToDeptId = @roleId AND StatusId = 1 AND IsCancel = 0 AND Priority = 'Urgent') AS IndentUrgentCount,
        0 AS DischargeCount;   -- 2026-11-24_DischargeMedication.sql replaces this proc with a real count: re-run that script after this one
END
GO

/* ---------- 6i. Ward indents for the store (OLD S_getPendingIndentDetails) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPendingPatientIndents
    @hospId int, @branchId int, @roleId int, @statusId int = 1, @search nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');
    SELECT pmi.IndentId, pmi.IndentNo, pmi.Priority, pmi.StatusId, pmi.Status, pmi.CreatedOn, pmi.Remarks,
           pmi.PatientId, pm.UHID, pm.PatientName, pm.Age, pm.Gender, pmi.VisitId, pvd.VisitNo AS IpdNo,
           CONCAT(wnm.WardName, ' / ', bm.BedNo) AS WardBed, dm.CompleteName AS DoctorName, ISNULL(dm.Department, '') AS Department, um.Name AS RaisedBy,
           CASE WHEN pmi.StatusId = 1 AND EXISTS (SELECT 1 FROM dbo.PatientMedicineIndentItems x WHERE x.IndentId = pmi.IndentId AND x.IssuedQTY + x.RejectedQTY > 0)
                THEN 1 ELSE 0 END AS IsPartial,
           (SELECT COUNT(*) FROM dbo.PatientMedicineIndentItems i WHERE i.IndentId = pmi.IndentId) AS ItemCount
    FROM dbo.PatientMedicineIndent pmi
    INNER JOIN dbo.PatientMaster pm ON pm.PatientId = pmi.PatientId
    INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = pmi.VisitId
    LEFT JOIN dbo.BedMaster bm ON bm.BedId = pvd.BedId
    LEFT JOIN dbo.WardNameMaster wnm ON wnm.WardNameId = bm.WardNameId
    LEFT JOIN dbo.DoctorMaster dm ON dm.DoctorId = pvd.DoctorId
    LEFT JOIN dbo.UserMaster um ON um.Id = pmi.CreatedBy
    WHERE pmi.HospId = @hospId AND pmi.ToBranchId = @branchId AND pmi.ToDeptId = @roleId AND pmi.IsCancel = 0
      AND (@statusId IS NULL OR pmi.StatusId = @statusId)
      AND (@search IS NULL OR pm.UHID LIKE '%' + @search + '%' OR pm.PatientName LIKE '%' + @search + '%'
           OR pvd.VisitNo LIKE '%' + @search + '%' OR CAST(pmi.IndentNo AS nvarchar(20)) LIKE '%' + @search + '%'
           OR wnm.WardName LIKE '%' + @search + '%' OR dm.CompleteName LIKE '%' + @search + '%')
    ORDER BY CASE WHEN pmi.Priority = 'Urgent' THEN 0 ELSE 1 END, pmi.IndentId DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetPatientIndentItems
    @hospId int, @branchId int, @roleId int, @indentId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today date = CAST(GETDATE() AS date);
    SELECT i.IndentItemId, i.IndentId, i.ItemId, sim.Name AS ItemName, ISNULL(sim.ItemDiscription, '') AS SaltName, ISNULL(df.DosageFormName, '') AS DosageForm,
           i.RequestedQTY, i.IssuedQTY, i.RejectedQTY, i.RequestedQTY - i.IssuedQTY - i.RejectedQTY AS PendingQty,
           ISNULL(i.Dose, '') AS Dose, ISNULL(i.Times, '') AS Times, ISNULL(i.Duration, '') AS Duration, ISNULL(i.Route, '') AS Route,
           ISNULL(i.Remarks, '') AS Remarks, ISNULL(i.RejectionRemarks, '') AS RejectionRemarks,
           ISNULL((SELECT SUM(st.CurrentStock) FROM dbo.Stock st WHERE st.ItemId = i.ItemId AND st.BranchId = @branchId AND st.RoleId = @roleId
                   AND st.IsActive = 1 AND st.CurrentStock > 0 AND st.ExpiryDate > @today), 0) AS InStock
    FROM dbo.PatientMedicineIndentItems i
    INNER JOIN dbo.PatientMedicineIndent pmi ON pmi.IndentId = i.IndentId
    INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = i.ItemId
    LEFT JOIN dbo.DosageFormMaster df ON df.DosageFormId = sim.DosageFormId
    WHERE pmi.HospId = @hospId AND i.IndentId = @indentId AND pmi.ToBranchId = @branchId AND pmi.ToDeptId = @roleId
    ORDER BY i.IndentItemId;
END
GO

/* ---------- 6j. Raise a ward indent (OLD I_PatientMedicineIndent + I_PatientMedicineIndentItems); items JSON ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_PatientMedicineIndent
    @hospId int, @branchId int, @patientId int, @visitId int, @toBranchId int, @toDeptId int,
    @priority nvarchar(20) = 'Routine', @remarks nvarchar(500) = NULL, @itemsJson nvarchar(max),
    @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PatientVisitDetails WHERE VisitId = @visitId AND PatientId = @patientId AND TypeId = 2
                   AND ISNULL(IsDischarged, 0) = 0 AND ISNULL(IsCancel, 0) = 0)
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    DECLARE @indentNo int = ISNULL((SELECT MAX(IndentNo) FROM dbo.PatientMedicineIndent WITH (UPDLOCK, HOLDLOCK) WHERE ToBranchId = @toBranchId), 0) + 1;
    INSERT INTO dbo.PatientMedicineIndent (HospId, BranchId, IndentNo, PatientId, VisitId, ToBranchId, ToDeptId, Priority, Remarks, CreatedBy, IpAddress, UniqueId)
    VALUES (@hospId, @branchId, @indentNo, @patientId, @visitId, @toBranchId, @toDeptId,
            CASE WHEN @priority = 'Urgent' THEN 'Urgent' ELSE 'Routine' END, @remarks, @userId, @ipAddress, CONVERT(nvarchar(100), NEWID()));
    SET @Result = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientMedicineIndentItems (IndentId, ItemId, RequestedQTY, Dose, Route, Times, Duration, Meal, Remarks, CreatedBy, IpAddress, UniqueId)
    SELECT @Result, j.ItemId, j.Qty, j.Dose, j.Route, j.Times, j.Duration, j.Meal, j.Remarks, @userId, @ipAddress, CONVERT(nvarchar(100), NEWID())
    FROM OPENJSON(@itemsJson) WITH (ItemId int '$.itemId', Qty int '$.qty', Dose nvarchar(100) '$.dose', Route nvarchar(100) '$.route',
                                    Times nvarchar(100) '$.times', Duration nvarchar(100) '$.duration', Meal nvarchar(100) '$.meal',
                                    Remarks nvarchar(max) '$.remarks') j
    WHERE j.ItemId > 0 AND j.Qty > 0;
    SELECT @Result;
END
GO

/* ---------- 6k. Reject a whole indent (OLD U_rejectIndent) ---------- */
CREATE OR ALTER PROCEDURE dbo.U_RejectPatientIndent
    @hospId int, @branchId int, @roleId int, @indentId int, @reason nvarchar(256), @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PatientMedicineIndent WHERE IndentId = @indentId AND HospId = @hospId AND ToBranchId = @branchId AND ToDeptId = @roleId)
    BEGIN SET @Result = 0; SELECT @Result; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM dbo.PatientMedicineIndent WHERE IndentId = @indentId AND StatusId = 1 AND IsCancel = 0)
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    UPDATE dbo.PatientMedicineIndent SET StatusId = 3, Status = 'Rejected', LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE IndentId = @indentId;
    -- OLD set RejectedQTY = RequestedQTY; here only the still-pending part is rejected so issued quantities stay consistent
    UPDATE dbo.PatientMedicineIndentItems SET RejectionRemarks = @reason, RejectedQTY = RequestedQTY - IssuedQTY,
           LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE IndentId = @indentId;
    SET @Result = @indentId;
    SELECT @Result;
END
GO

/* ---------- 6l. EMR medicine requests (OLD S_GetNewEMRMedicineAdviseDetails; NEW EMR keeps the medicine list as JSON in PatientDoctorHeadersData) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetEmrMedicineRequests
    @hospId int, @branchId int, @fromDate date, @toDate date, @search nvarchar(100) = NULL, @statusId int = NULL  -- NULL all, 1 pending, 2 dispensed
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');
    ;WITH v AS
    (
        SELECT h.VisitId, MAX(h.CreatedOn) AS PrescribedOn
        FROM dbo.PatientDoctorHeadersData h
        WHERE ISJSON(h.HeaderValue) = 1 AND JSON_VALUE(h.HeaderValue, '$[0].medicineName') IS NOT NULL
          AND CAST(h.CreatedOn AS date) BETWEEN @fromDate AND @toDate
        GROUP BY h.VisitId
    )
    SELECT v.VisitId, v.PrescribedOn, pvd.VisitNo, pvd.PatientId, pm.UHID, pm.PatientName, pm.Age, pm.Gender, pm.ContactNumber,
           pvd.DoctorId, dm.CompleteName AS DoctorName, ISNULL(dm.Department, '') AS Department, pvd.CorporateId,
           (SELECT COUNT(*) FROM dbo.PatientDoctorHeadersData h2 CROSS APPLY OPENJSON(h2.HeaderValue) j
             WHERE h2.VisitId = v.VisitId AND ISJSON(h2.HeaderValue) = 1 AND JSON_VALUE(h2.HeaderValue, '$[0].medicineName') IS NOT NULL) AS ItemCount,
           CASE WHEN d.StoreBillId IS NULL THEN 1 ELSE 2 END AS StatusId,
           CASE WHEN d.StoreBillId IS NULL THEN 'Pending' ELSE 'Dispensed' END AS Status,
           d.BillNo
    FROM v
    INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = v.VisitId AND pvd.TypeId = 1
    INNER JOIN dbo.PatientMaster pm ON pm.PatientId = pvd.PatientId
    LEFT JOIN dbo.DoctorMaster dm ON dm.DoctorId = pvd.DoctorId
    OUTER APPLY (SELECT TOP 1 sb.StoreBillId, sb.BillNo FROM dbo.StoreBills sb WHERE sb.SourceTypeId = 1 AND sb.SourceId = v.VisitId AND sb.IsCancel = 0) d
    WHERE pvd.HospId = @hospId AND pvd.BranchId = @branchId
      AND (@statusId IS NULL OR (@statusId = 1 AND d.StoreBillId IS NULL) OR (@statusId = 2 AND d.StoreBillId IS NOT NULL))
      AND (@search IS NULL OR pm.UHID LIKE '%' + @search + '%' OR pm.PatientName LIKE '%' + @search + '%'
           OR pvd.VisitNo LIKE '%' + @search + '%' OR dm.CompleteName LIKE '%' + @search + '%')
    ORDER BY v.PrescribedOn DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetEmrMedicineRequestItems
    @hospId int, @branchId int, @roleId int, @visitId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today date = CAST(GETDATE() AS date);
    SELECT TRY_CAST(JSON_VALUE(m.value, '$.serviceItemId') AS int) AS ItemId,
           JSON_VALUE(m.value, '$.medicineName') AS MedicineName,
           JSON_VALUE(m.value, '$.schedule[0].doseQty') AS DoseQty,
           JSON_VALUE(m.value, '$.schedule[0].doseUnit') AS DoseUnit,
           JSON_VALUE(m.value, '$.schedule[0].frequency') AS Frequency,
           JSON_VALUE(m.value, '$.schedule[0].durationValue') AS DurationValue,
           JSON_VALUE(m.value, '$.schedule[0].durationUnit') AS DurationUnit,
           JSON_VALUE(m.value, '$.schedule[0].route') AS Route,
           JSON_VALUE(m.value, '$.schedule[0].instruction') AS Instruction,
           ISNULL(sim.ItemDiscription, '') AS SaltName, ISNULL(df.DosageFormName, '') AS DosageForm,
           ISNULL((SELECT SUM(st.CurrentStock) FROM dbo.Stock st WHERE st.ItemId = TRY_CAST(JSON_VALUE(m.value, '$.serviceItemId') AS int)
                   AND st.BranchId = @branchId AND st.RoleId = @roleId AND st.IsActive = 1 AND st.CurrentStock > 0 AND st.ExpiryDate > @today), 0) AS InStock
    FROM dbo.PatientDoctorHeadersData h
    INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = h.VisitId AND pvd.HospId = @hospId
    CROSS APPLY OPENJSON(h.HeaderValue) m
    LEFT JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = TRY_CAST(JSON_VALUE(m.value, '$.serviceItemId') AS int)
    LEFT JOIN dbo.DosageFormMaster df ON df.DosageFormId = sim.DosageFormId
    WHERE h.VisitId = @visitId AND ISJSON(h.HeaderValue) = 1 AND JSON_VALUE(h.HeaderValue, '$[0].medicineName') IS NOT NULL;
END
GO

/* ---------- 6m. Rights used by the sale ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPharmacyUserRights
    @branchId int, @userId int, @roleId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CASE WHEN EXISTS (SELECT 1 FROM dbo.UserRightMapping urm INNER JOIN dbo.UserRightsMaster ur ON ur.Id = urm.UserRightId
                          WHERE ur.Name = 'CanSellNarcoDrugs' AND urm.UserId = @userId AND urm.TypeId = 1 AND urm.BranchId = @branchId
                            AND (urm.RoleId = @roleId OR urm.RoleId = 0) AND urm.IsActive = 1) THEN 1 ELSE 0 END AS CanSellNarcoDrugs,
        ISNULL((SELECT TOP 1 DiscPerPharmacy FROM dbo.UserwiseDiscountMaster WHERE UserId = @userId), 100.00) AS MaxDiscountPer;
END
GO

/* ---------- 6n. Patient (+ admitted IPD visit) used by the save; visitId 0 = OPD ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPatientForSale
    @hospId int, @branchId int, @patientId int, @visitId int = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pm.PatientId, pm.UHID, pm.PatientName, pm.Age, ISNULL(pm.CorporateId, 1) AS PatientCorporateId,
           ISNULL(pm.InsuranceCompanyId, 0) AS InsuranceCompanyId, ISNULL(pm.IsGeneralPatient, 0) AS IsGeneralPatient,
           pvd.VisitId, pvd.CorporateId AS VisitCorporateId, pvd.DoctorId AS VisitDoctorId,
           CASE WHEN pvd.VisitId IS NOT NULL AND pvd.TypeId = 2 AND pvd.BranchId = @branchId AND pvd.StatusId = 1
                     AND ISNULL(pvd.IsDischarged, 0) = 0 AND ISNULL(pvd.IsCancel, 0) = 0 THEN 1 ELSE 0 END AS IsAdmitted
    FROM dbo.PatientMaster pm
    LEFT JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = NULLIF(@visitId, 0) AND pvd.PatientId = pm.PatientId
    WHERE pm.HospId = @hospId AND pm.PatientId = @patientId;
END
GO

/* ---------- 6o. Bill slip printed (design: Reprint #n · Duplicate, total prints) ----------
   @Result = total prints after this one (1 = original), 0 = bill not found */
CREATE OR ALTER PROCEDURE dbo.U_PharmacyBillPrint
    @hospId int, @storeBillId int, @userId int, @IpAddress nvarchar(100) = NULL, @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @t TABLE (PrintCount int);
    UPDATE dbo.StoreBills
       SET PrintCount = PrintCount + 1, LastPrintedBy = @userId, LastPrintedOn = GETDATE()
    OUTPUT inserted.PrintCount INTO @t
    WHERE HospId = @hospId AND StoreBillId = @storeBillId;
    SET @Result = ISNULL((SELECT TOP 1 PrintCount FROM @t), 0);
    SELECT @Result;
END
GO
