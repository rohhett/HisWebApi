/*  Move "Allergy Result Entry" from the EMR tab to Sample Processing, after the other result entry
    menus (owner 2026-11-26). Run after 2026-11-26_ArrangeNavigationTabs.sql.
    Target: GWT_HIS_API. Re-runnable: yes. User and role rights are unchanged (mapped by SubMenuId).
    After running: delete Redis keys _UserTabMenu_*, _UserWiseMenuMapping_*, _RoleWiseMenuMapping_*. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @tabId int = (SELECT MIN(TabId) FROM dbo.NavigationTabMaster WHERE LTRIM(RTRIM(TabName)) = 'Sample Processing');
IF @tabId IS NULL
BEGIN
    PRINT 'Tab Sample Processing not found; run 2026-11-26_ArrangeNavigationTabs.sql first.';
    RETURN;
END

IF EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'allergy-result-entry' AND TabId = @tabId)
BEGIN
    PRINT 'Already moved.';
    RETURN;
END

DECLARE @seq int = ISNULL((SELECT MAX(SequenceNo) FROM dbo.NavigationSubMenuMaster
                           WHERE TabId = @tabId AND URL = 'histo-result-entry'),
                          (SELECT ISNULL(MAX(SequenceNo), 0) FROM dbo.NavigationSubMenuMaster WHERE TabId = @tabId));

BEGIN TRANSACTION;

UPDATE dbo.NavigationSubMenuMaster SET SequenceNo = SequenceNo + 1 WHERE TabId = @tabId AND SequenceNo > @seq;

UPDATE dbo.NavigationSubMenuMaster SET TabId = @tabId, SequenceNo = @seq + 1, LastModifiedOn = GETDATE()
WHERE URL = 'allergy-result-entry';
PRINT CONCAT('Menus moved: ', @@ROWCOUNT);

COMMIT TRANSACTION;
