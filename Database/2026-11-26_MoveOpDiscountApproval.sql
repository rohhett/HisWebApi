/*  Move "OP Discount Approval" into the Billing tab, just before "OP Payment Collection"
    (owner 2026-11-26). The Utility tab is then empty and is deleted.
    Run after 2026-11-26_ArrangeNavigationTabs.sql.
    Target: GWT_HIS_API. Re-runnable: yes. User and role rights are unchanged (mapped by SubMenuId).
    After running: delete Redis keys _UserTabMenu_*, _UserWiseMenuMapping_*, _RoleWiseMenuMapping_*. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @tabId int = (SELECT MIN(TabId) FROM dbo.NavigationTabMaster WHERE LTRIM(RTRIM(TabName)) = 'Billing');
DECLARE @seq int = (SELECT MIN(SequenceNo) FROM dbo.NavigationSubMenuMaster
                    WHERE TabId = @tabId AND URL = 'op-payment-collection');
IF @tabId IS NULL OR @seq IS NULL
BEGIN
    PRINT 'Billing tab or OP Payment Collection menu not found.';
    RETURN;
END

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster
               WHERE URL = 'op-discount-approval' AND TabId = @tabId AND SequenceNo = @seq - 1)
   AND EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'op-discount-approval')
BEGIN
    UPDATE dbo.NavigationSubMenuMaster SET SequenceNo = SequenceNo + 1
    WHERE TabId = @tabId AND SequenceNo >= @seq AND URL <> 'op-discount-approval';

    UPDATE dbo.NavigationSubMenuMaster SET TabId = @tabId, SequenceNo = @seq, LastModifiedOn = GETDATE()
    WHERE URL = 'op-discount-approval';
    PRINT CONCAT('Menus moved: ', @@ROWCOUNT);
END

DELETE t FROM dbo.NavigationTabMaster t
WHERE LTRIM(RTRIM(t.TabName)) = 'Utility'
  AND NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster s WHERE s.TabId = t.TabId);
PRINT CONCAT('Empty Utility tab deleted: ', @@ROWCOUNT);

COMMIT TRANSACTION;
