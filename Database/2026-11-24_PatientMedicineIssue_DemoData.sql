/* =====================================================================================================
   2026-11-24  Patient Medicine Issue - demo data (owner: "add some dummy patient data in EMR Request,
               ward indents ... medicine are same as serviceitemmaster, so we bind it to bill ... use patientmaster")

   Re-runnable. Every demo row is tagged IpAddress = 'demo-pmi' so it can be found and removed.
   Uses existing data only for people: PatientMaster / PatientVisitDetails rows of branch 1
   (OPD visits 3048-3057, open IPD admissions 3066-3075, created by the 2026-11-16 test-data seed).

     1. Stock       demo batches (Branch 1, Pharmacy RoleId 5) for the ServiceItemMaster medicines used below,
                    so EMR / indent lines can be loaded into the bill. Inserted once per item; never deleted
                    (sales may already have issued from them).
     2. OPD visits  the 8 chosen OPD visits had DoctorId 0; a doctor is set only where it is still 0.
     3. EMR         PatientDoctorHeadersData rows (Section 12 Medication, Header 3 "Medicine List", ControlType 8)
                    in the same JSON shape MedicineListControl saves. One per visit; while still not dispensed
                    the prescribed time is moved to "today minus a few hours/days" so it stays in the 7-day inbox.
     4. Indents     PatientMedicineIndent (+ Items) to Branch 1 / Pharmacy (ToDeptId 5): Urgent + Routine pending
                    indents, one with an out-of-stock medicine, one rejected. One per visit; pending ones get a
                    fresh CreatedOn on re-run.

     5. Discharge   PatientDischargeSummary (approved header, only where the visit has none) + PatientDischargeMedication
                    take-home medicines for 5 open IPD admissions (3065, 3067, 3068, 3074, 3076).
                    Run 2026-11-24_DischargeMedication.sql FIRST (it creates the table).

   Remove all demo rows (not the stock):
     DELETE FROM dbo.PatientDischargeMedication WHERE IpAddress = 'demo-pmi';
     DELETE FROM dbo.PatientDischargeSummary WHERE IpAddress = 'demo-pmi';
     DELETE i FROM dbo.PatientMedicineIndentItems i JOIN dbo.PatientMedicineIndent p ON p.IndentId = i.IndentId WHERE p.IpAddress = 'demo-pmi';
     DELETE FROM dbo.PatientMedicineIndent WHERE IpAddress = 'demo-pmi';
     DELETE FROM dbo.PatientDoctorHeadersData WHERE IpAddress = 'demo-pmi';
   ===================================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- Stock has a filtered index; sqlcmd starts with QUOTED_IDENTIFIER OFF
SET ANSI_NULLS ON;
BEGIN TRAN;

DECLARE @hospId int = 1, @branchId int = 1, @roleId int = 5, @seedUser int = 2, @tag nvarchar(20) = N'demo-pmi';
DECLARE @now datetime = GETDATE();

/* ---------- 1. Stock batches for the medicines ---------- */
DECLARE @med TABLE (ItemId int PRIMARY KEY, Packs int, Packing int, UnitCost decimal(18,4), Mrp decimal(18,4), Gst decimal(9,2), Hsn nvarchar(20), BatchNo nvarchar(30), ExpMonths int);
INSERT INTO @med VALUES
 (118528, 20, 10,   2.10,   3.20, 12, N'30049099', N'DM-ACL2611', 20),   -- ACILOC 150MG TAB
 (119072, 30, 15,   1.90,   3.10, 12, N'30049099', N'DM-DL6511', 24),    -- DOLO 650MG TAB
 (120709, 20, 10,   4.20,   6.50, 12, N'30049099', N'DM-ZDP2611', 18),   -- ZERODOL P TAB
 (119542, 15, 15,   0.85,   1.40, 12, N'30049099', N'DM-LSX2611', 22),   -- LASIX 40MG TAB
 (120611, 20, 15,   6.40,   9.80, 12, N'30049099', N'DM-VGPM2611', 20),  -- VGP MET TAB
 (118729, 15, 10,   9.60,  14.50, 12, N'30049099', N'DM-BTV2611', 19),   -- BETAVERT 16MG TAB
 (118548, 20,  1,  14.00,  22.00, 12, N'30049099', N'DM-AF2611', 26),    -- AF 150MG TAB
 (118690, 15, 10,   2.80,   4.40, 12, N'30049099', N'DM-AVM2611', 21),   -- AVOMINE 25MG
 (119535, 10, 15,   1.30,   2.10, 12, N'30049099', N'DM-LNX2611', 23),   -- LANOXIN 0.25MG TAB
 (119256, 15, 10,   6.10,   9.50, 12, N'30049099', N'DM-FLV2611', 20),   -- FLAVEDON MR TAB
 (119550, 15, 10,   7.30,  11.20, 12, N'30049099', N'DM-LVR2611', 19),   -- LEVERA DT 250MG TAB
 (118156, 10, 30,   3.00,   4.60, 12, N'30049099', N'DM-AST2611', 17),   -- ASTHALIN ROTACAPS 30'S
 (118374, 20,  1,  24.00,  38.00, 12, N'30049099', N'DM-DLS2611', 15),   -- DOLO 250MG SUSPENSION 60ML
 (117357, 25,  1, 160.00, 245.00, 12, N'30049099', N'DM-DLI2611', 16),   -- DOLO INFUSION
 (117595, 10,  1, 205.00, 310.00, 12, N'30049099', N'DM-NRF2611', 14),   -- NEOROF 20ML INJ (narcotic category)
 (113906, 12,  1,  64.00,  98.00, 18, N'33069000', N'DM-HXD2611', 24);   -- HEXIDINE 160ML MOUTHWASH
-- 112383 AMBISOME 50MG INJ already has stock; 119538 LARENTINE D 10MG TAB is left without stock on purpose (shows "Out of stock")

INSERT INTO dbo.Stock (HospId, ReceivingId, FTDID, BranchId, RoleId, ItemId, ItemName, CurrentStock, QTY, FreeQTY, IssuedQTY, ReturnedQTY,
                       UnitCost, Rate, MRP, MajorMRP, BatchNo, ExpiryDate, HSNCode, Packing, GSTPercentage, IsActive,
                       ReceivedBy, ReceivedOn, CreatedBy, CreatedOn, IpAddress)
SELECT @hospId, NULL, NULL, @branchId, @roleId, m.ItemId, sim.Name, m.Packs * m.Packing, m.Packs, 0, 0, 0,
       m.UnitCost, m.UnitCost * m.Packing, m.Mrp, m.Mrp * m.Packing, m.BatchNo,
       DATEADD(month, m.ExpMonths, CAST(@now AS date)), m.Hsn, m.Packing, m.Gst, 1,
       @seedUser, @now, @seedUser, @now, @tag
FROM @med m
INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = m.ItemId
WHERE NOT EXISTS (SELECT 1 FROM dbo.Stock s WHERE s.ItemId = m.ItemId AND s.BranchId = @branchId AND s.RoleId = @roleId AND s.IpAddress = @tag);

/* ---------- 2 + 3. EMR prescriptions on OPD visits ---------- */
DECLARE @emr TABLE (VisitId int PRIMARY KEY, DoctorId int, MinutesAgo int);
INSERT INTO @emr VALUES
 (3057,    7,   35),   -- MR. PANKAJ KUSHWAHA 81 M   - Dr. Ashutosh Mishra
 (3056, 2002,   80),   -- MRS. POOJA KHAN 37 F       - Dr. Prajwal Sharma (ENT)
 (3055, 2002,  150),   -- MR. YOGESH AGARWAL 22 M    - Dr. Prajwal Sharma (ENT)
 (3054,    5,  240),   -- MRS. JYOTI YADAV 55 F      - Dr. Rashika Gupta (Dental)
 (3053,    7,  420),   -- MR. ARJUN DUBEY 63 M       - Dr. Ashutosh Mishra
 (3052, 2003, 1500),   -- MR. ADITYA SHUKLA 45 M     - Dr. Akhilesh Shahu (Surgeon)
 (3051,    7, 2950),   -- MRS. ANANYA SHARMA 58 F    - Dr. Ashutosh Mishra
 (3048, 1002, 4400);   -- MASTER ANKIT PANDEY 6 M    - Dr. Neelam Singh (Pediatric)

DECLARE @emrLine TABLE (VisitId int, Seq int, ItemId int, DoseQty nvarchar(20), DoseUnit nvarchar(20), Frequency nvarchar(50),
                        DurationValue nvarchar(10), DurationUnit nvarchar(20), Route nvarchar(30), Instruction nvarchar(100));
INSERT INTO @emrLine VALUES
 (3057, 1, 119542, N'1', N'tablets', N'Once a Day',   N'10', N'Days', N'Oral', N'Take on empty stomach'),
 (3057, 2, 119535, N'1', N'tablets', N'Once a Day',   N'10', N'Days', N'Oral', N'Do not crush'),
 (3057, 3, 118528, N'1', N'tablets', N'Twice a Day',  N'10', N'Days', N'Oral', N'Take on empty stomach'),
 (3056, 1, 118729, N'1', N'tablets', N'Twice a Day',  N'7',  N'Days', N'Oral', N'Take with food'),
 (3056, 2, 118690, N'1', N'tablets', N'SOS',          N'3',  N'Days', N'Oral', N'Take at bedtime'),
 (3056, 3, 113906, N'10', N'ml',     N'Twice a Day',  N'7',  N'Days', N'Topical', N'Gargle, do not swallow'),
 (3055, 1, 119072, N'1', N'tablets', N'Thrice a Day', N'3',  N'Days', N'Oral', N'Take with food'),
 (3055, 2, 118548, N'1', N'tablets', N'Once a Day',   N'1',  N'Days', N'Oral', N'Take with plenty of water'),
 (3055, 3, 118156, N'1', N'capsules', N'Twice a Day', N'15', N'Days', N'Inhalational', N'Use with rotahaler'),
 (3054, 1, 120709, N'1', N'tablets', N'Twice a Day',  N'5',  N'Days', N'Oral', N'Take with food'),
 (3054, 2, 113906, N'10', N'ml',     N'Thrice a Day', N'5',  N'Days', N'Topical', N'Gargle, do not swallow'),
 (3054, 3, 118528, N'1', N'tablets', N'Twice a Day',  N'5',  N'Days', N'Oral', N'Take on empty stomach'),
 (3053, 1, 120611, N'1', N'tablets', N'Twice a Day',  N'30', N'Days', N'Oral', N'Take with food'),
 (3053, 2, 119256, N'1', N'tablets', N'Twice a Day',  N'30', N'Days', N'Oral', N'Take with food'),
 (3053, 3, 119538, N'1', N'tablets', N'Once a Day',   N'30', N'Days', N'Oral', N'Take at bedtime'),
 (3052, 1, 120709, N'1', N'tablets', N'Twice a Day',  N'5',  N'Days', N'Oral', N'Take with food'),
 (3052, 2, 119072, N'1', N'tablets', N'SOS',          N'3',  N'Days', N'Oral', N'Take with food'),
 (3052, 3, 118528, N'1', N'tablets', N'Twice a Day',  N'5',  N'Days', N'Oral', N'Take on empty stomach'),
 (3051, 1, 119550, N'1', N'tablets', N'Twice a Day',  N'30', N'Days', N'Oral', N'Do not crush'),
 (3051, 2, 119072, N'1', N'tablets', N'SOS',          N'5',  N'Days', N'Oral', N'Take with food'),
 (3048, 1, 118374, N'5', N'ml',      N'Thrice a Day', N'3',  N'Days', N'Oral', N'Give after food'),
 (3048, 2, 118156, N'1', N'capsules', N'Twice a Day', N'5',  N'Days', N'Inhalational', N'Use with rotahaler');

-- the OPD visits of the 2026-11-16 seed have no doctor; set one only where it is still empty
UPDATE pvd SET DoctorId = e.DoctorId
FROM dbo.PatientVisitDetails pvd INNER JOIN @emr e ON e.VisitId = pvd.VisitId
WHERE pvd.TypeId = 1 AND ISNULL(pvd.DoctorId, 0) = 0;

INSERT INTO dbo.PatientDoctorHeadersData (PatientId, VisitId, SectionId, HeaderId, ControlTypeId, TemplateId, HeaderValue, CreatedBy, CreatedOn, IpAddress)
SELECT pvd.PatientId, e.VisitId, 12, 3, 8, 0,
       (SELECT CONVERT(nvarchar(36), NEWID()) AS id, sim.Name AS medicineName, CAST(l.ItemId AS nvarchar(20)) AS serviceItemId,
               CAST(0 AS bit) AS isTapering, CAST(0 AS bit) AS isVariableDose, CAST(0 AS bit) AS favourite,
               JSON_QUERY((SELECT CONVERT(nvarchar(36), NEWID()) AS id, l.DoseQty AS doseQty, l.DoseUnit AS doseUnit, l.Frequency AS frequency,
                                  l.DurationValue AS durationValue, l.DurationUnit AS durationUnit, l.Route AS route, l.Instruction AS instruction
                           FOR JSON PATH)) AS schedule
        FROM @emrLine l INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = l.ItemId
        WHERE l.VisitId = e.VisitId ORDER BY l.Seq
        FOR JSON PATH),
       e.DoctorId, DATEADD(minute, -e.MinutesAgo, @now), @tag
FROM @emr e
INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = e.VisitId AND pvd.TypeId = 1
WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientDoctorHeadersData h WHERE h.VisitId = e.VisitId AND h.IpAddress = @tag);

-- keep not-yet-dispensed demo prescriptions inside the "last 7 days" inbox
UPDATE h SET CreatedOn = DATEADD(minute, -e.MinutesAgo, @now)
FROM dbo.PatientDoctorHeadersData h INNER JOIN @emr e ON e.VisitId = h.VisitId
WHERE h.IpAddress = @tag
  AND NOT EXISTS (SELECT 1 FROM dbo.StoreBills sb WHERE sb.SourceTypeId = 1 AND sb.SourceId = h.VisitId AND sb.IsCancel = 0);

/* ---------- 4. Ward indents from open IPD admissions to the Pharmacy ---------- */
DECLARE @ind TABLE (VisitId int PRIMARY KEY, Priority nvarchar(20), RaisedBy int, MinutesAgo int, Remarks nvarchar(500), Rejected bit, RejectReason nvarchar(256));
INSERT INTO @ind VALUES
 (3075, N'Urgent',  3,     25, N'Post-op fever 101 F, start IV paracetamol. CHF on digoxin.', 0, NULL),          -- MR. SUNIL SINGH 80 M, Deluxe / 37
 (3071, N'Urgent',  3006,  50, N'Shifted to OT for appendicectomy - anaesthesia drugs required.', 0, NULL),       -- MASTER ROHAN KUSHWAHA 13 M, General / 9
 (3072, N'Routine', 3,    110, N'Fungal infection - Day 2 of Ambisome.', 0, NULL),                                -- MRS. SWATI PAL 33 F, Deluxe / 35
 (3070, N'Routine', 3006, 180, N'Vertigo with vomiting.', 0, NULL),                                               -- MR. ROHAN MISHRA 63 M, General / 13
 (3069, N'Routine', 3,    300, N'Regular diabetic and cardiac medicines for 5 days.', 0, NULL),                  -- MR. DEEPAK UPADHYAY 60 M, Deluxe / 33
 (3073, N'Routine', 3006, 520, N'Seizure prophylaxis, wheeze at night.', 0, NULL),                                -- MRS. SARITA SRIVASTAVA 54 F, General / 14
 (3066, N'Routine', 3,   1380, N'Pain management.', 1, N'Duplicate indent - same medicines already sent on the morning indent'); -- MRS. POOJA PATEL 59 F

DECLARE @indLine TABLE (VisitId int, Seq int, ItemId int, Qty int, Dose nvarchar(100), Times nvarchar(100), Duration nvarchar(100), Route nvarchar(100), Meal nvarchar(100));
INSERT INTO @indLine VALUES
 (3075, 1, 117357, 3, N'1 g',      N'TDS',  N'1 day',  N'IV',   NULL),
 (3075, 2, 119542, 4, N'40 mg',    N'BD',   N'2 days', N'Oral', N'Before food'),
 (3075, 3, 119535, 2, N'0.25 mg',  N'OD',   N'2 days', N'Oral', N'After food'),
 (3075, 4, 118528, 4, N'150 mg',   N'BD',   N'2 days', N'Oral', N'Before food'),
 (3071, 1, 117595, 2, N'2 mg/kg',  N'Stat', N'Once',   N'IV',   NULL),
 (3071, 2, 117357, 2, N'15 mg/kg', N'TDS',  N'1 day',  N'IV',   NULL),
 (3071, 3, 113906, 1, N'10 ml',    N'BD',   N'3 days', N'Gargle', NULL),
 (3072, 1, 112383, 2, N'3 mg/kg',  N'OD',   N'2 days', N'IV',   NULL),
 (3072, 2, 118528, 4, N'150 mg',   N'BD',   N'2 days', N'Oral', N'Before food'),
 (3072, 3, 118690, 3, N'25 mg',    N'HS',   N'3 days', N'Oral', N'After food'),
 (3070, 1, 118729, 6, N'16 mg',    N'TDS',  N'2 days', N'Oral', N'After food'),
 (3070, 2, 118690, 4, N'25 mg',    N'BD',   N'2 days', N'Oral', N'After food'),
 (3070, 3, 119072, 6, N'650 mg',   N'TDS',  N'2 days', N'Oral', N'After food'),
 (3069, 1, 120611, 10, N'50/500 mg', N'BD', N'5 days', N'Oral', N'After food'),
 (3069, 2, 119256, 10, N'35 mg',   N'BD',   N'5 days', N'Oral', N'After food'),
 (3069, 3, 119538, 5, N'10 mg',    N'HS',   N'5 days', N'Oral', N'After food'),
 (3073, 1, 119550, 6, N'250 mg',   N'BD',   N'3 days', N'Oral', N'After food'),
 (3073, 2, 118156, 1, N'200 mcg',  N'BD',   N'15 days', N'Inhalation', NULL),
 (3066, 1, 120709, 6, N'1 tab',    N'BD',   N'3 days', N'Oral', N'After food'),
 (3066, 2, 118528, 6, N'150 mg',   N'BD',   N'3 days', N'Oral', N'Before food');

DECLARE @v int, @pri nvarchar(20), @by int, @ago int, @rem nvarchar(500), @rej bit, @rejWhy nvarchar(256), @no int, @newId int;
DECLARE ic CURSOR LOCAL FAST_FORWARD FOR
    SELECT i.VisitId, i.Priority, i.RaisedBy, i.MinutesAgo, i.Remarks, i.Rejected, i.RejectReason
    FROM @ind i
    INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = i.VisitId AND pvd.TypeId = 2 AND ISNULL(pvd.IsDischarged, 0) = 0 AND ISNULL(pvd.IsCancel, 0) = 0
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientMedicineIndent p WHERE p.VisitId = i.VisitId AND p.IpAddress = @tag)
    ORDER BY i.MinutesAgo DESC;
OPEN ic;
FETCH NEXT FROM ic INTO @v, @pri, @by, @ago, @rem, @rej, @rejWhy;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @no = ISNULL((SELECT MAX(IndentNo) FROM dbo.PatientMedicineIndent WITH (UPDLOCK, HOLDLOCK) WHERE ToBranchId = @branchId), 0) + 1;
    INSERT INTO dbo.PatientMedicineIndent (HospId, BranchId, IndentNo, PatientId, VisitId, ToBranchId, ToDeptId, Status, StatusId, Priority, Remarks,
                                           CreatedBy, CreatedOn, LastModifiedBy, LastModifiedOn, IpAddress, UniqueId)
    SELECT @hospId, @branchId, @no, pvd.PatientId, @v, @branchId, @roleId,
           CASE WHEN @rej = 1 THEN N'Rejected' ELSE N'Open' END, CASE WHEN @rej = 1 THEN 3 ELSE 1 END, @pri, @rem,
           @by, DATEADD(minute, -@ago, @now),
           CASE WHEN @rej = 1 THEN @seedUser END, CASE WHEN @rej = 1 THEN DATEADD(minute, -@ago + 40, @now) END,
           @tag, CONVERT(nvarchar(100), NEWID())
    FROM dbo.PatientVisitDetails pvd WHERE pvd.VisitId = @v;
    SET @newId = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientMedicineIndentItems (IndentId, ItemId, RequestedQTY, IssuedQTY, RejectedQTY, RejectionRemarks, Dose, Route, Times, Duration, Meal,
                                                CreatedBy, CreatedOn, IpAddress, UniqueId)
    SELECT @newId, l.ItemId, l.Qty, 0, CASE WHEN @rej = 1 THEN l.Qty ELSE 0 END, CASE WHEN @rej = 1 THEN @rejWhy END,
           l.Dose, l.Route, l.Times, l.Duration, l.Meal, @by, DATEADD(minute, -@ago, @now), @tag, CONVERT(nvarchar(100), NEWID())
    FROM @indLine l WHERE l.VisitId = @v ORDER BY l.Seq;

    FETCH NEXT FROM ic INTO @v, @pri, @by, @ago, @rem, @rej, @rejWhy;
END
CLOSE ic; DEALLOCATE ic;

-- pending demo indents nothing has been issued against: refresh the raised time on re-run
UPDATE p SET CreatedOn = DATEADD(minute, -i.MinutesAgo, @now)
FROM dbo.PatientMedicineIndent p INNER JOIN @ind i ON i.VisitId = p.VisitId
WHERE p.IpAddress = @tag AND p.StatusId = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.PatientMedicineIndentItems x WHERE x.IndentId = p.IndentId AND x.IssuedQTY + x.RejectedQTY > 0);

/* ---------- 5. Discharge Summary take-home medicines (needs 2026-11-24_DischargeMedication.sql) ---------- */
DECLARE @dis TABLE (VisitId int PRIMARY KEY, MinutesAgo int, Header nvarchar(256));
INSERT INTO @dis VALUES
 (3076,   40, N'Discharge Summary - Acute gastroenteritis'),        -- MRS. JYOTI YADAV 55 F, Deluxe / 38
 (3074,   95, N'Discharge Summary - Lower respiratory tract infection'), -- MR. DEEPAK SRIVASTAVA 43 M, General / 10
 (3068,  200, N'Discharge Summary - Dental abscess, I&D done'),     -- MR. SANDEEP VERMA 29 M, General / 8
 (3067,  330, N'Discharge Summary - Viral fever with wheeze'),      -- MASTER SURESH AGARWAL 12 M, General / 12
 (3065, 1250, N'Discharge Summary - Type 2 DM with hypertension');  -- MRS. DIVYA PAL 60 F, General / 7

DECLARE @disLine TABLE (VisitId int, Seq int, ItemId int, Qty int, Dose nvarchar(100), Freq nvarchar(100), Duration nvarchar(100), Route nvarchar(100), Meal nvarchar(100), Remarks nvarchar(256));
INSERT INTO @disLine VALUES
 (3076, 1, 118528, 10, N'150 mg', N'Twice a Day',  N'5 Days',  N'Oral', N'Before food', NULL),
 (3076, 2, 118690,  6, N'25 mg',  N'Thrice a Day', N'2 Days',  N'Oral', N'After food',  N'Stop if no vomiting'),
 (3076, 3, 119072,  6, N'650 mg', N'SOS',          N'3 Days',  N'Oral', N'After food',  N'Only if fever > 100 F'),
 (3074, 1, 118548,  1, N'150 mg', N'Once a Day',   N'1 Day',   N'Oral', N'After food',  NULL),
 (3074, 2, 118156, 30, N'200 mcg', N'Twice a Day', N'15 Days', N'Inhalational', NULL,   N'Rinse mouth after use'),
 (3074, 3, 119072, 10, N'650 mg', N'Thrice a Day', N'3 Days',  N'Oral', N'After food',  NULL),
 (3074, 4, 120709, 10, N'1 tab',  N'Twice a Day',  N'5 Days',  N'Oral', N'After food',  NULL),
 (3068, 1, 120709, 10, N'1 tab',  N'Twice a Day',  N'5 Days',  N'Oral', N'After food',  NULL),
 (3068, 2, 113906,  1, N'10 ml',  N'Thrice a Day', N'7 Days',  N'Topical', NULL,        N'Gargle after meals'),
 (3068, 3, 118528, 10, N'150 mg', N'Twice a Day',  N'5 Days',  N'Oral', N'Before food', NULL),
 (3067, 1, 118374,  1, N'5 ml',   N'Thrice a Day', N'3 Days',  N'Oral', N'After food',  NULL),
 (3067, 2, 118156, 30, N'200 mcg', N'Twice a Day', N'15 Days', N'Inhalational', NULL,   NULL),
 (3065, 1, 120611, 30, N'50/500 mg', N'Twice a Day', N'15 Days', N'Oral', N'After food', NULL),
 (3065, 2, 119256, 30, N'35 mg',  N'Twice a Day',  N'15 Days', N'Oral', N'After food',  NULL),
 (3065, 3, 119542, 15, N'40 mg',  N'Once a Day',   N'15 Days', N'Oral', N'Before food', N'Morning dose'),
 (3065, 4, 119538, 15, N'10 mg',  N'Once a Day',   N'15 Days', N'Oral', N'After food',  N'At bedtime');

-- discharge summary header (approved by the treating doctor) when the visit has none yet
INSERT INTO dbo.PatientDischargeSummary (HospId, VisitId, ReportHeader, IsApproved, ApprovedByDoctorId, ApprovedByUserId, ApprovedOn, CreatedBy, CreatedOn, IpAddress)
SELECT @hospId, d.VisitId, d.Header, 1, NULLIF(pvd.DoctorId, 0), @seedUser, DATEADD(minute, -d.MinutesAgo, @now), @seedUser, DATEADD(minute, -d.MinutesAgo - 30, @now), @tag
FROM @dis d
INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = d.VisitId AND pvd.TypeId = 2 AND ISNULL(pvd.IsCancel, 0) = 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientDischargeSummary s WHERE s.VisitId = d.VisitId);

INSERT INTO dbo.PatientDischargeMedication (HospId, VisitId, DSId, PatientId, ItemId, ItemName, Qty, Dose, FrequencyName, Duration, Route, Meal, Remarks, CreatedBy, CreatedOn, IpAddress)
SELECT @hospId, l.VisitId, (SELECT MAX(s.DSId) FROM dbo.PatientDischargeSummary s WHERE s.VisitId = l.VisitId), pvd.PatientId,
       l.ItemId, sim.Name, l.Qty, l.Dose, l.Freq, l.Duration, l.Route, l.Meal, l.Remarks, ISNULL(NULLIF(pvd.DoctorId, 0), @seedUser),
       DATEADD(minute, -d.MinutesAgo, @now), @tag
FROM @disLine l
INNER JOIN @dis d ON d.VisitId = l.VisitId
INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = l.VisitId AND pvd.TypeId = 2 AND ISNULL(pvd.IsCancel, 0) = 0
INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = l.ItemId
WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientDischargeMedication m WHERE m.VisitId = l.VisitId AND m.IpAddress = @tag)
ORDER BY l.VisitId, l.Seq;

-- not yet dispensed: keep the written time recent
UPDATE m SET CreatedOn = DATEADD(minute, -d.MinutesAgo, @now)
FROM dbo.PatientDischargeMedication m INNER JOIN @dis d ON d.VisitId = m.VisitId
WHERE m.IpAddress = @tag
  AND NOT EXISTS (SELECT 1 FROM dbo.StoreBills sb WHERE sb.SourceTypeId = 3 AND sb.SourceId = m.VisitId AND sb.IsCancel = 0);

COMMIT;

/* summary */
SELECT 'Stock batches' AS Demo, COUNT(*) AS [Rows] FROM dbo.Stock WHERE IpAddress = @tag
UNION ALL SELECT 'EMR prescriptions', COUNT(*) FROM dbo.PatientDoctorHeadersData WHERE IpAddress = @tag
UNION ALL SELECT 'Ward indents', COUNT(*) FROM dbo.PatientMedicineIndent WHERE IpAddress = @tag
UNION ALL SELECT 'Ward indent lines', COUNT(*) FROM dbo.PatientMedicineIndentItems WHERE IpAddress = @tag
UNION ALL SELECT 'Discharge summaries', COUNT(*) FROM dbo.PatientDischargeSummary WHERE IpAddress = @tag
UNION ALL SELECT 'Discharge medicines', COUNT(*) FROM dbo.PatientDischargeMedication WHERE IpAddress = @tag;
GO
