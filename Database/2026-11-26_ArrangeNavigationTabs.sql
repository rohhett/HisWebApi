/*  Arrange navigation tabs and menus like the OLD HIS (owner request 2026-11-26).
    Target: GWT_HIS_API. Re-runnable: yes.
    1. Backups (first run only): NavigationTabMaster_Bak_20261126, NavigationSubMenuMaster_Bak_20261126
    2. Rename tabs: Admin -> Admin Master, Investigation Master -> Investigation Masters,
       Master Management -> Master, Lab New -> Sample Processing
    3. Create missing tabs   4. Move menus (matched by URL) and set menu order   5. Set tab order
    6. Delete tabs left without any menu
    Role and user rights use SubMenuId, so nobody loses access.
    After running: delete Redis keys _UserTabMenu_*, _UserWiseMenuMapping_*, _RoleWiseMenuMapping_*
    and sign in again.                                                                              */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.NavigationTabMaster_Bak_20261126') IS NULL
    SELECT * INTO dbo.NavigationTabMaster_Bak_20261126 FROM dbo.NavigationTabMaster;
IF OBJECT_ID('dbo.NavigationSubMenuMaster_Bak_20261126') IS NULL
    SELECT * INTO dbo.NavigationSubMenuMaster_Bak_20261126 FROM dbo.NavigationSubMenuMaster;

DECLARE @tab TABLE (TabName nvarchar(100) PRIMARY KEY, OldName nvarchar(100), FaIconId int, Seq int);
INSERT @tab VALUES
    ('Billing', NULL, 756, 1),
    ('Sample Processing', 'Lab New', 800, 2),
    ('EMR', NULL, 1283, 3),
    ('Patient Status', NULL, 158, 4),
    ('Stock Receiving', NULL, 221, 5),
    ('Purchase Request', NULL, 311, 6),
    ('Purchase', NULL, 312, 7),
    ('Reprint', NULL, 1450, 8),
    ('Utility', NULL, 1556, 9),
    ('MIS Dashboard', NULL, 341, 10),
    ('Doctor', NULL, 1917, 11),
    ('Investigation Masters', 'Investigation Master', 1222, 12),
    ('Rate Master', NULL, 1784, 13),
    ('Master', 'Master Management', 532, 14),
    ('Admin Master', 'Admin', 1931, 15),
    ('MRD Scheduler', NULL, 808, 16);

DECLARE @menu TABLE (Url nvarchar(200) PRIMARY KEY, TabName nvarchar(100), Seq int);
INSERT @menu VALUES
    ('patient-registration', 'Billing', 1),
    ('opd-billing', 'Billing', 2),
    ('ipd-billing', 'Billing', 3),
    ('ipd-admission', 'Billing', 4),
    ('opd-appointment', 'Billing', 5),
    ('opd-appointment-confirmation', 'Billing', 6),
    ('patient-advance', 'Billing', 7),
    ('op-payment-collection', 'Billing', 8),
    ('medicine-sales', 'Billing', 9),
    ('opd-refund', 'Billing', 10),
    ('op-refund-approval', 'Billing', 11),
    ('op-refund-payment', 'Billing', 12),
    ('ot-booking', 'Billing', 13),
    ('ot-schedular', 'Billing', 14),
    ('ivf-admission', 'Billing', 15),
    ('corporate-transfer-approval', 'Billing', 16),
    ('credit-note', 'Billing', 17),
    ('credit-note-generation', 'Billing', 18),
    ('credit-note-approval', 'Billing', 19),
    ('write-off', 'Billing', 20),
    ('write-off-generation', 'Billing', 21),
    ('write-off-approval', 'Billing', 22),
    ('sample-management', 'Sample Processing', 1),
    ('department-receive', 'Sample Processing', 2),
    ('result-entry-radiology', 'Sample Processing', 3),
    ('result-entry-pathology', 'Sample Processing', 4),
    ('micro-result-entry', 'Sample Processing', 6),
    ('histo-result-entry', 'Sample Processing', 7),
    ('lab-work-sheet', 'Sample Processing', 8),
    ('laboratory-help', 'Sample Processing', 9),
    ('send-outsource', 'Sample Processing', 10),
    ('doctor-consultation-new', 'EMR', 1),
    ('temperature-room', 'EMR', 2),
    ('allergy-result-entry', 'EMR', 3),
    ('ot-booking-details', 'Patient Status', 1),
    ('stock-receiving', 'Stock Receiving', 1),
    ('purchase-request', 'Purchase Request', 1),
    ('purchase-request-approval', 'Purchase Request', 2),
    ('purchase-order', 'Purchase', 1),
    ('purchase-order-approval', 'Purchase', 2),
    ('approval-authority-master', 'Purchase', 3),
    ('bill-receipt-reprint', 'Reprint', 1),
    ('op-discount-approval', 'Utility', 1),
    ('mis-dashboard', 'MIS Dashboard', 2),
    ('bed-details', 'MIS Dashboard', 3),
    ('bed-info', 'MIS Dashboard', 4),
    ('doctor-master', 'Doctor', 1),
    ('refer-doctor-master', 'Doctor', 2),
    ('lab-investigation-master', 'Investigation Masters', 1),
    ('investigation-observation-mapping', 'Investigation Masters', 2),
    ('template-investigation-interpretation', 'Investigation Masters', 3),
    ('formula-master', 'Investigation Masters', 4),
    ('lab-master', 'Investigation Masters', 5),
    ('micro-report-master', 'Investigation Masters', 6),
    ('histo-report-master', 'Investigation Masters', 7),
    ('rate-list-master', 'Rate Master', 1),
    ('tariff-manager', 'Rate Master', 2),
    ('import-tariffs', 'Rate Master', 3),
    ('user-master', 'Master', 1),
    ('user-authorization', 'Master', 2),
    ('role-master', 'Master', 3),
    ('user-group', 'Master', 4),
    ('user-department', 'Master', 5),
    ('service-master', 'Master', 6),
    ('store-item-master', 'Master', 7),
    ('vendor-master', 'Master', 8),
    ('medical-store-master', 'Master', 9),
    ('bank-master', 'Master', 10),
    ('bed-master', 'Master', 11),
    ('opd-package-master', 'Master', 12),
    ('ipd-package-master', 'Master', 13),
    ('surgery-master', 'Master', 14),
    ('ot-master', 'Master', 15),
    ('ot-process-master', 'Master', 16),
    ('discharge-process-master', 'Master', 17),
    ('document-master', 'Master', 18),
    ('patient-document', 'Master', 19),
    ('patient-form-master', 'Master', 20),
    ('consultation-header-master', 'Master', 21),
    ('emr-controls', 'Master', 22),
    ('emr-templates', 'Master', 23),
    ('allergy-master', 'Master', 24),
    ('vital-master', 'Master', 25),
    ('procedure-master', 'Master', 26),
    ('mrd-location', 'Master', 27),
    ('refer-lab-master', 'Master', 28),
    ('print-settings', 'Master', 29),
    ('navigation-pane', 'Master', 30),
    ('tab-master', 'Master', 31),
    ('control-test', 'Master', 32),
    ('gravity-master', 'Master', 33),
    ('branch-master', 'Admin Master', 1),
    ('branch-settings', 'Admin Master', 2),
    ('company-setting', 'Admin Master', 3),
    ('corporate-master', 'Admin Master', 4),
    ('location-master', 'Admin Master', 5),
    ('discount-approval-master', 'Admin Master', 6),
    ('user-wise-discount-master', 'Admin Master', 7),
    ('diagnosis-master', 'MRD Scheduler', 1);

BEGIN TRANSACTION;

UPDATE t SET TabName = n.TabName, LastModifiedOn = GETDATE()
FROM dbo.NavigationTabMaster t
JOIN @tab n ON LTRIM(RTRIM(t.TabName)) = n.OldName
WHERE NOT EXISTS (SELECT 1 FROM dbo.NavigationTabMaster x WHERE LTRIM(RTRIM(x.TabName)) = n.TabName);

INSERT INTO dbo.NavigationTabMaster (HospId, FaIconId, TabName, IsActive, CreatedBy, CreatedOn, SequenceNo)
SELECT 1, n.FaIconId, n.TabName, 1, 1, GETDATE(), n.Seq
FROM @tab n
WHERE NOT EXISTS (SELECT 1 FROM dbo.NavigationTabMaster t WHERE LTRIM(RTRIM(t.TabName)) = n.TabName);
PRINT CONCAT('Tabs created: ', @@ROWCOUNT);

UPDATE t SET TabName = n.TabName, SequenceNo = n.Seq, IsActive = 1, LastModifiedOn = GETDATE()
FROM dbo.NavigationTabMaster t JOIN @tab n ON LTRIM(RTRIM(t.TabName)) = n.TabName;

UPDATE s SET TabId = t.TabId, SequenceNo = m.Seq, LastModifiedOn = GETDATE()
FROM dbo.NavigationSubMenuMaster s
JOIN @menu m ON m.Url = LTRIM(RTRIM(s.URL))
CROSS APPLY (SELECT MIN(TabId) TabId FROM dbo.NavigationTabMaster WHERE LTRIM(RTRIM(TabName)) = m.TabName) t;
PRINT CONCAT('Menus arranged: ', @@ROWCOUNT);

DELETE t FROM dbo.NavigationTabMaster t
WHERE NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster s WHERE s.TabId = t.TabId)
  AND LTRIM(RTRIM(t.TabName)) NOT IN (SELECT TabName FROM @tab);
PRINT CONCAT('Empty tabs deleted: ', @@ROWCOUNT);

COMMIT TRANSACTION;

-- menus on this server that the script does not know (left where they were)
SELECT t.TabName, s.SubMenuName, s.URL
FROM dbo.NavigationSubMenuMaster s LEFT JOIN dbo.NavigationTabMaster t ON t.TabId = s.TabId
WHERE ISNULL(LTRIM(RTRIM(s.URL)), '') NOT IN (SELECT Url FROM @menu);
