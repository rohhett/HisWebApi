/* =====================================================================================================
   2026-11-27  Lab master data for the 20 most-used tests (owner request 2026-11-27)
   Database : GWT_HIS_API          Re-runnable: yes (every step looks up what is already there)

   Top 20 by PatientInvestigationDetails count on 2026-11-27 (Verified):
     Tabular (observations + ranges + option lists + formulas + checks + interpretation):
       40754 CBC · 167 CBC (COMPLETE BLOOD COUNT) · 53707 CBC (COMPLETE BLOOD COUNT). · 40748 LFT · 135 KFT
       811 RFT · 184 HEMOGLOBIN (HB) · 465 URINE COMPLETE · 198 PT & INR · 12946 Hb1AC · 778 T3 T4 TSH
       195 PBF EXAMINATION ... FOR MALARIA PARASITE · 774 MALARIA RAPID PV/PF
     Free text (report template):
       7826 CHEST X-RAY · 916 WIDAL · 23486 NCCT HEAD PLAIN · 42 HRCT CHEST · 795 USG WHOLE ABDOMEN
       23474 FACE X-RAY · 124898 MRI BRAIN PLAIN

   How the result entry reads this data (Verified in S_getPatientTabularReportForResultEntry):
     - rows   = InvastigationObservationMapping (IsActive = 1) ORDER BY MappingId
     - range  = InvastigationObservationRangeMaster per OBSERVATION (not per test),
                FromAge <= age in DAYS <= ToAge and Gender IN ('B', patient M/F). The join has no IsActive
                filter, so overlapping rows would duplicate a grid row: this script keeps one row per age band.
     - option list = ObservationCommentLOVsMapping TypeId 2 -> ObservationListOfValuesMaster
     - formula = LabInvestigationFormula (patient gender row first, else B); checks = LabInvestigationValidation
     - interpretation (TypeId 2) / template (TypeId 1) = InvastigationTemplateCommentMaster
                                                          + InvestigationTemplateInterpretationMapping

   What the script does
     1. Lab methods by name (adds missing ones).
     2. Observations: re-uses the existing CBC observations 1005-1028 (fixes their names, e.g. "Motocytes",
        "PVC/HCT", "PWD", "(ANC)" on the lymphocyte row) and adds the rest by name.
     3. Reference ranges of these observations are replaced (old rows deleted, new rows inserted).
     4. Option lists (LOVs) of these observations are replaced.
     5. Test-observation mapping: when a test's active rows differ from the target, its old rows are set
        IsActive = 0 and the target rows are inserted in report order.
     6. Formulas and checks of these tests: target rows upserted, any other active row set IsActive = 0.
     7. Interpretations / templates upserted by name and linked to the tests (existing links are kept).
     8. ServiceItemMaster.ReportType text set to match ReportTypeId; 53707 had no ReportTypeId -> 1 Tabular.

   Effect on saved results: results already saved stay in PatientInvestigationResultsTabularReports, but an
   observation that is no longer mapped to its test (e.g. "Influenza B" on 40754) no longer shows in the
   result entry grid for that visit.

   Reference ranges are commonly used adult / paediatric intervals (Recommended). The pathologist must check
   them against the lab's own analyser and reagent kit inserts before go-live. Paediatric bands are given
   only where widely agreed (CBC main values, creatinine, ALP, urea, uric acid, calcium, phosphorus); other
   observations show no range below 12 years.
   Age bands (days): 0-30 newborn · 31-364 infant · 365-4379 child (1-11 y) · 4380+ adult (12 y+).

   Dry run: set @DryRun = 1 -> everything runs inside the transaction, a summary is printed, then rolled back.
   ===================================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun bit = 0;
DECLARE @HospId int = 1, @UserId int = 2, @Ip nvarchar(40) = N'2026-11-27 script', @Now datetime = GETDATE();

BEGIN TRAN;

/* ---------------------------------------------------------------- 0. tests */
IF OBJECT_ID('tempdb..#Inv') IS NOT NULL DROP TABLE #Inv;
CREATE TABLE #Inv (InvId int PRIMARY KEY, ExpectedName nvarchar(300) NOT NULL, Panel varchar(10) NOT NULL, IsTabular bit NOT NULL, IsValid bit NOT NULL DEFAULT 0);
INSERT #Inv (InvId, ExpectedName, Panel, IsTabular) VALUES
 (40754, N'CBC', 'CBC', 1), (167, N'CBC (COMPLETE BLOOD COUNT)', 'CBC', 1), (53707, N'CBC (COMPLETE BLOOD COUNT).', 'CBC', 1),
 (40748, N'LFT', 'LFT', 1), (135, N'KFT', 'KFT', 1), (811, N'RFT', 'KFT', 1), (184, N'HEMOGLOBIN (HB)', 'HB', 1),
 (465, N'URINE COMPLETE', 'URINE', 1), (198, N'PT & INR', 'PT', 1), (12946, N'Hb1AC', 'A1C', 1), (778, N'T3 T4 TSH', 'THY', 1),
 (195, N'PBF EXAMINATION (PERIPHERAL BLOOD FILM) FOR MALARIA PARASITE', 'MP', 1), (774, N'MALARIA RAPID PV/PF', 'MAL', 1),
 (7826, N'CHEST X-RAY', 'T_CXR', 0), (916, N'WIDAL', 'T_WID', 0), (23486, N'NCCT HEAD PLAIN', 'T_CTH', 0), (42, N'HRCT CHEST', 'T_HRCT', 0),
 (795, N'USG WHOLE ABDOMEN', 'T_USG', 0), (23474, N'FACE X-RAY', 'T_FACE', 0), (124898, N'MRI BRAIN PLAIN', 'T_MRI', 0);

-- a test is touched only when its id still carries the expected name (protects a server with other ids)
UPDATE i SET IsValid = 1
FROM #Inv i JOIN ServiceItemMaster s ON s.ServiceItemId = i.InvId AND LTRIM(RTRIM(s.Name)) = i.ExpectedName;

DECLARE @skipped nvarchar(max) = (SELECT STRING_AGG(CONCAT(InvId, ' ', ExpectedName), '; ') FROM #Inv WHERE IsValid = 0);
IF @skipped IS NOT NULL PRINT N'Skipped (id / name not found): ' + @skipped;

/* ---------------------------------------------------------------- 1. methods */
IF OBJECT_ID('tempdb..#Method') IS NOT NULL DROP TABLE #Method;
CREATE TABLE #Method (Method nvarchar(200) PRIMARY KEY, MethodId int NULL);
INSERT #Method (Method) VALUES
 (N'Automated Haematology Analyser'), (N'Calculated'), (N'Diazo'), (N'IFCC without P5P'), (N'IFCC (AMP buffer)'), (N'IFCC (Szasz)'),
 (N'Biuret'), (N'Bromocresol Green (BCG)'), (N'Urease-GLDH'), (N'Jaffe Kinetic'), (N'Uricase-POD'), (N'Ion Selective Electrode (ISE)'),
 (N'Arsenazo III'), (N'Phosphomolybdate UV'), (N'Visual'), (N'Dipstick (Reflectance)'), (N'Microscopy'), (N'Clotting (Photo-optical)'),
 (N'HPLC'), (N'CLIA'), (N'Immunochromatography');

INSERT LabMethodMaster (HospId, Method, IsActive, CreatedBy, CreatedOn, IpAddress, UniqueId)
SELECT @HospId, m.Method, 1, @UserId, @Now, @Ip, CONVERT(nvarchar(100), NEWID())
FROM #Method m
WHERE NOT EXISTS (SELECT 1 FROM LabMethodMaster x WHERE x.HospId = @HospId AND x.IsActive = 1 AND LTRIM(RTRIM(x.Method)) = m.Method);

UPDATE m SET MethodId = (SELECT MIN(x.MethodId) FROM LabMethodMaster x WHERE x.HospId = @HospId AND x.IsActive = 1 AND LTRIM(RTRIM(x.Method)) = m.Method)
FROM #Method m;

/* ---------------------------------------------------------------- 2. observations */
IF OBJECT_ID('tempdb..#Obs') IS NOT NULL DROP TABLE #Obs;
CREATE TABLE #Obs (
  Code varchar(12) PRIMARY KEY, ExistingId int NULL, OldName nvarchar(500) NULL, Name nvarchar(500) NOT NULL,
  FieldTypeId int NOT NULL, RoundUp nvarchar(20) NULL, Method nvarchar(200) NULL, Id int NULL);

INSERT #Obs (Code, ExistingId, OldName, Name, FieldTypeId, RoundUp, Method) VALUES
 -- CBC (existing observations 1005-1028 re-used)
 ('H_RBC',  NULL, NULL, N'HAEMOGLOBIN & RBC INDICES', 1, NULL, NULL),
 ('HB',     1010, N'Haemoglobin (Hb)', N'Haemoglobin (Hb)', 1, N'1', N'Automated Haematology Analyser'),
 ('RBC',    1017, N'RBC COUNT', N'Total RBC Count', 1, N'2', N'Automated Haematology Analyser'),
 ('PCV',    1018, N'PVC/HCT', N'Packed Cell Volume (PCV / HCT)', 1, N'1', N'Automated Haematology Analyser'),
 ('MCV',    1019, N'MCV', N'Mean Corpuscular Volume (MCV)', 1, N'1', N'Calculated'),
 ('MCH',    1020, N'MCH', N'Mean Corpuscular Haemoglobin (MCH)', 1, N'1', N'Calculated'),
 ('MCHC',   1021, N'MCHC', N'Mean Corpuscular Hb Concentration (MCHC)', 1, N'1', N'Calculated'),
 ('RDW',    1022, N'RDW-CV', N'Red Cell Distribution Width (RDW-CV)', 1, N'1', N'Automated Haematology Analyser'),
 ('H_WBC',  NULL, NULL, N'WBC COUNT', 1, NULL, NULL),
 ('TLC',    1005, N'WBC/TLC', N'Total Leucocyte Count (TLC)', 1, N'0', N'Automated Haematology Analyser'),
 ('H_DLC',  NULL, NULL, N'DIFFERENTIAL LEUCOCYTE COUNT', 1, NULL, NULL),
 ('NEU',    1006, N'Neutrophils', N'Neutrophils', 1, N'0', N'Automated Haematology Analyser'),
 ('LYM',    1007, N'Lymphocytes', N'Lymphocytes', 1, N'0', N'Automated Haematology Analyser'),
 ('MON',    1009, N'Motocytes', N'Monocytes', 1, N'0', N'Automated Haematology Analyser'),
 ('EOS',    1008, N'Eosinophils', N'Eosinophils', 1, N'0', N'Automated Haematology Analyser'),
 ('BAS',    1011, N'Basophils', N'Basophils', 1, N'0', N'Automated Haematology Analyser'),
 ('H_ABS',  NULL, NULL, N'ABSOLUTE LEUCOCYTE COUNT', 1, NULL, NULL),
 ('ANC',    1012, N'Absolute Lymphocyte Count (ANC)', N'Absolute Neutrophil Count (ANC)', 1, N'0', N'Calculated'),
 ('ALC',    1013, N'Absolute Lymphocyte Count (ALC)', N'Absolute Lymphocyte Count (ALC)', 1, N'0', N'Calculated'),
 ('AMC',    1015, N'Absolute Monocyte Count', N'Absolute Monocyte Count (AMC)', 1, N'0', N'Calculated'),
 ('AEC',    1014, N'Absolute Eosinophil Count', N'Absolute Eosinophil Count (AEC)', 1, N'0', N'Calculated'),
 ('ABC',    1016, N'Absolute Basophil Count', N'Absolute Basophil Count (ABC)', 1, N'0', N'Calculated'),
 ('H_PLT',  NULL, NULL, N'PLATELET PARAMETERS', 1, NULL, NULL),
 ('PLT',    1023, N'Platelet Count', N'Platelet Count', 1, N'0', N'Automated Haematology Analyser'),
 ('MPV',    1024, N'MPV', N'Mean Platelet Volume (MPV)', 1, N'1', N'Automated Haematology Analyser'),
 ('PDW',    1025, N'PWD', N'Platelet Distribution Width (PDW)', 1, N'1', N'Automated Haematology Analyser'),
 ('PCT',    1026, N'PCT', N'Plateletcrit (PCT)', 1, N'2', N'Automated Haematology Analyser'),
 ('PLCC',   1027, N'P-LCC', N'Platelet Large Cell Count (P-LCC)', 1, N'0', N'Automated Haematology Analyser'),
 ('PLCR',   1028, N'P-LCR', N'Platelet Large Cell Ratio (P-LCR)', 1, N'1', N'Automated Haematology Analyser'),
 -- LFT
 ('H_BIL',  NULL, NULL, N'SERUM BILIRUBIN', 1, NULL, NULL),
 ('TBIL',   NULL, NULL, N'Bilirubin Total', 1, N'2', N'Diazo'),
 ('DBIL',   NULL, NULL, N'Bilirubin Direct (Conjugated)', 1, N'2', N'Diazo'),
 ('IBIL',   NULL, NULL, N'Bilirubin Indirect (Unconjugated)', 1, N'2', N'Calculated'),
 ('H_ENZ',  NULL, NULL, N'LIVER ENZYMES', 1, NULL, NULL),
 ('AST',    NULL, NULL, N'SGOT (AST)', 1, N'0', N'IFCC without P5P'),
 ('ALT',    NULL, NULL, N'SGPT (ALT)', 1, N'0', N'IFCC without P5P'),
 ('ALP',    NULL, NULL, N'Alkaline Phosphatase (ALP)', 1, N'0', N'IFCC (AMP buffer)'),
 ('GGT',    NULL, NULL, N'Gamma Glutamyl Transferase (GGT)', 1, N'0', N'IFCC (Szasz)'),
 ('H_PROT', NULL, NULL, N'SERUM PROTEINS', 1, NULL, NULL),
 ('TP',     NULL, NULL, N'Total Protein', 1, N'2', N'Biuret'),
 ('ALB',    NULL, NULL, N'Albumin', 1, N'2', N'Bromocresol Green (BCG)'),
 ('GLOB',   NULL, NULL, N'Globulin', 1, N'2', N'Calculated'),
 ('AGR',    NULL, NULL, N'A/G Ratio', 1, N'2', N'Calculated'),
 -- KFT / RFT
 ('UREA',   NULL, NULL, N'Blood Urea', 1, N'1', N'Urease-GLDH'),
 ('BUN',    NULL, NULL, N'Blood Urea Nitrogen (BUN)', 1, N'1', N'Calculated'),
 ('CREA',   NULL, NULL, N'Serum Creatinine', 1, N'2', N'Jaffe Kinetic'),
 ('BUNCR',  NULL, NULL, N'BUN / Creatinine Ratio', 1, N'1', N'Calculated'),
 ('EGFR',   NULL, NULL, N'eGFR (CKD-EPI 2021)', 1, N'0', N'Calculated'),
 ('URIC',   NULL, NULL, N'Serum Uric Acid', 1, N'1', N'Uricase-POD'),
 ('CA',     NULL, NULL, N'Serum Calcium', 1, N'1', N'Arsenazo III'),
 ('PHOS',   NULL, NULL, N'Serum Phosphorus', 1, N'1', N'Phosphomolybdate UV'),
 ('H_ELEC', NULL, NULL, N'SERUM ELECTROLYTES', 1, NULL, NULL),
 ('NA',     NULL, NULL, N'Serum Sodium (Na+)', 1, N'0', N'Ion Selective Electrode (ISE)'),
 ('K',      NULL, NULL, N'Serum Potassium (K+)', 1, N'1', N'Ion Selective Electrode (ISE)'),
 ('CL',     NULL, NULL, N'Serum Chloride (Cl-)', 1, N'0', N'Ion Selective Electrode (ISE)'),
 -- Urine complete
 ('H_UPHY', NULL, NULL, N'PHYSICAL EXAMINATION', 1, NULL, NULL),
 ('U_QTY',  NULL, NULL, N'Urine Quantity', 1, NULL, N'Visual'),
 ('U_COL',  NULL, NULL, N'Urine Colour', 1, NULL, N'Visual'),
 ('U_APP',  NULL, NULL, N'Urine Appearance', 1, NULL, N'Visual'),
 ('U_SG',   NULL, NULL, N'Urine Specific Gravity', 1, NULL, N'Dipstick (Reflectance)'),
 ('H_UCHE', NULL, NULL, N'CHEMICAL EXAMINATION', 1, NULL, NULL),
 ('U_PH',   NULL, NULL, N'Urine pH', 1, N'1', N'Dipstick (Reflectance)'),
 ('U_PRO',  NULL, NULL, N'Urine Protein (Albumin)', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_GLU',  NULL, NULL, N'Urine Glucose (Sugar)', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_KET',  NULL, NULL, N'Urine Ketone Bodies', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_BLD',  NULL, NULL, N'Urine Blood', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_BS',   NULL, NULL, N'Urine Bile Salts', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_BP',   NULL, NULL, N'Urine Bile Pigments (Bilirubin)', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_UBG',  NULL, NULL, N'Urine Urobilinogen', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_NIT',  NULL, NULL, N'Urine Nitrite', 1, NULL, N'Dipstick (Reflectance)'),
 ('U_LE',   NULL, NULL, N'Urine Leucocyte Esterase', 1, NULL, N'Dipstick (Reflectance)'),
 ('H_UMIC', NULL, NULL, N'MICROSCOPIC EXAMINATION', 1, NULL, NULL),
 ('U_PUS',  NULL, NULL, N'Urine Pus Cells', 1, NULL, N'Microscopy'),
 ('U_RBC',  NULL, NULL, N'Urine Red Blood Cells', 1, NULL, N'Microscopy'),
 ('U_EPI',  NULL, NULL, N'Urine Epithelial Cells', 1, NULL, N'Microscopy'),
 ('U_CAST', NULL, NULL, N'Urine Casts', 1, NULL, N'Microscopy'),
 ('U_CRY',  NULL, NULL, N'Urine Crystals', 1, NULL, N'Microscopy'),
 ('U_BAC',  NULL, NULL, N'Urine Bacteria', 1, NULL, N'Microscopy'),
 ('U_YST',  NULL, NULL, N'Urine Yeast Cells', 1, NULL, N'Microscopy'),
 ('U_OTH',  NULL, NULL, N'Urine Other Findings', 2, NULL, N'Microscopy'),
 -- PT & INR
 ('PT_TEST',NULL, NULL, N'Prothrombin Time (Patient)', 1, N'1', N'Clotting (Photo-optical)'),
 ('PT_CTRL',NULL, NULL, N'Prothrombin Time (Control / MNPT)', 1, N'1', N'Clotting (Photo-optical)'),
 ('PT_ISI', NULL, NULL, N'ISI of Thromboplastin Reagent', 1, N'2', NULL),
 ('INR',    NULL, NULL, N'INR (International Normalised Ratio)', 1, N'2', N'Calculated'),
 -- HbA1c
 ('A1C',    NULL, NULL, N'Glycated Haemoglobin (HbA1c)', 1, N'1', N'HPLC'),
 ('EAG',    NULL, NULL, N'Estimated Average Glucose (eAG)', 1, N'0', N'Calculated'),
 -- Thyroid
 ('T3',     NULL, NULL, N'Total T3 (Triiodothyronine)', 1, N'2', N'CLIA'),
 ('T4',     NULL, NULL, N'Total T4 (Thyroxine)', 1, N'2', N'CLIA'),
 ('TSH',    NULL, NULL, N'TSH (Thyroid Stimulating Hormone)', 1, N'2', N'CLIA'),
 -- Malaria smear / rapid
 ('MP_SM',  NULL, NULL, N'Malarial Parasite (Thick & Thin Smear)', 1, NULL, N'Microscopy'),
 ('MP_SP',  NULL, NULL, N'Malarial Parasite Species', 1, NULL, N'Microscopy'),
 ('MP_ST',  NULL, NULL, N'Malarial Parasite Stage Seen', 1, NULL, N'Microscopy'),
 ('MP_DEN', NULL, NULL, N'Malarial Parasite Density', 1, N'2', N'Microscopy'),
 ('MAL_PV', NULL, NULL, N'Plasmodium vivax Antigen (pLDH)', 1, NULL, N'Immunochromatography'),
 ('MAL_PF', NULL, NULL, N'Plasmodium falciparum Antigen (HRP-2)', 1, NULL, N'Immunochromatography');

-- 2a. the existing row by id when it still carries its old (or the new) name, else by the new name, else by the old name
UPDATE o SET Id = om.ObservationId
FROM #Obs o JOIN ObservationMaster om ON om.ObservationId = o.ExistingId AND om.HospId = @HospId
 AND LTRIM(RTRIM(om.ObservationName)) IN (o.OldName, o.Name);

UPDATE o SET Id = (SELECT MIN(om.ObservationId) FROM ObservationMaster om WHERE om.HospId = @HospId AND LTRIM(RTRIM(om.ObservationName)) = o.Name)
FROM #Obs o WHERE o.Id IS NULL;

UPDATE o SET Id = (SELECT MIN(om.ObservationId) FROM ObservationMaster om WHERE om.HospId = @HospId AND LTRIM(RTRIM(om.ObservationName)) = o.OldName)
FROM #Obs o WHERE o.Id IS NULL AND o.OldName IS NOT NULL;

UPDATE om SET
  ObservationName = o.Name, MethodId = ISNULL(m.MethodId, 0), FieldTypeId = o.FieldTypeId,
  FieldType = CASE o.FieldTypeId WHEN 1 THEN N'Numeric' WHEN 2 THEN N'Single Line' ELSE N'Multi Line' END,
  RoundUp = o.RoundUp, IsActive = 1, LastModifiedBy = @UserId, LastModifiedOn = @Now, IpAddress = @Ip
FROM ObservationMaster om JOIN #Obs o ON o.Id = om.ObservationId LEFT JOIN #Method m ON m.Method = o.Method
WHERE ISNULL(om.ObservationName, N'') <> o.Name OR ISNULL(om.MethodId, -1) <> ISNULL(m.MethodId, 0)
   OR ISNULL(om.FieldTypeId, -1) <> o.FieldTypeId OR ISNULL(om.RoundUp, N'~') <> ISNULL(o.RoundUp, N'~') OR om.IsActive <> 1;

DECLARE @newObs TABLE (ObservationId int, ObservationName nvarchar(500));
INSERT ObservationMaster (HospId, ObservationName, Prefix, Suffix, MethodId, IsActive, CreatedBy, CreatedOn, IpAddress,
                          ShowInDischargeSummary, FieldType, FieldTypeId, RoundUp)
OUTPUT inserted.ObservationId, inserted.ObservationName INTO @newObs
SELECT @HospId, o.Name, NULL, NULL, ISNULL(m.MethodId, 0), 1, @UserId, @Now, @Ip,
       CASE WHEN o.Code LIKE 'H[_]%' THEN 0 ELSE 1 END,
       CASE o.FieldTypeId WHEN 1 THEN N'Numeric' WHEN 2 THEN N'Single Line' ELSE N'Multi Line' END, o.FieldTypeId, o.RoundUp
FROM #Obs o LEFT JOIN #Method m ON m.Method = o.Method
WHERE o.Id IS NULL;

UPDATE o SET Id = n.ObservationId FROM #Obs o JOIN @newObs n ON n.ObservationName = o.Name WHERE o.Id IS NULL;

IF EXISTS (SELECT 1 FROM #Obs WHERE Id IS NULL) THROW 50001, 'An observation could not be resolved', 1;

/* ---------------------------------------------------------------- 3. reference ranges (per observation) */
IF OBJECT_ID('tempdb..#Rng') IS NOT NULL DROP TABLE #Rng;
CREATE TABLE #Rng (Code varchar(12), Gender char(1), FromAge int, ToAge int, MinV decimal(16,2) NULL, MaxV decimal(16,2) NULL,
                   Unit nvarchar(100) NULL, Display nvarchar(200) NULL);
INSERT #Rng VALUES
 -- CBC
 ('HB','B',0,30,14.0,22.0,N'g/dL',N'14.0 - 22.0'), ('HB','B',31,364,9.5,13.5,N'g/dL',N'9.5 - 13.5'), ('HB','B',365,4379,11.5,15.5,N'g/dL',N'11.5 - 15.5'),
 ('HB','M',4380,99999,13.0,17.0,N'g/dL',N'13.0 - 17.0'), ('HB','F',4380,99999,12.0,15.0,N'g/dL',N'12.0 - 15.0'),
 ('RBC','B',0,30,4.00,6.60,N'million/cumm',N'4.00 - 6.60'), ('RBC','B',31,364,3.80,5.40,N'million/cumm',N'3.80 - 5.40'), ('RBC','B',365,4379,4.00,5.20,N'million/cumm',N'4.00 - 5.20'),
 ('RBC','M',4380,99999,4.50,5.50,N'million/cumm',N'4.50 - 5.50'), ('RBC','F',4380,99999,3.80,4.80,N'million/cumm',N'3.80 - 4.80'),
 ('PCV','B',0,30,42.0,65.0,N'%',N'42.0 - 65.0'), ('PCV','B',31,364,30.0,41.0,N'%',N'30.0 - 41.0'), ('PCV','B',365,4379,35.0,45.0,N'%',N'35.0 - 45.0'),
 ('PCV','M',4380,99999,40.0,50.0,N'%',N'40.0 - 50.0'), ('PCV','F',4380,99999,36.0,46.0,N'%',N'36.0 - 46.0'),
 ('MCV','B',0,30,95.0,121.0,N'fL',N'95.0 - 121.0'), ('MCV','B',31,364,70.0,90.0,N'fL',N'70.0 - 90.0'), ('MCV','B',365,4379,77.0,95.0,N'fL',N'77.0 - 95.0'), ('MCV','B',4380,99999,83.0,101.0,N'fL',N'83.0 - 101.0'),
 ('MCH','B',0,30,31.0,37.0,N'pg',N'31.0 - 37.0'), ('MCH','B',31,364,23.0,31.0,N'pg',N'23.0 - 31.0'), ('MCH','B',365,4379,25.0,33.0,N'pg',N'25.0 - 33.0'), ('MCH','B',4380,99999,27.0,32.0,N'pg',N'27.0 - 32.0'),
 ('MCHC','B',0,99999,31.5,34.5,N'g/dL',N'31.5 - 34.5'),
 ('RDW','B',0,99999,11.6,14.0,N'%',N'11.6 - 14.0'),
 ('TLC','B',0,30,9000,30000,N'cells/cumm',N'9000 - 30000'), ('TLC','B',31,364,6000,17500,N'cells/cumm',N'6000 - 17500'),
 ('TLC','B',365,4379,4500,13500,N'cells/cumm',N'4500 - 13500'), ('TLC','B',4380,99999,4000,10000,N'cells/cumm',N'4000 - 10000'),
 ('NEU','B',4380,99999,40,80,N'%',N'40 - 80'), ('LYM','B',4380,99999,20,40,N'%',N'20 - 40'), ('MON','B',4380,99999,2,10,N'%',N'2 - 10'),
 ('EOS','B',4380,99999,1,6,N'%',N'1 - 6'), ('BAS','B',4380,99999,0,2,N'%',N'0 - 2'),
 ('ANC','B',4380,99999,2000,7000,N'cells/cumm',N'2000 - 7000'), ('ALC','B',4380,99999,1000,3000,N'cells/cumm',N'1000 - 3000'),
 ('AMC','B',4380,99999,200,1000,N'cells/cumm',N'200 - 1000'), ('AEC','B',4380,99999,20,500,N'cells/cumm',N'20 - 500'), ('ABC','B',4380,99999,20,100,N'cells/cumm',N'20 - 100'),
 ('PLT','B',0,4379,150000,450000,N'/cumm',N'150000 - 450000'), ('PLT','B',4380,99999,150000,410000,N'/cumm',N'150000 - 410000'),
 ('MPV','B',0,99999,6.5,12.0,N'fL',N'6.5 - 12.0'), ('PDW','B',0,99999,9.0,17.0,N'fL',N'9.0 - 17.0'), ('PCT','B',0,99999,0.17,0.35,N'%',N'0.17 - 0.35'),
 ('PLCC','B',0,99999,30,90,N'10^3/uL',N'30 - 90'), ('PLCR','B',0,99999,13.0,43.0,N'%',N'13.0 - 43.0'),
 -- LFT
 ('TBIL','B',31,99999,0.30,1.20,N'mg/dL',N'0.30 - 1.20'), ('DBIL','B',31,99999,0.00,0.30,N'mg/dL',N'0.00 - 0.30'), ('IBIL','B',31,99999,0.10,1.00,N'mg/dL',N'0.10 - 1.00'),
 ('AST','B',365,4379,0,50,N'U/L',N'Up to 50'), ('AST','M',4380,99999,0,50,N'U/L',N'Up to 50'), ('AST','F',4380,99999,0,35,N'U/L',N'Up to 35'),
 ('ALT','B',365,4379,0,40,N'U/L',N'Up to 40'), ('ALT','M',4380,99999,0,50,N'U/L',N'Up to 50'), ('ALT','F',4380,99999,0,35,N'U/L',N'Up to 35'),
 ('ALP','B',365,4379,0,420,N'U/L',N'Up to 420'), ('ALP','M',4380,99999,40,129,N'U/L',N'40 - 129'), ('ALP','F',4380,99999,35,104,N'U/L',N'35 - 104'),
 ('GGT','M',4380,99999,10,71,N'U/L',N'10 - 71'), ('GGT','F',4380,99999,6,42,N'U/L',N'6 - 42'),
 ('TP','B',365,99999,6.40,8.30,N'g/dL',N'6.40 - 8.30'), ('ALB','B',365,99999,3.50,5.20,N'g/dL',N'3.50 - 5.20'),
 ('GLOB','B',365,99999,2.00,3.50,N'g/dL',N'2.00 - 3.50'), ('AGR','B',365,99999,1.00,2.10,NULL,N'1.00 - 2.10'),
 -- KFT / RFT
 ('UREA','B',365,4379,11,36,N'mg/dL',N'11 - 36'), ('UREA','B',4380,99999,17,43,N'mg/dL',N'17 - 43'),
 ('BUN','B',365,4379,5.0,17.0,N'mg/dL',N'5.0 - 17.0'), ('BUN','B',4380,99999,8.0,20.0,N'mg/dL',N'8.0 - 20.0'),
 ('CREA','B',31,364,0.20,0.40,N'mg/dL',N'0.20 - 0.40'), ('CREA','B',365,4379,0.30,0.70,N'mg/dL',N'0.30 - 0.70'),
 ('CREA','M',4380,99999,0.70,1.30,N'mg/dL',N'0.70 - 1.30'), ('CREA','F',4380,99999,0.60,1.10,N'mg/dL',N'0.60 - 1.10'),
 ('BUNCR','B',4380,99999,10.0,20.0,NULL,N'10 - 20'),
 ('EGFR','B',6570,99999,90,NULL,N'mL/min/1.73 m2',N'>= 90'),
 ('URIC','B',365,4379,2.0,5.5,N'mg/dL',N'2.0 - 5.5'), ('URIC','M',4380,99999,3.5,7.2,N'mg/dL',N'3.5 - 7.2'), ('URIC','F',4380,99999,2.6,6.0,N'mg/dL',N'2.6 - 6.0'),
 ('CA','B',365,4379,8.8,10.8,N'mg/dL',N'8.8 - 10.8'), ('CA','B',4380,99999,8.6,10.2,N'mg/dL',N'8.6 - 10.2'),
 ('PHOS','B',365,4379,4.0,7.0,N'mg/dL',N'4.0 - 7.0'), ('PHOS','B',4380,99999,2.5,4.5,N'mg/dL',N'2.5 - 4.5'),
 ('NA','B',31,99999,136,145,N'mmol/L',N'136 - 145'), ('K','B',31,99999,3.5,5.1,N'mmol/L',N'3.5 - 5.1'), ('CL','B',31,99999,98,107,N'mmol/L',N'98 - 107'),
 -- Urine (qualitative rows show the expected finding; numeric pus cell / RBC counts are flagged)
 ('U_QTY','B',0,99999,NULL,NULL,N'mL',NULL),
 ('U_COL','B',0,99999,NULL,NULL,NULL,N'Pale Yellow'), ('U_APP','B',0,99999,NULL,NULL,NULL,N'Clear'),
 ('U_SG','B',0,99999,NULL,NULL,NULL,N'1.005 - 1.030'), ('U_PH','B',0,99999,5.0,8.0,NULL,N'5.0 - 8.0'),
 ('U_PRO','B',0,99999,NULL,NULL,NULL,N'Nil'), ('U_GLU','B',0,99999,NULL,NULL,NULL,N'Nil'), ('U_KET','B',0,99999,NULL,NULL,NULL,N'Negative'),
 ('U_BLD','B',0,99999,NULL,NULL,NULL,N'Negative'), ('U_BS','B',0,99999,NULL,NULL,NULL,N'Absent'), ('U_BP','B',0,99999,NULL,NULL,NULL,N'Absent'),
 ('U_UBG','B',0,99999,NULL,NULL,NULL,N'Normal'), ('U_NIT','B',0,99999,NULL,NULL,NULL,N'Negative'), ('U_LE','B',0,99999,NULL,NULL,NULL,N'Negative'),
 ('U_PUS','B',0,99999,0,5,N'/hpf',N'0 - 5'), ('U_RBC','B',0,99999,0,2,N'/hpf',N'0 - 2'), ('U_EPI','B',0,99999,0,5,N'/hpf',N'0 - 5'),
 ('U_CAST','B',0,99999,NULL,NULL,N'/lpf',N'Nil'), ('U_CRY','B',0,99999,NULL,NULL,N'/hpf',N'Nil'),
 ('U_BAC','B',0,99999,NULL,NULL,NULL,N'Absent'), ('U_YST','B',0,99999,NULL,NULL,NULL,N'Absent'),
 -- PT & INR
 ('PT_TEST','B',0,99999,11.0,13.5,N'seconds',N'11.0 - 13.5'), ('PT_CTRL','B',0,99999,NULL,NULL,N'seconds',NULL),
 ('INR','B',0,99999,0.80,1.20,NULL,N'0.80 - 1.20'),
 -- HbA1c (ADA 2025)
 ('A1C','B',0,99999,4.0,5.6,N'%',N'4.0 - 5.6 (Non-diabetic)'), ('EAG','B',0,99999,68,114,N'mg/dL',N'68 - 114'),
 -- Thyroid (adult, non-pregnant)
 ('T3','B',4380,99999,0.80,2.00,N'ng/mL',N'0.80 - 2.00'), ('T4','B',4380,99999,5.10,14.10,N'ug/dL',N'5.10 - 14.10'),
 ('TSH','B',4380,99999,0.27,4.20,N'uIU/mL',N'0.27 - 4.20'),
 -- Malaria
 ('MP_SM','B',0,99999,NULL,NULL,NULL,N'Not Seen'), ('MP_DEN','B',0,99999,NULL,NULL,N'% RBCs parasitised',NULL),
 ('MAL_PV','B',0,99999,NULL,NULL,NULL,N'Negative'), ('MAL_PF','B',0,99999,NULL,NULL,NULL,N'Negative');

-- one row per patient: no two rows of an observation may overlap for the same gender (B overlaps M and F)
IF EXISTS (SELECT 1 FROM #Rng a JOIN #Rng b ON a.Code = b.Code AND (a.Gender <> b.Gender OR a.FromAge <> b.FromAge)
           AND (a.Gender = b.Gender OR 'B' IN (a.Gender, b.Gender)) AND a.FromAge <= b.ToAge AND b.FromAge <= a.ToAge)
  THROW 50002, 'Overlapping reference ranges in the script data', 1;

DELETE r FROM InvastigationObservationRangeMaster r WHERE r.ObservationId IN (SELECT Id FROM #Obs);

INSERT InvastigationObservationRangeMaster (ObservationId, Gender, FromAge, ToAge, IsActive, MinValue, MaxValue, Unit, DisplayValue,
                                            CreatedBy, CreatedDate, IpAddress, DefaultValue)
SELECT o.Id, r.Gender, CAST(r.FromAge AS nvarchar(20)), CAST(r.ToAge AS nvarchar(20)), 1, r.MinV, r.MaxV, r.Unit, r.Display,
       @UserId, @Now, @Ip, NULL
FROM #Rng r JOIN #Obs o ON o.Code = r.Code;

/* ---------------------------------------------------------------- 4. option lists (LOVs) */
IF OBJECT_ID('tempdb..#Lov') IS NOT NULL DROP TABLE #Lov;
CREATE TABLE #Lov (Code varchar(12), Seq int, LOVName nvarchar(200));
INSERT #Lov VALUES
 ('U_COL',1,N'Pale Yellow'),('U_COL',2,N'Yellow'),('U_COL',3,N'Dark Yellow'),('U_COL',4,N'Amber'),('U_COL',5,N'Colourless'),('U_COL',6,N'Reddish'),('U_COL',7,N'Brown'),
 ('U_APP',1,N'Clear'),('U_APP',2,N'Slightly Turbid'),('U_APP',3,N'Turbid'),('U_APP',4,N'Hazy'),
 ('U_PRO',1,N'Nil'),('U_PRO',2,N'Trace'),('U_PRO',3,N'1+'),('U_PRO',4,N'2+'),('U_PRO',5,N'3+'),('U_PRO',6,N'4+'),
 ('U_GLU',1,N'Nil'),('U_GLU',2,N'Trace'),('U_GLU',3,N'1+'),('U_GLU',4,N'2+'),('U_GLU',5,N'3+'),('U_GLU',6,N'4+'),
 ('U_KET',1,N'Negative'),('U_KET',2,N'Trace'),('U_KET',3,N'1+'),('U_KET',4,N'2+'),('U_KET',5,N'3+'),
 ('U_BLD',1,N'Negative'),('U_BLD',2,N'Trace'),('U_BLD',3,N'1+'),('U_BLD',4,N'2+'),('U_BLD',5,N'3+'),
 ('U_BS',1,N'Absent'),('U_BS',2,N'Present'),
 ('U_BP',1,N'Absent'),('U_BP',2,N'Present'),
 ('U_UBG',1,N'Normal'),('U_UBG',2,N'Increased'),
 ('U_NIT',1,N'Negative'),('U_NIT',2,N'Positive'),
 ('U_LE',1,N'Negative'),('U_LE',2,N'Trace'),('U_LE',3,N'1+'),('U_LE',4,N'2+'),('U_LE',5,N'3+'),
 ('U_PUS',1,N'Nil'),('U_PUS',2,N'0 - 1'),('U_PUS',3,N'1 - 2'),('U_PUS',4,N'2 - 4'),('U_PUS',5,N'4 - 6'),('U_PUS',6,N'6 - 8'),('U_PUS',7,N'8 - 10'),('U_PUS',8,N'10 - 15'),('U_PUS',9,N'15 - 20'),('U_PUS',10,N'Plenty'),
 ('U_RBC',1,N'Nil'),('U_RBC',2,N'0 - 1'),('U_RBC',3,N'1 - 2'),('U_RBC',4,N'2 - 4'),('U_RBC',5,N'4 - 6'),('U_RBC',6,N'8 - 10'),('U_RBC',7,N'Plenty'),
 ('U_EPI',1,N'Nil'),('U_EPI',2,N'Occasional'),('U_EPI',3,N'1 - 2'),('U_EPI',4,N'2 - 4'),('U_EPI',5,N'4 - 6'),('U_EPI',6,N'Plenty'),
 ('U_CAST',1,N'Nil'),('U_CAST',2,N'Hyaline casts seen'),('U_CAST',3,N'Granular casts seen'),('U_CAST',4,N'RBC casts seen'),('U_CAST',5,N'WBC casts seen'),
 ('U_CRY',1,N'Nil'),('U_CRY',2,N'Calcium oxalate'),('U_CRY',3,N'Uric acid'),('U_CRY',4,N'Amorphous urates'),('U_CRY',5,N'Amorphous phosphates'),('U_CRY',6,N'Triple phosphate'),
 ('U_BAC',1,N'Absent'),('U_BAC',2,N'Present'),
 ('U_YST',1,N'Absent'),('U_YST',2,N'Present'),
 ('MP_SM',1,N'Not Seen'),('MP_SM',2,N'Seen'),
 ('MP_SP',1,N'Not Applicable'),('MP_SP',2,N'Plasmodium vivax'),('MP_SP',3,N'Plasmodium falciparum'),('MP_SP',4,N'Mixed (P. vivax + P. falciparum)'),('MP_SP',5,N'Plasmodium malariae'),('MP_SP',6,N'Plasmodium ovale'),
 ('MP_ST',1,N'Not Applicable'),('MP_ST',2,N'Ring forms'),('MP_ST',3,N'Trophozoites'),('MP_ST',4,N'Schizonts'),('MP_ST',5,N'Gametocytes'),('MP_ST',6,N'Ring forms and gametocytes'),
 ('MAL_PV',1,N'Negative'),('MAL_PV',2,N'Positive'),('MAL_PV',3,N'Invalid (control line absent)'),
 ('MAL_PF',1,N'Negative'),('MAL_PF',2,N'Positive'),('MAL_PF',3,N'Invalid (control line absent)');

INSERT ObservationListOfValuesMaster (LOVName, IsActive, CreatedBy, CreatedOn, IpAddress)
SELECT DISTINCT l.LOVName, 1, @UserId, @Now, @Ip FROM #Lov l
WHERE NOT EXISTS (SELECT 1 FROM ObservationListOfValuesMaster x WHERE x.IsActive = 1 AND LTRIM(RTRIM(x.LOVName)) = l.LOVName);

DELETE m FROM ObservationCommentLOVsMapping m WHERE m.TypeId = 2 AND m.ObservationId IN (SELECT Id FROM #Obs);

-- ordered so the dropdown lists the normal finding first
INSERT ObservationCommentLOVsMapping (TypeId, Type, ObservationId, ItemId, CreatedBy, CreatedOn, IpAddress)
SELECT 2, N'LOVs', o.Id,
       (SELECT MIN(x.LOVId) FROM ObservationListOfValuesMaster x WHERE x.IsActive = 1 AND LTRIM(RTRIM(x.LOVName)) = l.LOVName),
       @UserId, @Now, @Ip
FROM #Lov l JOIN #Obs o ON o.Code = l.Code
ORDER BY o.Id, l.Seq;

/* ---------------------------------------------------------------- 5. test - observation mapping */
IF OBJECT_ID('tempdb..#PanelMap') IS NOT NULL DROP TABLE #PanelMap;
CREATE TABLE #PanelMap (Panel varchar(10), Seq int, Code varchar(12), IsMandatory bit);
INSERT #PanelMap VALUES
 ('CBC',1,'H_RBC',0),('CBC',2,'HB',1),('CBC',3,'RBC',1),('CBC',4,'PCV',1),('CBC',5,'MCV',0),('CBC',6,'MCH',0),('CBC',7,'MCHC',0),('CBC',8,'RDW',0),
 ('CBC',9,'H_WBC',0),('CBC',10,'TLC',1),
 ('CBC',11,'H_DLC',0),('CBC',12,'NEU',1),('CBC',13,'LYM',1),('CBC',14,'MON',1),('CBC',15,'EOS',1),('CBC',16,'BAS',1),
 ('CBC',17,'H_ABS',0),('CBC',18,'ANC',0),('CBC',19,'ALC',0),('CBC',20,'AMC',0),('CBC',21,'AEC',0),('CBC',22,'ABC',0),
 ('CBC',23,'H_PLT',0),('CBC',24,'PLT',1),('CBC',25,'MPV',0),('CBC',26,'PDW',0),('CBC',27,'PCT',0),('CBC',28,'PLCC',0),('CBC',29,'PLCR',0),
 ('LFT',1,'H_BIL',0),('LFT',2,'TBIL',1),('LFT',3,'DBIL',1),('LFT',4,'IBIL',0),
 ('LFT',5,'H_ENZ',0),('LFT',6,'AST',1),('LFT',7,'ALT',1),('LFT',8,'ALP',1),('LFT',9,'GGT',0),
 ('LFT',10,'H_PROT',0),('LFT',11,'TP',1),('LFT',12,'ALB',1),('LFT',13,'GLOB',0),('LFT',14,'AGR',0),
 ('KFT',1,'UREA',1),('KFT',2,'BUN',0),('KFT',3,'CREA',1),('KFT',4,'BUNCR',0),('KFT',5,'EGFR',0),('KFT',6,'URIC',0),('KFT',7,'CA',0),('KFT',8,'PHOS',0),
 ('KFT',9,'H_ELEC',0),('KFT',10,'NA',1),('KFT',11,'K',1),('KFT',12,'CL',0),
 ('HB',1,'HB',1),
 ('URINE',1,'H_UPHY',0),('URINE',2,'U_QTY',0),('URINE',3,'U_COL',1),('URINE',4,'U_APP',1),('URINE',5,'U_SG',0),
 ('URINE',6,'H_UCHE',0),('URINE',7,'U_PH',0),('URINE',8,'U_PRO',1),('URINE',9,'U_GLU',1),('URINE',10,'U_KET',0),('URINE',11,'U_BLD',0),
 ('URINE',12,'U_BS',0),('URINE',13,'U_BP',0),('URINE',14,'U_UBG',0),('URINE',15,'U_NIT',0),('URINE',16,'U_LE',0),
 ('URINE',17,'H_UMIC',0),('URINE',18,'U_PUS',1),('URINE',19,'U_RBC',1),('URINE',20,'U_EPI',1),('URINE',21,'U_CAST',0),('URINE',22,'U_CRY',0),
 ('URINE',23,'U_BAC',0),('URINE',24,'U_YST',0),('URINE',25,'U_OTH',0),
 ('PT',1,'PT_TEST',1),('PT',2,'PT_CTRL',1),('PT',3,'PT_ISI',1),('PT',4,'INR',0),
 ('A1C',1,'A1C',1),('A1C',2,'EAG',0),
 ('THY',1,'T3',1),('THY',2,'T4',1),('THY',3,'TSH',1),
 ('MP',1,'MP_SM',1),('MP',2,'MP_SP',0),('MP',3,'MP_ST',0),('MP',4,'MP_DEN',0),
 ('MAL',1,'MAL_PV',1),('MAL',2,'MAL_PF',1);

IF OBJECT_ID('tempdb..#TargetMap') IS NOT NULL DROP TABLE #TargetMap;
SELECT i.InvId, p.Seq, o.Id AS ObservationId,
       CASE WHEN o.Code LIKE 'H[_]%' THEN 1 ELSE 0 END AS IsHeader,
       CASE WHEN o.Code LIKE 'H[_]%' THEN 1 ELSE 0 END AS IsBold,
       CAST(p.IsMandatory AS int) AS IsMandatory
INTO #TargetMap
FROM #Inv i JOIN #PanelMap p ON p.Panel = i.Panel JOIN #Obs o ON o.Code = p.Code
WHERE i.IsValid = 1 AND i.IsTabular = 1;

IF OBJECT_ID('tempdb..#MapToRedo') IS NOT NULL DROP TABLE #MapToRedo;
SELECT t.InvId INTO #MapToRedo
FROM (SELECT InvId, STRING_AGG(CONCAT(ObservationId, ':', IsHeader, IsBold, IsMandatory), ',') WITHIN GROUP (ORDER BY Seq) AS Sig
      FROM #TargetMap GROUP BY InvId) t
LEFT JOIN (SELECT InvastigationId AS InvId,
                  STRING_AGG(CONCAT(ObservationId, ':', CAST(ISNULL(IsHeader,0) AS int), CAST(ISNULL(IsBold,0) AS int), CAST(ISNULL(IsMandatory,0) AS int)), ',')
                    WITHIN GROUP (ORDER BY MappingId) AS Sig
           FROM InvastigationObservationMapping WHERE IsActive = 1 GROUP BY InvastigationId) c ON c.InvId = t.InvId
WHERE ISNULL(c.Sig, '') <> t.Sig;

UPDATE m SET IsActive = 0, LastModifiedBy = @UserId, LastModifiedOn = @Now, IPAddress = @Ip
FROM InvastigationObservationMapping m JOIN #MapToRedo r ON r.InvId = m.InvastigationId
WHERE m.IsActive = 1;

INSERT InvastigationObservationMapping (InvastigationId, ObservationId, IsHeader, IsBold, IsUnderLine, IsActive, CreatedBy, CreatedDate, IPAddress, HospId, IsMandatory)
SELECT t.InvId, t.ObservationId, t.IsHeader, t.IsBold, 0, 1, @UserId, @Now, @Ip, @HospId, t.IsMandatory
FROM #TargetMap t JOIN #MapToRedo r ON r.InvId = t.InvId
ORDER BY t.InvId, t.Seq;

/* ---------------------------------------------------------------- 6a. formulas ({CODE} = obs(<id>)) */
IF OBJECT_ID('tempdb..#PanelFx') IS NOT NULL DROP TABLE #PanelFx;
CREATE TABLE #PanelFx (Panel varchar(10), Code varchar(12), Gender char(1), Expr nvarchar(4000), ExprText nvarchar(4000) NULL, Dp int);
INSERT #PanelFx (Panel, Code, Gender, Expr, Dp) VALUES
 ('CBC','MCV','B',N'{PCV}/{RBC}*10',1),
 ('CBC','MCH','B',N'{HB}/{RBC}*10',1),
 ('CBC','MCHC','B',N'{HB}/{PCV}*100',1),
 ('CBC','ANC','B',N'{TLC}*{NEU}/100',0),
 ('CBC','ALC','B',N'{TLC}*{LYM}/100',0),
 ('CBC','AMC','B',N'{TLC}*{MON}/100',0),
 ('CBC','AEC','B',N'{TLC}*{EOS}/100',0),
 ('CBC','ABC','B',N'{TLC}*{BAS}/100',0),
 ('LFT','IBIL','B',N'{TBIL}-{DBIL}',2),
 ('LFT','GLOB','B',N'{TP}-{ALB}',2),
 ('LFT','AGR','B',N'{ALB}/({TP}-{ALB})',2),
 ('KFT','BUN','B',N'{UREA}/2.14',1),
 ('KFT','BUNCR','B',N'({UREA}/2.14)/{CREA}',1),
 -- CKD-EPI 2021 (race-free); valid from 18 years, its range row starts at 6570 days
 ('KFT','EGFR','M',N'142*pow(min({CREA}/0.9,1),-0.302)*pow(max({CREA}/0.9,1),-1.2)*pow(0.9938,AGE)',0),
 ('KFT','EGFR','F',N'142*pow(min({CREA}/0.7,1),-0.241)*pow(max({CREA}/0.7,1),-1.2)*pow(0.9938,AGE)*1.012',0),
 ('PT','INR','B',N'pow({PT_TEST}/{PT_CTRL},{PT_ISI})',2),
 ('A1C','EAG','B',N'28.7*{A1C}-46.7',0);

UPDATE #PanelFx SET ExprText = Expr;
DECLARE @code varchar(12), @oid int, @oname nvarchar(500);
DECLARE obs_cur CURSOR LOCAL FAST_FORWARD FOR SELECT Code, Id, Name FROM #Obs ORDER BY LEN(Code) DESC;
OPEN obs_cur;
FETCH NEXT FROM obs_cur INTO @code, @oid, @oname;
WHILE @@FETCH_STATUS = 0
BEGIN
  UPDATE #PanelFx SET Expr = REPLACE(Expr, '{' + @code + '}', 'obs(' + CAST(@oid AS varchar(12)) + ')'),
                      ExprText = REPLACE(ExprText, '{' + @code + '}', '[' + @oname + ']')
  WHERE Expr LIKE '%{' + @code + '}%';
  FETCH NEXT FROM obs_cur INTO @code, @oid, @oname;
END
CLOSE obs_cur; DEALLOCATE obs_cur;

IF EXISTS (SELECT 1 FROM #PanelFx WHERE Expr LIKE '%{%') THROW 50003, 'A formula token was not replaced', 1;

IF OBJECT_ID('tempdb..#TargetFx') IS NOT NULL DROP TABLE #TargetFx;
SELECT i.InvId, o.Id AS ObservationId, f.Gender, f.Expr, f.ExprText, f.Dp
INTO #TargetFx
FROM #Inv i JOIN #PanelFx f ON f.Panel = i.Panel JOIN #Obs o ON o.Code = f.Code
WHERE i.IsValid = 1 AND i.IsTabular = 1;

-- other active formulas of these tests (e.g. the Formula Master test rows on 40754) are switched off
UPDATE lf SET IsActive = 0, LastModifiedBy = @UserId, LastModifiedOn = @Now, IpAddress = @Ip
FROM LabInvestigationFormula lf
WHERE lf.IsActive = 1 AND lf.InvestigationId IN (SELECT InvId FROM #Inv WHERE IsValid = 1 AND IsTabular = 1)
  AND NOT EXISTS (SELECT 1 FROM #TargetFx t WHERE t.InvId = lf.InvestigationId AND t.ObservationId = lf.ObservationId AND t.Gender = lf.Gender);

UPDATE lf SET Expression = t.Expr, FormulaText = t.ExprText, DecimalPlaces = t.Dp, IsActive = 1,
              LastModifiedBy = @UserId, LastModifiedOn = @Now, IpAddress = @Ip
FROM LabInvestigationFormula lf
JOIN #TargetFx t ON t.InvId = lf.InvestigationId AND t.ObservationId = lf.ObservationId AND t.Gender = lf.Gender
WHERE lf.FormulaId = (SELECT MAX(x.FormulaId) FROM LabInvestigationFormula x
                      WHERE x.InvestigationId = t.InvId AND x.ObservationId = t.ObservationId AND x.Gender = t.Gender)
  AND (lf.Expression <> t.Expr OR ISNULL(lf.FormulaText, N'') <> t.ExprText OR lf.DecimalPlaces <> t.Dp OR lf.IsActive <> 1);

-- keep a single active row per test / observation / gender
UPDATE lf SET IsActive = 0, LastModifiedBy = @UserId, LastModifiedOn = @Now
FROM LabInvestigationFormula lf JOIN #TargetFx t ON t.InvId = lf.InvestigationId AND t.ObservationId = lf.ObservationId AND t.Gender = lf.Gender
WHERE lf.IsActive = 1 AND lf.FormulaId <> (SELECT MAX(x.FormulaId) FROM LabInvestigationFormula x
                      WHERE x.InvestigationId = t.InvId AND x.ObservationId = t.ObservationId AND x.Gender = t.Gender);

INSERT LabInvestigationFormula (HospId, InvestigationId, ObservationId, Gender, Expression, FormulaText, DecimalPlaces, IsActive, CreatedBy, CreatedOn, IpAddress)
SELECT @HospId, t.InvId, t.ObservationId, t.Gender, t.Expr, t.ExprText, t.Dp, 1, @UserId, @Now, @Ip
FROM #TargetFx t
WHERE NOT EXISTS (SELECT 1 FROM LabInvestigationFormula x WHERE x.InvestigationId = t.InvId AND x.ObservationId = t.ObservationId AND x.Gender = t.Gender);

/* ---------------------------------------------------------------- 6b. result checks (ActionType 1 block save, 2 warn) */
IF OBJECT_ID('tempdb..#PanelVal') IS NOT NULL DROP TABLE #PanelVal;
CREATE TABLE #PanelVal (Panel varchar(10), ValName nvarchar(200), CheckType varchar(20), Operator varchar(12), V1 decimal(18,4), V2 decimal(18,4) NULL,
                        Tol decimal(18,4), ActionType int, Msg nvarchar(1000));
INSERT #PanelVal VALUES
 ('CBC', N'DLC total must be 100', 'SUM', 'EQ', 100, NULL, 0, 1, N'Neutrophils + Lymphocytes + Monocytes + Eosinophils + Basophils must add up to 100 %.'),
 ('LFT', N'Direct bilirubin within total', 'DIFFERENCE', 'GE', 0, NULL, 0, 1, N'Bilirubin Direct cannot be more than Bilirubin Total.'),
 ('LFT', N'Albumin within total protein', 'DIFFERENCE', 'GE', 0, NULL, 0, 1, N'Albumin cannot be more than Total Protein.'),
 ('PT',  N'ISI of reagent', 'EACH', 'BETWEEN', 0.8, 3.0, 0, 2, N'ISI is usually between 0.8 and 3.0: check the value on the reagent insert.');

IF OBJECT_ID('tempdb..#PanelValObs') IS NOT NULL DROP TABLE #PanelValObs;
CREATE TABLE #PanelValObs (Panel varchar(10), ValName nvarchar(200), Seq int, Code varchar(12));
INSERT #PanelValObs VALUES
 ('CBC', N'DLC total must be 100', 1, 'NEU'), ('CBC', N'DLC total must be 100', 2, 'LYM'), ('CBC', N'DLC total must be 100', 3, 'MON'),
 ('CBC', N'DLC total must be 100', 4, 'EOS'), ('CBC', N'DLC total must be 100', 5, 'BAS'),
 ('LFT', N'Direct bilirubin within total', 1, 'TBIL'), ('LFT', N'Direct bilirubin within total', 2, 'DBIL'),
 ('LFT', N'Albumin within total protein', 1, 'TP'), ('LFT', N'Albumin within total protein', 2, 'ALB'),
 ('PT',  N'ISI of reagent', 1, 'PT_ISI');

UPDATE v SET IsActive = 0, LastModifiedBy = @UserId, LastModifiedOn = @Now, IpAddress = @Ip
FROM LabInvestigationValidation v JOIN #Inv i ON i.InvId = v.InvestigationId AND i.IsValid = 1 AND i.IsTabular = 1
WHERE v.IsActive = 1 AND NOT EXISTS (SELECT 1 FROM #PanelVal p WHERE p.Panel = i.Panel AND p.ValName = v.ValidationName);

UPDATE v SET Gender = 'B', CheckType = p.CheckType, Expression = NULL, ExpressionText = NULL, Operator = p.Operator, Value1 = p.V1, Value2 = p.V2,
             Tolerance = p.Tol, ActionType = p.ActionType, SkipIfBlank = 1, ErrorMessage = p.Msg, IsActive = 1,
             LastModifiedBy = @UserId, LastModifiedOn = @Now, IpAddress = @Ip
FROM LabInvestigationValidation v JOIN #Inv i ON i.InvId = v.InvestigationId AND i.IsValid = 1 AND i.IsTabular = 1
JOIN #PanelVal p ON p.Panel = i.Panel AND p.ValName = v.ValidationName;

INSERT LabInvestigationValidation (HospId, InvestigationId, ValidationName, Gender, CheckType, Expression, ExpressionText, Operator, Value1, Value2,
                                   Tolerance, ActionType, SkipIfBlank, ErrorMessage, IsActive, CreatedBy, CreatedOn, IpAddress)
SELECT @HospId, i.InvId, p.ValName, 'B', p.CheckType, NULL, NULL, p.Operator, p.V1, p.V2, p.Tol, p.ActionType, 1, p.Msg, 1, @UserId, @Now, @Ip
FROM #Inv i JOIN #PanelVal p ON p.Panel = i.Panel
WHERE i.IsValid = 1 AND i.IsTabular = 1
  AND NOT EXISTS (SELECT 1 FROM LabInvestigationValidation v WHERE v.InvestigationId = i.InvId AND v.ValidationName = p.ValName);

DELETE vo FROM LabInvestigationValidationObservation vo
JOIN LabInvestigationValidation v ON v.ValidationId = vo.ValidationId
JOIN #Inv i ON i.InvId = v.InvestigationId AND i.IsValid = 1 AND i.IsTabular = 1
JOIN #PanelVal p ON p.Panel = i.Panel AND p.ValName = v.ValidationName;

INSERT LabInvestigationValidationObservation (ValidationId, ObservationId, SequenceNo)
SELECT v.ValidationId, o.Id, po.Seq
FROM LabInvestigationValidation v
JOIN #Inv i ON i.InvId = v.InvestigationId AND i.IsValid = 1 AND i.IsTabular = 1
JOIN #PanelValObs po ON po.Panel = i.Panel AND po.ValName = v.ValidationName
JOIN #Obs o ON o.Code = po.Code
ORDER BY v.ValidationId, po.Seq;

/* ---------------------------------------------------------------- 7. interpretations (TypeId 2) and templates (TypeId 1) */
IF OBJECT_ID('tempdb..#Tpl') IS NOT NULL DROP TABLE #Tpl;
CREATE TABLE #Tpl (TKey varchar(12) PRIMARY KEY, TypeId int, Type nvarchar(50), Name nvarchar(300), Content nvarchar(max), Id int NULL);
INSERT #Tpl (TKey, TypeId, Type, Name, Content) VALUES
('I_CBC', 2, N'Interpretation', N'CBC - Interpretation', N'<p><strong>Complete Blood Count - interpretation</strong></p>
<ul>
<li><strong>Anaemia (WHO):</strong> Hb below 13.0 g/dL in men and 12.0 g/dL in non-pregnant women. Mild 11.0 - 12.9 (men) / 11.0 - 11.9 (women), moderate 8.0 - 10.9, severe below 8.0 g/dL.</li>
<li><strong>Microcytic hypochromic picture</strong> (low MCV, MCH): iron deficiency, thalassaemia trait, anaemia of chronic disease. Correlate with serum ferritin / Hb electrophoresis.</li>
<li><strong>Macrocytosis</strong> (MCV above 100 fL): vitamin B12 / folate deficiency, liver disease, alcohol, hypothyroidism, some drugs.</li>
<li><strong>Raised RDW</strong> suggests mixed red cell population (early iron deficiency, combined deficiency).</li>
<li><strong>Neutrophilia:</strong> bacterial infection, inflammation, stress, steroids. <strong>Lymphocytosis:</strong> viral infection. <strong>Eosinophilia:</strong> allergy, parasitic infestation.</li>
<li><strong>Thrombocytopenia</strong> (platelets below 1.5 lakh/cumm): viral fever (dengue), sepsis, drugs, ITP; rule out platelet clumping on smear before reporting a low count.</li>
</ul>
<p>Results to be correlated clinically. Absolute counts are calculated from TLC and the differential count.</p>'),
('I_LFT', 2, N'Interpretation', N'LFT - Interpretation', N'<p><strong>Liver Function Test - interpretation</strong></p>
<ul>
<li><strong>Raised unconjugated (indirect) bilirubin:</strong> haemolysis, Gilbert syndrome, ineffective erythropoiesis.</li>
<li><strong>Raised conjugated (direct) bilirubin:</strong> hepatocellular injury or biliary obstruction.</li>
<li><strong>Hepatocellular pattern:</strong> SGPT / SGOT markedly raised with mild ALP rise (viral hepatitis, drugs, ischaemia). AST : ALT above 2 suggests alcoholic liver disease.</li>
<li><strong>Cholestatic pattern:</strong> ALP and GGT raised more than transaminases (obstruction, cholestatic drugs, infiltrative disease).</li>
<li><strong>Low albumin / reversed A/G ratio:</strong> chronic liver disease, malnutrition, nephrotic syndrome; raised globulin in chronic infection, autoimmune disease, myeloma.</li>
</ul>
<p>Haemolysed samples falsely raise SGOT. Results to be correlated clinically.</p>'),
('I_KFT', 2, N'Interpretation', N'KFT / RFT - Interpretation', N'<p><strong>Kidney Function Test - interpretation</strong></p>
<ul>
<li><strong>eGFR (CKD-EPI 2021, mL/min/1.73 m2):</strong> G1 90 or more (normal) · G2 60 - 89 (mildly decreased) · G3a 45 - 59 · G3b 30 - 44 · G4 15 - 29 · G5 below 15 (kidney failure). CKD needs reduced eGFR or kidney damage for more than 3 months.</li>
<li>eGFR is valid for adults (18 years and above) with stable creatinine; not valid in acute kidney injury, pregnancy, extremes of muscle mass or amputation.</li>
<li><strong>BUN / creatinine ratio above 20:</strong> pre-renal cause (dehydration, heart failure, GI bleed, high protein intake). Below 10: intrinsic renal disease, liver disease, low protein intake.</li>
<li><strong>Potassium above 5.5 mmol/L:</strong> verify on a fresh non-haemolysed sample before reporting; above 6.5 mmol/L is critical.</li>
<li><strong>Raised uric acid:</strong> gout, renal impairment, high cell turnover, diuretics.</li>
</ul>
<p>Results to be correlated clinically.</p>'),
('I_HB', 2, N'Interpretation', N'Haemoglobin - Interpretation', N'<p><strong>Haemoglobin - interpretation (WHO)</strong></p>
<ul>
<li>Anaemia: Hb below 13.0 g/dL (men), 12.0 g/dL (non-pregnant women), 11.0 g/dL (pregnant women, children 6 - 59 months).</li>
<li>Severity: mild 11.0 - 12.9 (men) / 11.0 - 11.9 (women), moderate 8.0 - 10.9, severe below 8.0 g/dL.</li>
<li>Raised Hb: dehydration, smoking, high altitude, chronic hypoxia, polycythaemia.</li>
</ul>
<p>Results to be correlated clinically.</p>'),
('I_URINE', 2, N'Interpretation', N'Urine Routine - Interpretation', N'<p><strong>Urine routine and microscopy - interpretation</strong></p>
<ul>
<li><strong>Pus cells above 5 /hpf</strong> with nitrite / leucocyte esterase positive suggests urinary tract infection; confirm with urine culture.</li>
<li><strong>Protein:</strong> transient with fever or exercise; persistent proteinuria needs urine albumin / creatinine ratio.</li>
<li><strong>Glucose:</strong> diabetes mellitus or low renal threshold; correlate with blood glucose.</li>
<li><strong>Ketones:</strong> uncontrolled diabetes (DKA), starvation, vomiting.</li>
<li><strong>RBCs / blood:</strong> stones, infection, trauma, glomerular disease, contamination during menstruation.</li>
<li>Many squamous epithelial cells indicate a contaminated sample: repeat with a mid-stream clean-catch sample.</li>
</ul>
<p>Results to be correlated clinically.</p>'),
('I_PT', 2, N'Interpretation', N'PT / INR - Interpretation', N'<p><strong>Prothrombin Time / INR - interpretation</strong></p>
<ul>
<li>INR = (patient PT / mean normal PT) ^ ISI of the thromboplastin reagent.</li>
<li>Normal (not on anticoagulant): INR 0.8 - 1.2.</li>
<li>Target on warfarin: INR 2.0 - 3.0 for most indications (DVT / PE, atrial fibrillation); 2.5 - 3.5 for mechanical mitral valve.</li>
<li>Raised PT / INR without anticoagulant: liver disease, vitamin K deficiency, DIC, factor II / V / VII / X deficiency.</li>
<li>Sample: 3.2 % sodium citrate, correct 9 : 1 fill, test within 4 hours.</li>
</ul>
<p>Results to be correlated clinically.</p>'),
('I_A1C', 2, N'Interpretation', N'HbA1c - Interpretation', N'<p><strong>HbA1c - interpretation (American Diabetes Association)</strong></p>
<table border="1" cellpadding="4" cellspacing="0">
<tbody>
<tr><td><strong>HbA1c (%)</strong></td><td><strong>Interpretation</strong></td></tr>
<tr><td>Below 5.7</td><td>Non-diabetic</td></tr>
<tr><td>5.7 - 6.4</td><td>Prediabetes (increased risk)</td></tr>
<tr><td>6.5 or more</td><td>Diabetes (confirm with a repeat test)</td></tr>
<tr><td>Below 7.0</td><td>Usual treatment target for adults with diabetes</td></tr>
</tbody>
</table>
<p>HbA1c reflects the average blood glucose of the last 2 - 3 months. Falsely low with haemolysis, recent blood loss or transfusion; falsely high with iron deficiency. Haemoglobin variants can interfere. Estimated average glucose (mg/dL) = 28.7 x HbA1c - 46.7.</p>'),
('I_THY', 2, N'Interpretation', N'Thyroid Profile - Interpretation', N'<p><strong>Thyroid profile (T3, T4, TSH) - interpretation</strong></p>
<table border="1" cellpadding="4" cellspacing="0">
<tbody>
<tr><td><strong>TSH</strong></td><td><strong>T3 / T4</strong></td><td><strong>Suggests</strong></td></tr>
<tr><td>High</td><td>Low</td><td>Primary hypothyroidism</td></tr>
<tr><td>High</td><td>Normal</td><td>Subclinical hypothyroidism</td></tr>
<tr><td>Low</td><td>High</td><td>Primary hyperthyroidism</td></tr>
<tr><td>Low</td><td>Normal</td><td>Subclinical hyperthyroidism / recovery / drugs</td></tr>
<tr><td>Low or normal</td><td>Low</td><td>Secondary (pituitary) hypothyroidism, non-thyroidal illness</td></tr>
</tbody>
</table>
<p>TSH in pregnancy (ATA): 1st trimester 0.1 - 2.5, 2nd 0.2 - 3.0, 3rd 0.3 - 3.0 uIU/mL. TSH has a diurnal rhythm (highest at night); biotin intake can interfere with immunoassays. Results to be correlated clinically.</p>'),
('I_MAL', 2, N'Interpretation', N'Malaria - Interpretation', N'<p><strong>Malaria test - interpretation</strong></p>
<ul>
<li>Peripheral smear (thick and thin) is the gold standard: thick film for detection, thin film for species and parasite density.</li>
<li>A single negative smear does not exclude malaria: repeat every 12 - 24 hours for 3 samples if fever continues.</li>
<li>Rapid antigen tests: HRP-2 (P. falciparum) can stay positive 2 - 4 weeks after cure; pLDH turns negative with clearance. A positive rapid test should be confirmed on smear where possible.</li>
<li>Parasitaemia above 5 % or schizonts of P. falciparum in peripheral blood indicate severe malaria.</li>
</ul>
<p>Results to be correlated clinically.</p>'),
('T_CXR', 1, N'Template', N'Chest X-Ray PA View - Normal', N'<p><strong>X-RAY CHEST PA VIEW</strong></p>
<p>Both lung fields are clear. No focal consolidation, mass or cavity is seen.</p>
<p>Bronchovascular markings are normal.</p>
<p>Both hila are normal in size and position.</p>
<p>Cardiac size and configuration are within normal limits (CT ratio less than 0.5).</p>
<p>Trachea is central. Mediastinum is not widened.</p>
<p>Both costophrenic and cardiophrenic angles are clear.</p>
<p>Both domes of the diaphragm are normal in position and outline.</p>
<p>Visualised bony thoracic cage and soft tissues are normal.</p>
<p><strong>IMPRESSION:</strong> No significant abnormality detected.</p>'),
('T_WID', 1, N'Template', N'Widal Test (Slide Agglutination) - Report', N'<p><strong>WIDAL TEST (SLIDE AGGLUTINATION METHOD)</strong></p>
<table border="1" cellpadding="4" cellspacing="0">
<tbody>
<tr><td><strong>Antigen</strong></td><td><strong>1:20</strong></td><td><strong>1:40</strong></td><td><strong>1:80</strong></td><td><strong>1:160</strong></td><td><strong>1:320</strong></td></tr>
<tr><td>Salmonella typhi O</td><td>-</td><td>-</td><td>-</td><td>-</td><td>-</td></tr>
<tr><td>Salmonella typhi H</td><td>-</td><td>-</td><td>-</td><td>-</td><td>-</td></tr>
<tr><td>Salmonella paratyphi AH</td><td>-</td><td>-</td><td>-</td><td>-</td><td>-</td></tr>
<tr><td>Salmonella paratyphi BH</td><td>-</td><td>-</td><td>-</td><td>-</td><td>-</td></tr>
</tbody>
</table>
<p><strong>RESULT:</strong> No agglutination seen in any dilution. Widal test is negative.</p>
<p><strong>Note:</strong> A titre of 1:80 or more for O and 1:160 or more for H antigen is usually significant in endemic areas. A four-fold rise in titre in paired samples 7 - 10 days apart is more reliable than a single test. Blood culture is the test of choice in the first week of fever. Previous vaccination or infection may give raised H titres.</p>'),
('T_CTH', 1, N'Template', N'NCCT Head - Normal Study', N'<p><strong>NCCT HEAD (PLAIN)</strong></p>
<p><strong>Technique:</strong> Axial non-contrast sections of the brain were obtained from the skull base to the vertex.</p>
<p><strong>Findings:</strong></p>
<p>Cerebral and cerebellar hemispheres show normal attenuation. Grey-white matter differentiation is maintained.</p>
<p>No intra-axial or extra-axial haemorrhage, infarct or space-occupying lesion is seen.</p>
<p>Ventricular system and cortical sulci are normal for age. No midline shift.</p>
<p>Basal cisterns are clear. Brain stem and cerebellum appear normal.</p>
<p>Visualised calvarium, paranasal sinuses and mastoid air cells are normal.</p>
<p><strong>IMPRESSION:</strong> No significant intracranial abnormality detected.</p>'),
('T_HRCT', 1, N'Template', N'HRCT Chest - Normal Study', N'<p><strong>HRCT CHEST</strong></p>
<p><strong>Technique:</strong> Thin-section high-resolution axial images of the thorax were obtained from the lung apices to the bases.</p>
<p><strong>Findings:</strong></p>
<p>Both lungs are normally aerated. No ground-glass opacity, consolidation, interstitial thickening, nodule, bronchiectasis or honeycombing is seen.</p>
<p>Tracheobronchial tree is patent.</p>
<p>Mediastinal vascular structures and heart are normal. No significant mediastinal or hilar lymphadenopathy.</p>
<p>No pleural or pericardial effusion. Visualised bones and chest wall are normal.</p>
<p><strong>CT severity score:</strong> 0 / 25. <strong>CO-RADS:</strong> 1.</p>
<p><strong>IMPRESSION:</strong> No significant abnormality detected in the lungs.</p>'),
('T_USG', 1, N'Template', N'USG Whole Abdomen - Normal Study', N'<p><strong>ULTRASOUND WHOLE ABDOMEN</strong></p>
<p><strong>Liver:</strong> Normal in size and echotexture. No focal lesion. Intrahepatic biliary radicles are not dilated. Portal vein is normal in calibre.</p>
<p><strong>Gall bladder:</strong> Well distended. Wall thickness is normal. No calculus or sludge.</p>
<p><strong>CBD:</strong> Normal in calibre.</p>
<p><strong>Pancreas:</strong> Normal in size and echotexture. Pancreatic duct is not dilated.</p>
<p><strong>Spleen:</strong> Normal in size and echotexture. No focal lesion.</p>
<p><strong>Right kidney:</strong> ___ x ___ cm. Normal in size, shape and echotexture. Corticomedullary differentiation is maintained. No calculus or hydronephrosis.</p>
<p><strong>Left kidney:</strong> ___ x ___ cm. Normal in size, shape and echotexture. Corticomedullary differentiation is maintained. No calculus or hydronephrosis.</p>
<p><strong>Urinary bladder:</strong> Well distended. Wall is normal. No calculus or mass.</p>
<p><strong>Prostate / Uterus and ovaries:</strong> Normal in size and echotexture.</p>
<p>No free fluid in the peritoneal cavity. No significant lymphadenopathy.</p>
<p><strong>IMPRESSION:</strong> No significant abnormality detected.</p>'),
('T_FACE', 1, N'Template', N'X-Ray Face (Facial Bones) - Normal', N'<p><strong>X-RAY FACE (FACIAL BONES)</strong></p>
<p>Visualised facial bones including both maxillae, zygomatic bones and arches, nasal bones and mandible are normal. No fracture is seen.</p>
<p>Both orbits are normal in outline.</p>
<p>Paranasal sinuses are clear. No mucosal thickening or air-fluid level.</p>
<p>Nasal septum is central.</p>
<p>Both temporomandibular joints appear normal.</p>
<p>Soft tissues are normal.</p>
<p><strong>IMPRESSION:</strong> No significant bony abnormality detected.</p>'),
('T_MRI', 1, N'Template', N'MRI Brain Plain - Normal Study', N'<p><strong>MRI BRAIN (PLAIN)</strong></p>
<p><strong>Technique:</strong> Multiplanar, multisequence MR images (T1W, T2W, FLAIR, DWI / ADC, SWI / GRE) of the brain were obtained without contrast.</p>
<p><strong>Findings:</strong></p>
<p>Brain parenchyma shows normal signal intensity. Grey-white matter differentiation is maintained.</p>
<p>No restricted diffusion to suggest acute infarct. No blooming on SWI to suggest haemorrhage or calcification.</p>
<p>No space-occupying lesion or abnormal signal in the white matter.</p>
<p>Ventricles, cisterns and cortical sulci are normal for age. No midline shift.</p>
<p>Corpus callosum, brain stem, cerebellum and cerebello-pontine angles are normal.</p>
<p>Pituitary gland and sella appear normal. Major intracranial flow voids are maintained.</p>
<p>Visualised orbits, paranasal sinuses and mastoid air cells are normal.</p>
<p><strong>IMPRESSION:</strong> No significant intracranial abnormality detected.</p>');

UPDATE t SET Id = (SELECT MIN(x.Id) FROM InvastigationTemplateCommentMaster x WHERE x.TypeId = t.TypeId AND LTRIM(RTRIM(x.Name)) = t.Name)
FROM #Tpl t;

UPDATE x SET ContentValue = t.Content, Type = t.Type, IsActive = 1, LastModifiedBy = @UserId, LastMOdifiedOn = @Now, IpAddress = @Ip
FROM InvastigationTemplateCommentMaster x JOIN #Tpl t ON t.Id = x.Id
WHERE ISNULL(x.ContentValue, N'') <> t.Content OR x.IsActive <> 1;

DECLARE @newTpl TABLE (Id int, TypeId int, Name nvarchar(3000));
INSERT InvastigationTemplateCommentMaster (TypeId, Type, Name, ContentValue, IsActive, CreatedBy, CreatedOn, IpAddress)
OUTPUT inserted.Id, inserted.TypeId, inserted.Name INTO @newTpl
SELECT t.TypeId, t.Type, t.Name, t.Content, 1, @UserId, @Now, @Ip FROM #Tpl t WHERE t.Id IS NULL;

UPDATE t SET Id = n.Id FROM #Tpl t JOIN @newTpl n ON n.TypeId = t.TypeId AND n.Name = t.Name WHERE t.Id IS NULL;

IF OBJECT_ID('tempdb..#TplMap') IS NOT NULL DROP TABLE #TplMap;
CREATE TABLE #TplMap (TKey varchar(12), Panel varchar(10));
INSERT #TplMap VALUES
 ('I_CBC','CBC'), ('I_LFT','LFT'), ('I_KFT','KFT'), ('I_HB','HB'), ('I_URINE','URINE'), ('I_PT','PT'), ('I_A1C','A1C'), ('I_THY','THY'),
 ('I_MAL','MP'), ('I_MAL','MAL'),
 ('T_CXR','T_CXR'), ('T_WID','T_WID'), ('T_CTH','T_CTH'), ('T_HRCT','T_HRCT'), ('T_USG','T_USG'), ('T_FACE','T_FACE'), ('T_MRI','T_MRI');

INSERT InvestigationTemplateInterpretationMapping (TypeId, Type, InvestigationId, ItemId, CreatedBy, CreatedOn, IpAddress)
SELECT t.TypeId, t.Type, i.InvId, t.Id, @UserId, @Now, @Ip
FROM #TplMap m JOIN #Tpl t ON t.TKey = m.TKey JOIN #Inv i ON i.Panel = m.Panel AND i.IsValid = 1
WHERE NOT EXISTS (SELECT 1 FROM InvestigationTemplateInterpretationMapping x WHERE x.TypeId = t.TypeId AND x.InvestigationId = i.InvId AND x.ItemId = t.Id);

/* ---------------------------------------------------------------- 8. report type of the tests */
UPDATE s SET ReportTypeId = CASE WHEN i.IsTabular = 1 THEN 1 ELSE 2 END,
             ReportType = CASE WHEN i.IsTabular = 1 THEN N'Tabular' ELSE N'Free Text' END,
             LastModifiedBy = @UserId, LastModifiedOn = @Now
FROM ServiceItemMaster s JOIN #Inv i ON i.InvId = s.ServiceItemId AND i.IsValid = 1
WHERE ISNULL(s.ReportTypeId, 0) <> CASE WHEN i.IsTabular = 1 THEN 1 ELSE 2 END
   OR ISNULL(s.ReportType, N'') <> CASE WHEN i.IsTabular = 1 THEN N'Tabular' ELSE N'Free Text' END;

/* ---------------------------------------------------------------- summary */
SELECT i.InvId, i.ExpectedName AS Test, i.IsValid,
       (SELECT COUNT(*) FROM InvastigationObservationMapping m WHERE m.InvastigationId = i.InvId AND m.IsActive = 1) AS ActiveRows,
       (SELECT COUNT(*) FROM LabInvestigationFormula f WHERE f.InvestigationId = i.InvId AND f.IsActive = 1) AS Formulas,
       (SELECT COUNT(*) FROM LabInvestigationValidation v WHERE v.InvestigationId = i.InvId AND v.IsActive = 1) AS Checks,
       (SELECT COUNT(*) FROM InvestigationTemplateInterpretationMapping x WHERE x.InvestigationId = i.InvId AND x.TypeId = 2) AS Interpretations,
       (SELECT COUNT(*) FROM InvestigationTemplateInterpretationMapping x WHERE x.InvestigationId = i.InvId AND x.TypeId = 1) AS Templates
FROM #Inv i ORDER BY i.IsTabular DESC, i.InvId;

IF @DryRun = 1
BEGIN
  ROLLBACK TRAN;
  PRINT N'Dry run: all changes rolled back.';
END
ELSE
BEGIN
  COMMIT TRAN;
  PRINT N'Lab top-20 master data applied.';
END
