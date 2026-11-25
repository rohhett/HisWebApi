/* =====================================================================
   IPD tab icons (IPDTabMaster.FaIconId)  -  2026-11-24
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (only fills tabs that have no icon yet)
   Run after : 2026-11-24_ConsentForm.sql

   Icons come from dbo.FaIconMaster (matched by IconClass). Tabs that already have
   an icon keep it (chosen earlier by the owner); only NULL / 0 icons are filled:
     Add Service        -> fa-solid fa-circle-plus
     Consent Form       -> fa-solid fa-file-signature
     Doctor-tab         -> fa-solid fa-user-doctor
   Test rows (string, New Tab ...) are not touched.
   ===================================================================== */
SET NOCOUNT ON;

DECLARE @map TABLE (TabViewURL nvarchar(2000), IconClass nvarchar(200));
INSERT INTO @map (TabViewURL, IconClass) VALUES
    ('add-service',  'fa-solid fa-circle-plus'),
    ('consent-form', 'fa-solid fa-file-signature'),
    ('doctor-tab',   'fa-solid fa-user-doctor');

UPDATE t
SET    t.FaIconId       = f.Id,
       t.LastModifiedOn = GETDATE()
FROM   dbo.IPDTabMaster t
JOIN   @map m          ON m.TabViewURL = t.TabViewURL
JOIN   dbo.FaIconMaster f ON f.IconClass = m.IconClass AND f.IsActive = 1
WHERE  ISNULL(t.FaIconId, 0) = 0;

PRINT CONCAT('IPD tab icons filled: ', @@ROWCOUNT);

SELECT t.TabId, t.TabName, t.TabViewURL, t.FaIconId, f.IconClass
FROM   dbo.IPDTabMaster t
LEFT JOIN dbo.FaIconMaster f ON f.Id = t.FaIconId
WHERE  t.IsActive = 1 AND t.TabTypeId IN (1, 2) AND t.TabViewURL IN ('add-service', 'consent-form', 'doctor-tab');
GO
