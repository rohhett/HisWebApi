/* =====================================================================================
   2026-11-16  Approve Purchase Request - page config (grid / list cards) in PageConfigMaster
   ConfigKey : purchaseRequestApprovalConfig   (read by useConfigMaster on the Approve Purchase Request screen)
   Same content as Frontend src/config/masterConfig/purchaseRequestApprovalConfig.ts (the file is the fallback).
   Re-runnable: inserts the row, or updates ConfigJson when the key already exists.
   Updated 2026-11-16: amount columns (totalAmountText / approvedAmountText / amountUpToText, formatted by the screen).
   AFTER RUNNING: clear Redis key  _PageConfig_All  (API caches every page config there without expiry),
   e.g.  redis-cli DEL _PageConfig_All
   ===================================================================================== */
SET NOCOUNT ON;

DECLARE @key  nvarchar(256) = N'purchaseRequestApprovalConfig';
DECLARE @json nvarchar(max) = N'{
  "type": "purchaseRequestApproval",
  "gridCardView": {
    "type": "purchaseRequestApproval",
    "cardType": "purchaseRequestApprovalGrid",
    "cardViewType": "grid",
    "recordIdKey": "purchaseRequestId",
    "gridLeftTop": [{ "label": "Status", "keyFromApi": "status" }],
    "gridRightTop": [{ "label": "toggle", "action": "ListToggleButton" }],
    "gridAvatar": [{ "label": "profile", "keyFromApi": null }],
    "gridId": [{ "label": "PR No", "keyFromApi": "purchaseRequestNo" }],
    "gridTitle": [{ "label": "PR No", "keyFromApi": "purchaseRequestNo" }],
    "gridFooterSection": [
      { "label": "From Dept", "keyFromApi": "fromDeptName" },
      { "label": "To Dept", "keyFromApi": "toDeptName" },
      { "label": "Store Type", "keyFromApi": "storeType" },
      { "label": "PR Date", "keyFromApi": "prDate" },
      { "label": "Items", "keyFromApi": "itemCount" },
      { "label": "Req. Qty", "keyFromApi": "requestedQty" },
      { "label": "Amount", "keyFromApi": "totalAmountText" },
      { "label": "Appr. Amount", "keyFromApi": "approvedAmountText" }
    ],
    "gridButtonSection": [
      { "label": "Approve", "action": "toggleApprovePurchaseRequest" },
      { "label": "Cancel", "action": "toggleCancelPurchaseRequest" }
    ]
  },
  "listCardView": {
    "type": "purchaseRequestApproval",
    "cardType": "purchaseRequestApprovalList",
    "cardViewType": "list",
    "recordIdKey": "purchaseRequestId",
    "listLeftButton": [{ "label": "Action", "action": "toggleApprovePurchaseRequest" }],
    "columns": [
      { "label": "PR No", "keyFromApi": "purchaseRequestNo", "isSortable": true, "isSearchable": true, "allowColumnFilter": true },
      { "label": "PR Date", "keyFromApi": "prDate", "isSortable": true },
      { "label": "From Department", "keyFromApi": "fromDeptName", "isSortable": true, "isSearchable": true, "allowColumnFilter": true },
      { "label": "To Department", "keyFromApi": "toDeptName", "isSortable": true, "isSearchable": true, "allowColumnFilter": true },
      { "label": "Store Type", "keyFromApi": "storeType", "allowColumnFilter": true },
      { "label": "Status", "keyFromApi": "status", "allowColumnFilter": true },
      { "label": "Pending With", "keyFromApi": "currentApprover", "isSearchable": true },
      { "label": "Items", "keyFromApi": "itemCount" },
      { "label": "Requested Qty", "keyFromApi": "requestedQty" },
      { "label": "Approved Qty", "keyFromApi": "approvedQty" },
      { "label": "Amount", "keyFromApi": "totalAmountText" },
      { "label": "Approved Amount", "keyFromApi": "approvedAmountText" },
      { "label": "Approval Slab Up To", "keyFromApi": "amountUpToText" },
      { "label": "Remarks", "keyFromApi": "remarks" },
      { "label": "Approval Remarks", "keyFromApi": "approvalRemarks" },
      { "label": "Level 1 Approved", "keyFromApi": "isLevel1Approve" },
      { "label": "Level 1 Approve On", "keyFromApi": "level1ApproveOn" },
      { "label": "Level 2 Approved", "keyFromApi": "isLevel2Approve" },
      { "label": "Level 2 Approve On", "keyFromApi": "level2ApproveOn" },
      { "label": "Level 3 Approved", "keyFromApi": "isLevel3Approve" },
      { "label": "Level 3 Approve On", "keyFromApi": "level3ApproveOn" },
      { "label": "Level 4 Approved", "keyFromApi": "isLevel4Approve" },
      { "label": "Level 4 Approve On", "keyFromApi": "level4ApproveOn" },
      { "label": "Cancel By", "keyFromApi": "cancelBy" },
      { "label": "Cancel On", "keyFromApi": "cancelOn" },
      { "label": "Cancel Reason", "keyFromApi": "cancelReason" },
      { "label": "Created By", "keyFromApi": "createdBy", "isSearchable": true },
      { "label": "Created On", "keyFromApi": "createdOn" },
      { "label": "Can Approve", "keyFromApi": "canApprove" }
    ]
  }
}';

IF ISJSON(@json) <> 1
    THROW 50001, 'purchaseRequestApprovalConfig: ConfigJson is not valid JSON', 1;

IF EXISTS (SELECT 1 FROM dbo.PageConfigMaster WHERE ConfigKey = @key)
BEGIN
    UPDATE dbo.PageConfigMaster
       SET ConfigJson = @json, IsActive = 1, LastModifiedBy = 1, LastModifiedOn = GETDATE(), IpAddress = N'127.0.0.1'
     WHERE ConfigKey = @key;
    PRINT 'purchaseRequestApprovalConfig updated';
END
ELSE
BEGIN
    INSERT INTO dbo.PageConfigMaster (ConfigKey, ConfigJson, IsActive, CreatedBy, CreatedOn, IpAddress)
    VALUES (@key, @json, 1, 1, GETDATE(), N'127.0.0.1');
    PRINT 'purchaseRequestApprovalConfig inserted';
END
GO
