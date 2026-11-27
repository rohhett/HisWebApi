/* =====================================================================
   Lab report PRINT-SETTING PREVIEW (dummy data)

   Used by  GET api/Lab/printSettingPreviewReport  (DummyMode = 3).
   Renders the normal lab report with this dummy data so changes in
   LabReportPrintSettingMaster can be checked without real patients.

   - 4 tables : LabReportDummyPatient, LabReportDummyInvestigation,
                LabReportDummyTabularResult, LabReportDummyFreeTextResult
   - seed     : 1 patient, 10 investigations (7 tabular + 3 free text),
                each IsPrintAlone = 1  ->  10 pages
   - 3 SPs    : S_GetLabReportPrintPreviewInvestigations
                S_GetLabReportPrintPreviewTabularResults
                S_GetLabReportPrintPreviewFreeTextResults

   Header / footer (HeaderMaster), letter head and doctor signature are
   taken from the real branch setup, because they are part of the print
   setting being checked.

   Safe to run more than once (seed is deleted and re-inserted).
   File contains unicode (µ, ³): run with  sqlcmd ... -f 65001
   ===================================================================== */

/* ---------- 1. tables ---------- */
IF OBJECT_ID('dbo.LabReportDummyPatient') IS NULL
CREATE TABLE dbo.LabReportDummyPatient
(
    DummyPatientId      INT            NOT NULL PRIMARY KEY,
    PName               NVARCHAR(200)  NULL,
    UHID                NVARCHAR(50)   NULL,
    Age                 NVARCHAR(50)   NULL,
    Gender              NVARCHAR(20)   NULL,
    DOB                 NVARCHAR(20)   NULL,
    Relation            NVARCHAR(20)   NULL,
    Relative            NVARCHAR(200)  NULL,
    ContactNo           NVARCHAR(20)   NULL,
    Email               NVARCHAR(200)  NULL,
    ADoctor             NVARCHAR(200)  NULL,
    InvPrescribedBy     NVARCHAR(200)  NULL,
    InvReferdBy         NVARCHAR(200)  NULL,
    IPDNo               NVARCHAR(50)   NULL,
    LabNo               NVARCHAR(50)   NULL,
    BarCode             NVARCHAR(50)   NULL,
    VisitType           NVARCHAR(20)   NULL,
    InvReportApprovedBy NVARCHAR(200)  NULL,
    InvResultEntryBy    NVARCHAR(200)  NULL
);
GO

IF OBJECT_ID('dbo.LabReportDummyInvestigation') IS NULL
CREATE TABLE dbo.LabReportDummyInvestigation
(
    DummyInvestigationId INT            NOT NULL PRIMARY KEY,
    DummyPatientId       INT            NOT NULL,
    InvestigationName    NVARCHAR(200)  NOT NULL,
    Department           NVARCHAR(100)  NULL,
    SampleType           NVARCHAR(100)  NULL,
    ReportTypeId         INT            NOT NULL,      -- 1 = Tabular, 2 = Free text
    InvestigationMethod  NVARCHAR(200)  NULL,
    InvestigationComment NVARCHAR(MAX)  NULL,
    Interpretation       NVARCHAR(MAX)  NULL,          -- HTML
    IsPrintAlone         INT            NOT NULL DEFAULT 1,
    SortOrder            INT            NOT NULL DEFAULT 0,
    IsActive             BIT            NOT NULL DEFAULT 1
);
GO

IF OBJECT_ID('dbo.LabReportDummyTabularResult') IS NULL
CREATE TABLE dbo.LabReportDummyTabularResult
(
    Id                   INT IDENTITY(1,1) PRIMARY KEY,
    DummyInvestigationId INT            NOT NULL,
    SortOrder            INT            NOT NULL,
    ObservationName      NVARCHAR(200)  NOT NULL,
    ResultValue          NVARCHAR(500)  NULL,
    MinValue             NVARCHAR(50)   NULL,
    MaxValue             NVARCHAR(50)   NULL,
    DisplayRange         NVARCHAR(200)  NULL,
    Unit                 NVARCHAR(50)   NULL,
    FieldTypeId          INT            NOT NULL DEFAULT 1,   -- 1 = value/unit/range columns, other = result spans columns
    IsResultBold         INT            NOT NULL DEFAULT 0,
    ObservationMethod    NVARCHAR(200)  NULL,
    ObservationComment   NVARCHAR(MAX)  NULL,
    IsHeader             TINYINT        NOT NULL DEFAULT 0,   -- 1 = sub heading row
    IsBold               BIT            NOT NULL DEFAULT 0,
    IsUnderLine          BIT            NOT NULL DEFAULT 0
);
GO

IF OBJECT_ID('dbo.LabReportDummyFreeTextResult') IS NULL
CREATE TABLE dbo.LabReportDummyFreeTextResult
(
    DummyInvestigationId INT           NOT NULL PRIMARY KEY,
    ResultValue          NVARCHAR(MAX) NULL                  -- HTML
);
GO

/* ---------- 2. seed ---------- */
DELETE FROM dbo.LabReportDummyTabularResult;
DELETE FROM dbo.LabReportDummyFreeTextResult;
DELETE FROM dbo.LabReportDummyInvestigation;
DELETE FROM dbo.LabReportDummyPatient;
GO

INSERT INTO dbo.LabReportDummyPatient
(DummyPatientId, PName, UHID, Age, Gender, DOB, Relation, Relative, ContactNo, Email, ADoctor, InvPrescribedBy, InvReferdBy,
 IPDNo, LabNo, BarCode, VisitType, InvReportApprovedBy, InvResultEntryBy)
VALUES
(1, N'MR. DEMO PATIENT', N'UHID.DEMO.001', N'66Y 0M 0D', N'MALE', N'01-01-1960', N'S/O', N'DEMO RELATIVE', N'9999999999', N'demo@example.com',
 N'Dr. Demo Consultant', N'Dr. Demo Consultant', N'DEMO HOSPITAL', N'', N'DEMO-LAB-001', N'DEMO_BARCODE', N'OPD', N'Dr. Demo Pathologist', N'Demo Technician');
GO

INSERT INTO dbo.LabReportDummyInvestigation
(DummyInvestigationId, DummyPatientId, InvestigationName, Department, SampleType, ReportTypeId, InvestigationMethod, InvestigationComment, Interpretation, IsPrintAlone, SortOrder)
VALUES
(1,  1, N'CBC',                         N'HAEMATOLOGY',  N'EDTA Whole Blood', 1, N'Automated Cell Counter', N'Please correlate with clinical conditions.', NULL, 1, 1),
(2,  1, N'Liver Function Test',         N'BIOCHEMISTRY', N'Serum',            1, N'Dry Chemistry',          N'Please correlate results clinically.',
 N'<p><b>Clinical Significance of LFT:</b> Liver function tests help to detect hepatic abnormality, measure the severity of liver damage and follow the response to treatment.</p>', 1, 2),
(3,  1, N'Kidney Function Test',        N'BIOCHEMISTRY', N'Serum',            1, N'Dry Chemistry',          N'Please correlate results clinically.',
 N'<p><b>Classification of eGFR (KDIGO):</b></p><table><tr><th>eGFR (mL/min/1.73 m2)</th><th>Category</th><th>Significance</th></tr><tr><td>&gt;90</td><td>G1</td><td>Normal</td></tr><tr><td>60-89</td><td>G2</td><td>Mildly decreased</td></tr><tr><td>45-59</td><td>G3a</td><td>Mild to moderate decrease</td></tr><tr><td>30-44</td><td>G3b</td><td>Moderate to severe decrease</td></tr><tr><td>15-29</td><td>G4</td><td>Severely decreased</td></tr><tr><td>&lt;15</td><td>G5</td><td>Kidney failure</td></tr></table>', 1, 3),
(4,  1, N'Lipid Profile',               N'BIOCHEMISTRY', N'Serum',            1, N'Enzymatic',              NULL, NULL, 1, 4),
(5,  1, N'Thyroid Profile (T3, T4, TSH)', N'IMMUNOLOGY', N'Serum',            1, N'CLIA',                   N'TSH shows diurnal variation; a single value should be interpreted with clinical findings.', NULL, 1, 5),
(6,  1, N'Urine R/M',                   N'CLINICAL PATHOLOGY', N'Urine',      1, N'Strip & Microscopy',     N'Sample quantity is observed after transfer to a urinalysis tube.', NULL, 1, 6),
(7,  1, N'HbA1c (Glycosylated Haemoglobin)', N'BIOCHEMISTRY', N'EDTA Whole Blood', 1, N'HPLC',              NULL,
 N'<table><tr><th>HbA1c (%)</th><th>Interpretation</th></tr><tr><td>&lt; 5.7</td><td>Non diabetic</td></tr><tr><td>5.7 - 6.4</td><td>Pre diabetic</td></tr><tr><td>&gt;= 6.5</td><td>Diabetic</td></tr></table>', 1, 7),
(8,  1, N'X-Ray Chest PA View',         N'RADIOLOGY',    N'',                 2, NULL,                      N'Kindly correlate clinically.', NULL, 1, 8),
(9,  1, N'USG Whole Abdomen',           N'RADIOLOGY',    N'',                 2, NULL,                      NULL, NULL, 1, 9),
(10, 1, N'Peripheral Smear Examination', N'HAEMATOLOGY', N'EDTA Whole Blood', 2, NULL,                      N'Suggested: Serum Iron studies.', NULL, 1, 10);
GO

/* tabular rows: (inv, sort, name, result, min, max, range, unit, fieldType, resultBold, method, comment, isHeader, bold, underline) */
INSERT INTO dbo.LabReportDummyTabularResult
(DummyInvestigationId, SortOrder, ObservationName, ResultValue, MinValue, MaxValue, DisplayRange, Unit, FieldTypeId, IsResultBold, ObservationMethod, ObservationComment, IsHeader, IsBold, IsUnderLine)
VALUES
-- 1. CBC
(1, 1,  N'Hemoglobin',                       N'7.1',   N'13',   N'17',   N'13.0 - 17.0',  N'gm/dL',      1, 0, N'Photometric Measurement', N'Low haemoglobin, suggestive of anaemia.', 0, 0, 0),
(1, 2,  N'Total RBC',                        N'3.50',  N'4.5',  N'5.5',  N'4.5 - 5.5',    N'million/µL', 1, 0, N'Coulter Principle', NULL, 0, 0, 0),
(1, 3,  N'Platelet Count',                   N'214',   N'150',  N'410',  N'150 - 410',    N'X 10³ / µL', 1, 0, N'Impedance', NULL, 0, 0, 0),
(1, 4,  N'Total Leucocyte Count (WBC)',      N'13.72', N'4',    N'10',   N'4.0 - 10.0',   N'X 10³ / µL', 1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 5,  N'Differential Leucocyte Count (DLC)', NULL,   NULL,    NULL,    NULL,            NULL,          1, 0, NULL, NULL, 1, 1, 1),
(1, 6,  N'Neutrophils',                      N'75.5',  N'40',   N'80',   N'40 - 80',      N'%',          1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 7,  N'Lymphocytes',                      N'15.0',  N'20',   N'40',   N'20 - 40',      N'%',          1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 8,  N'Monocytes',                        N'7.3',   N'2',    N'10',   N'2 - 10',       N'%',          1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 9,  N'Eosinophils',                      N'2.1',   N'1',    N'6',    N'1 - 6',        N'%',          1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 10, N'Basophils',                        N'0.1',   N'0',    N'1',    N'0 - 1',        N'%',          1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 11, N'Absolute Neutrophil Count',        N'10.36', N'2',    N'7.5',  N'2.0 - 7.5',    N'X 10³ / µL', 1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 12, N'Absolute Lymphocyte Count',        N'2.06',  N'1',    N'4',    N'1.0 - 4.0',    N'X 10³ / µL', 1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 13, N'Absolute Monocyte Count',          N'1.00',  N'0.2',  N'1',    N'0.2 - 1.0',    N'X 10³ / µL', 1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 14, N'Absolute Eosinophil Count',        N'0.29',  N'0.02', N'0.5',  N'0.02 - 0.5',   N'X 10³ / µL', 1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 15, N'Absolute Basophil Count',          N'0.02',  N'0',    N'0.3',  N'0.00 - 0.30',  N'X 10³ / µL', 1, 0, N'Flow Cytometry', NULL, 0, 0, 0),
(1, 16, N'Indices',                          NULL,     NULL,    NULL,    NULL,            NULL,          1, 0, NULL, NULL, 1, 1, 1),
(1, 17, N'Hematocrit (PCV)',                 N'22.8',  N'40',   N'50',   N'40 - 50',      N'%',          1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 18, N'Mean Corpuscular Volume (MCV)',    N'65.1',  N'83',   N'101',  N'83 - 101',     N'fL',         1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 19, N'Mean Corp. Hemoglobin (MCH)',      N'20.3',  N'27',   N'32',   N'27 - 32',      N'pg',         1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 20, N'MCH Concentration (MCHC)',         N'31.1',  N'31.5', N'34.5', N'31.5 - 34.5',  N'g/dL',       1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 21, N'Red Cell Dist. Width (RDW-CV)',    N'16.1',  N'11.5', N'14.5', N'11.5 - 14.5',  N'%',          1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 22, N'Red Cell Dist. Width (RDW-SD)',    N'37.8',  N'39',   N'46',   N'39 - 46',      N'fL',         1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 23, N'Mean Platelet Volume (MPV)',       N'13.4',  N'7.5',  N'12',   N'7.5 - 12.0',   N'fL',         1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 24, N'P-LCC',                            N'113',   N'30',   N'90',   N'30 - 90',      N'10^9/L',     1, 0, N'SF Cube', NULL, 0, 0, 0),
(1, 25, N'P-LCR',                            N'52.8',  N'11',   N'45',   N'11 - 45',      N'%',          1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 26, N'Neutrophil-Lymphocyte Ratio (NLR)', N'5.03', NULL,    NULL,    N'',             N'Ratio',      1, 0, N'Calculated', NULL, 0, 0, 0),
(1, 27, N'Mentzer Index',                    N'18.6',  NULL,    NULL,    N'',             N'Index',      1, 0, N'Calculated', NULL, 0, 0, 0),

-- 2. LFT
(2, 1,  N'Total Protein',                    N'6.5',   N'6.5',  N'7.8',  N'6.5 - 7.8',    N'g/dL',  1, 0, N'Biuret, No Serum Blank', NULL, 0, 0, 0),
(2, 2,  N'Albumin',                          N'3.3',   N'4.2',  N'5.0',  N'4.2 - 5.0',    N'g/dL',  1, 0, N'Bromocresol Green', NULL, 0, 0, 0),
(2, 3,  N'Globulin',                         N'3.2',   N'2.0',  N'3.5',  N'2.0 - 3.5',    N'gm/dL', 1, 0, N'Calculated', NULL, 0, 0, 0),
(2, 4,  N'A/G Ratio',                        N'1.03',  N'1.5',  N'2.5',  N'1.5 - 2.5',    N'Ratio', 1, 0, N'Calculated', NULL, 0, 0, 0),
(2, 5,  N'Total Bilirubin',                  N'1.09',  N'0.2',  N'1.3',  N'0.2 - 1.3',    N'mg/dL', 1, 0, N'Azobilirubin/Dyphylline', NULL, 0, 0, 0),
(2, 6,  N'Conjugated Bilirubin',             N'0.97',  N'0',    N'0.3',  N'< 0.3',        N'mg/dL', 1, 0, N'Calculated', NULL, 0, 0, 0),
(2, 7,  N'Unconjugated Bilirubin',           N'0.12',  N'0',    N'1.1',  N'< 1.1',        N'mg/dL', 1, 0, N'Spectrophotometry', NULL, 0, 0, 0),
(2, 8,  N'SGOT (AST)',                       N'35',    N'18',   N'39',   N'18 - 39',      N'U/L',   1, 0, N'Enzymatic Colorimetric', NULL, 0, 0, 0),
(2, 9,  N'SGPT (ALT)',                       N'46',    N'4',    N'50',   N'4 - 50',       N'U/L',   1, 0, N'UV with P5P', NULL, 0, 0, 0),
(2, 10, N'SGOT/SGPT Ratio',                  N'0.76',  NULL,    NULL,    N'',             N'Ratio', 1, 0, N'Calculated', NULL, 0, 0, 0),
(2, 11, N'Alkaline Phosphatase',             N'442',   N'50',   N'116',  N'50 - 116',     N'U/L',   1, 0, N'PNPP, AMP buffer', NULL, 0, 0, 0),
(2, 12, N'Gamma Glutamyl Transferase',       N'240',   N'13',   N'109',  N'13 - 109',     N'U/L',   1, 0, N'G-glutamyl-p-nitroanilide', NULL, 0, 0, 0),

-- 3. KFT
(3, 1,  N'Blood Urea',                       N'32',    N'19',   N'43',   N'19 - 43',      N'mg/dL',          1, 0, N'Urease, Colorimetric', NULL, 0, 0, 0),
(3, 2,  N'Blood Urea Nitrogen',              N'14.95', N'9',    N'20',   N'9 - 20',       N'mg/dL',          1, 0, N'Calculated', NULL, 0, 0, 0),
(3, 3,  N'Creatinine',                       N'0.88',  N'0.6',  N'1.25', N'0.6 - 1.25',   N'mg/dL',          1, 0, N'Enzymatic', NULL, 0, 0, 0),
(3, 4,  N'Estimated GFR',                    N'94.80', NULL,    NULL,    N'',             N'mL/min/1.73m2',  1, 0, N'Calculated By CKD-EPI (2021)', NULL, 0, 0, 0),
(3, 5,  N'Uric Acid',                        N'6.2',   N'3.5',  N'8.5',  N'3.5 - 8.5',    N'mg/dL',          1, 0, N'Uricase, Colorimetric', NULL, 0, 0, 0),
(3, 6,  N'Calcium',                          N'8.6',   N'8.4',  N'10.2', N'8.4 - 10.2',   N'mg/dL',          1, 0, N'Arsenazo III', NULL, 0, 0, 0),
(3, 7,  N'Phosphorus',                       N'4.3',   N'2.5',  N'4.5',  N'2.5 - 4.5',    N'mg/dL',          1, 0, N'Phosphomolybdate reduction', NULL, 0, 0, 0),
(3, 8,  N'BUN/Creatinine Ratio',             N'16.99', NULL,    NULL,    N'',             N'Ratio',          1, 0, N'Calculated', NULL, 0, 0, 0),
(3, 9,  N'Electrolytes',                     NULL,     NULL,    NULL,    NULL,            NULL,              1, 0, NULL, NULL, 1, 1, 1),
(3, 10, N'Sodium',                           N'132',   N'137',  N'145',  N'137 - 145',    N'mmol/L',         1, 0, N'ISE Direct', NULL, 0, 0, 0),
(3, 11, N'Potassium',                        N'4.4',   N'3.5',  N'5.1',  N'3.5 - 5.1',    N'mmol/L',         1, 0, N'ISE Direct', NULL, 0, 0, 0),
(3, 12, N'Chloride',                         N'100',   N'98',   N'107',  N'98 - 107',     N'mmol/L',         1, 0, N'ISE Direct', NULL, 0, 0, 0),

-- 4. Lipid profile
(4, 1,  N'Total Cholesterol',                N'232',   N'0',    N'200',  N'< 200',        N'mg/dL', 1, 0, N'CHOD-PAP', NULL, 0, 0, 0),
(4, 2,  N'Triglycerides',                    N'188',   N'0',    N'150',  N'< 150',        N'mg/dL', 1, 0, N'GPO-PAP', NULL, 0, 0, 0),
(4, 3,  N'HDL Cholesterol',                  N'38',    N'40',   N'60',   N'40 - 60',      N'mg/dL', 1, 0, N'Direct', NULL, 0, 0, 0),
(4, 4,  N'LDL Cholesterol',                  N'156.4', N'0',    N'100',  N'< 100',        N'mg/dL', 1, 0, N'Calculated', NULL, 0, 0, 0),
(4, 5,  N'VLDL Cholesterol',                 N'37.6',  N'5',    N'40',   N'5 - 40',       N'mg/dL', 1, 0, N'Calculated', NULL, 0, 0, 0),
(4, 6,  N'Non-HDL Cholesterol',              N'194',   N'0',    N'130',  N'< 130',        N'mg/dL', 1, 0, N'Calculated', NULL, 0, 0, 0),
(4, 7,  N'Total Cholesterol / HDL Ratio',    N'6.1',   N'0',    N'5',    N'< 5.0',        N'Ratio', 1, 0, N'Calculated', NULL, 0, 0, 0),
(4, 8,  N'LDL / HDL Ratio',                  N'4.1',   N'0',    N'3.5',  N'< 3.5',        N'Ratio', 1, 0, N'Calculated', NULL, 0, 0, 0),

-- 5. Thyroid profile
(5, 1,  N'Total T3 (Triiodothyronine)',      N'1.12',  N'0.8',  N'2.0',  N'0.80 - 2.00',  N'ng/mL',  1, 0, N'CLIA', NULL, 0, 0, 0),
(5, 2,  N'Total T4 (Thyroxine)',             N'7.9',   N'5.1',  N'14.1', N'5.10 - 14.10', N'µg/dL',  1, 0, N'CLIA', NULL, 0, 0, 0),
(5, 3,  N'TSH (Thyroid Stimulating Hormone)', N'6.85', N'0.27', N'4.2',  N'0.27 - 4.20',  N'µIU/mL', 1, 0, N'CLIA', N'Mildly raised TSH; repeat after 4-6 weeks is advised.', 0, 0, 0),

-- 6. Urine R/M
(6, 1,  N'Physical Examination',             NULL,          NULL,    NULL,    NULL,            NULL,    1, 0, NULL, NULL, 1, 1, 1),
(6, 2,  N'Urine Quantity',                   N'7.5',        N'7',    N'8',    N'7 - 8',        N'mL',   1, 0, N'Physical Examination', NULL, 0, 0, 0),
(6, 3,  N'Urine Colour',                     N'Pale Yellow', NULL,   NULL,    N'Pale Yellow',  N'',     1, 0, N'Physical Examination', NULL, 0, 0, 0),
(6, 4,  N'Urinary Transparency',             N'Clear',      NULL,    NULL,    N'Clear',        N'',     1, 0, N'Physical Examination', NULL, 0, 0, 0),
(6, 5,  N'Biochemical Examination',          NULL,          NULL,    NULL,    NULL,            NULL,    1, 0, NULL, NULL, 1, 1, 1),
(6, 6,  N'Urinary pH',                       N'5.5',        N'6',    N'8',    N'6.0 - 8.0',    N'pH',   1, 0, N'Bromothymol blue', NULL, 0, 0, 0),
(6, 7,  N'Urinary Specific Gravity',         N'1.005',      N'1.005', N'1.030', N'1.005 - 1.030', N'',  1, 1, N'Ethyleneglycol-bis t.a.a.', NULL, 0, 0, 0),
(6, 8,  N'Urinary Protein',                  N'Negative',   NULL,    NULL,    N'Negative',     N'',     1, 0, N'Tetrachlorophenol', NULL, 0, 0, 0),
(6, 9,  N'Urinary Glucose',                  N'Negative',   NULL,    NULL,    N'Negative',     N'',     1, 0, N'Glucose-oxidase-peroxidase', NULL, 0, 0, 0),
(6, 10, N'Urinary Ketones',                  N'Negative',   NULL,    NULL,    N'Negative',     N'',     1, 0, N'Sodium Nitroprusside', NULL, 0, 0, 0),
(6, 11, N'Urobilinogen',                     N'Negative',   NULL,    NULL,    N'Negative',     N'',     1, 0, N'Methoxybenzene Diazonium', NULL, 0, 0, 0),
(6, 12, N'Urine Bilirubin',                  N'Negative',   NULL,    NULL,    N'Negative',     N'',     1, 0, N'Dichlorobenzene-diazonium', NULL, 0, 0, 0),
(6, 13, N'Urinary Nitrites',                 N'Negative',   NULL,    NULL,    N'Negative',     N'',     1, 0, N'Hydroxy', NULL, 0, 0, 0),
(6, 14, N'Microscopic Examination',          NULL,          NULL,    NULL,    NULL,            NULL,    1, 0, NULL, NULL, 1, 1, 1),
(6, 15, N'Pus Cells',                        N'1-2',        NULL,    NULL,    N'1 - 2 /HPF',   N'/HPF', 1, 0, N'Flow Micro Imaging', NULL, 0, 0, 0),
(6, 16, N'Epithelial Cells (Squamous)',      N'1-2',        NULL,    NULL,    N'0 - 2 /HPF',   N'/HPF', 1, 0, N'Flow Micro Imaging', NULL, 0, 0, 0),
(6, 17, N'Urinary RBC',                      N'NIL',        NULL,    NULL,    N'NIL /HPF',     N'/HPF', 1, 0, N'Flow Micro Imaging', NULL, 0, 0, 0),
(6, 18, N'Hyaline Casts',                    N'NIL',        NULL,    NULL,    N'0 - 2 /LPF',   N'/LPF', 1, 0, N'Flow Micro Imaging', NULL, 0, 0, 0),
(6, 19, N'Crystals',                         N'NIL',        NULL,    NULL,    N'NIL /HPF',     N'/HPF', 1, 0, N'Flow Micro Imaging', NULL, 0, 0, 0),
(6, 20, N'Other Morphology',                 N'NIL',        NULL,    NULL,    NULL,            NULL,    2, 0, N'Microscopy', NULL, 0, 0, 0),

-- 7. HbA1c
(7, 1,  N'HbA1c',                            N'7.8',   N'4',    N'5.6',  N'4.0 - 5.6',    N'%',     1, 0, N'HPLC', NULL, 0, 0, 0),
(7, 2,  N'Estimated Average Glucose (eAG)',  N'177',   N'70',   N'114',  N'70 - 114',     N'mg/dL', 1, 0, N'Calculated', NULL, 0, 0, 0);
GO

INSERT INTO dbo.LabReportDummyFreeTextResult (DummyInvestigationId, ResultValue)
VALUES
(8,  N'<p><b>X-RAY CHEST PA VIEW</b></p>
<p>Both lung fields are clear. No focal parenchymal lesion is seen.</p>
<p>Both costophrenic angles are clear.</p>
<p>Cardiac size and configuration are within normal limits.</p>
<p>Trachea is central. Both hila are normal.</p>
<p>Visualised bony thoracic cage appears normal.</p>
<p><b>IMPRESSION:</b> No significant abnormality detected.</p>'),
(9,  N'<p><b>USG WHOLE ABDOMEN</b></p>
<p><b>Liver:</b> Normal in size (13.2 cm) with mildly increased echotexture. No focal lesion. Intrahepatic biliary radicles are not dilated.</p>
<p><b>Gall Bladder:</b> Well distended. Wall thickness normal. No calculus seen.</p>
<p><b>Pancreas:</b> Normal in size and echotexture.</p>
<p><b>Spleen:</b> Normal in size (9.8 cm) and echotexture.</p>
<p><b>Kidneys:</b> Right kidney 10.1 cm, left kidney 10.4 cm. Normal size and cortical echogenicity. Corticomedullary differentiation maintained. No calculus or hydronephrosis.</p>
<p><b>Urinary Bladder:</b> Well distended, wall normal, no calculus.</p>
<p><b>Prostate:</b> Normal in size (volume 18 cc).</p>
<p>No free fluid in peritoneal cavity.</p>
<p><b>IMPRESSION:</b> Grade I fatty liver. Rest of the study is within normal limits.</p>'),
(10, N'<p><b>RBC:</b> Predominantly microcytic hypochromic red cells with mild anisopoikilocytosis. Few pencil cells and target cells seen.</p>
<p><b>WBC:</b> Total count is mildly increased. Differential count shows neutrophilia. No immature cells seen.</p>
<p><b>Platelets:</b> Adequate on smear. Normal morphology.</p>
<p><b>Haemoparasites:</b> Not seen.</p>
<p><b>IMPRESSION:</b> Microcytic hypochromic anaemia with neutrophilic leucocytosis.</p>');
GO

/* ---------- 3. SPs (same columns as the real print SPs) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetLabReportPrintPreviewInvestigations
    @PatientInvestigationIdList NVARCHAR(MAX) = NULL,   -- '' / '0' = all dummy investigations
    @isHeaderPNG INT = 0,
    @PrintBy     NVARCHAR(20) = NULL,
    @branchId    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @All BIT = CASE WHEN ISNULL(LTRIM(RTRIM(@PatientInvestigationIdList)), '') IN ('', '0') THEN 1 ELSE 0 END;

    SELECT
        P.PName, P.UHID, P.Age, P.Gender, P.DOB, P.Relation, P.Relative, P.ContactNo, P.Email,
        P.ADoctor, P.InvPrescribedBy, P.InvReferdBy, P.IPDNo,
        P.LabNo, P.BarCode,
        FORMAT(DATEADD(MINUTE, -150, @Now), 'dd-MMM-yy HH:mm') AS InvBillingDateTime,
        FORMAT(DATEADD(MINUTE, -145, @Now), 'dd-MMM-yy HH:mm') AS InvSampleColDateTime,
        FORMAT(DATEADD(MINUTE, -130, @Now), 'dd-MMM-yy HH:mm') AS InvSentToDepartmentDateTime,
        FORMAT(DATEADD(MINUTE, -120, @Now), 'dd-MMM-yy HH:mm') AS InvDeptRecDateTime,
        FORMAT(DATEADD(MINUTE,  -10, @Now), 'dd-MMM-yy HH:mm') AS InvReportApprovalDateTime,
        FORMAT(DATEADD(MINUTE,  -30, @Now), 'dd-MMM-yy HH:mm') AS InvResultDateTime,
        P.InvReportApprovedBy,
        -- real signature / letter head of the branch so their print settings can be checked
        (SELECT TOP 1 DLA.DocSignPath FROM DoctorSignatureMaster DLA WHERE DLA.isDelete = 0 AND DLA.isActive = 1) AS DoctorSignFilePath,
        (SELECT TOP 1 DLA.XSign       FROM DoctorSignatureMaster DLA WHERE DLA.isDelete = 0 AND DLA.isActive = 1) AS XSign,
        (SELECT TOP 1 DLA.YSign       FROM DoctorSignatureMaster DLA WHERE DLA.isDelete = 0 AND DLA.isActive = 1) AS YSign,
        (SELECT TOP 1 LH.LetterHeadFilePath FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS LetterHeadFilePath,
        (SELECT TOP 1 LH.PaddingLeft  FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS PaddingLeft,
        (SELECT TOP 1 LH.PaddingRight FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS PaddingRight,
        (SELECT TOP 1 LH.PaddingTop   FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS PaddingTop,
        CAST('' AS NVARCHAR(MAX)) AS ReportFooterRemarks,
        CAST('' AS NVARCHAR(500)) AS NABLFilePath,
        0 AS IsDefaultLogo,
        P.InvResultEntryBy,
        ISNULL((SELECT UM.Name FROM UserMaster UM WHERE CAST(UM.ID AS NVARCHAR(20)) = @PrintBy), '') AS InvResultPrintBy,
        I.DummyInvestigationId AS PatientInvestigationId,
        I.DummyInvestigationId AS InvestigationId,
        1 AS IsReportApproved,
        I.ReportTypeId,
        I.IsPrintAlone,
        I.Department,
        I.SampleType,
        I.InvestigationMethod,
        I.Interpretation AS interpretation,
        'Final' AS InvReportStatus,
        I.InvestigationComment,
        P.VisitType,
        I.Department AS SubSubCategoryName,
        I.SortOrder AS TestWiseOrdering,
        I.SortOrder AS DepartmentWiseOrdering,
        0 AS IsUnderPackage,
        I.InvestigationName,
        CAST('' AS NVARCHAR(MAX)) AS InvestigationDocumentFile
    FROM dbo.LabReportDummyInvestigation I
    INNER JOIN dbo.LabReportDummyPatient P ON P.DummyPatientId = I.DummyPatientId
    WHERE I.IsActive = 1
      AND (@All = 1 OR I.DummyInvestigationId IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@PatientInvestigationIdList, ',')))
    ORDER BY I.SortOrder, I.DummyInvestigationId;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetLabReportPrintPreviewTabularResults
    @PTInvstId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        R.ResultValue, R.MinValue, R.MaxValue, R.DisplayRange, R.Unit,
        R.FieldTypeId, R.IsResultBold,
        CASE
            WHEN TRY_CAST(R.ResultValue AS DECIMAL(18,10)) IS NOT NULL
             AND TRY_CAST(R.MinValue    AS DECIMAL(18,10)) IS NOT NULL
             AND TRY_CAST(R.MaxValue    AS DECIMAL(18,10)) IS NOT NULL
            THEN CASE
                    WHEN TRY_CAST(R.ResultValue AS DECIMAL(18,10)) < TRY_CAST(R.MinValue AS DECIMAL(18,10)) THEN '-1'
                    WHEN TRY_CAST(R.ResultValue AS DECIMAL(18,10)) > TRY_CAST(R.MaxValue AS DECIMAL(18,10)) THEN '1'
                    ELSE '0'
                 END
            ELSE ''
        END AS Flag,
        R.ObservationName,
        R.ObservationMethod,
        ISNULL(R.ObservationComment, '') AS ObservationComment,
        R.IsHeader,
        R.IsBold,
        R.IsUnderLine
    FROM dbo.LabReportDummyTabularResult R
    WHERE R.DummyInvestigationId = @PTInvstId
    ORDER BY R.SortOrder, R.Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetLabReportPrintPreviewFreeTextResults
    @PTInvstId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ISNULL(F.ResultValue, '') AS ResultValue,
        I.InvestigationComment
    FROM dbo.LabReportDummyInvestigation I
    LEFT JOIN dbo.LabReportDummyFreeTextResult F ON F.DummyInvestigationId = I.DummyInvestigationId
    WHERE I.DummyInvestigationId = @PTInvstId;
END
GO
