/* =====================================================================================
   2026-11-16  Purchase Order Approval - page config (grid / list cards) in PageConfigMaster
   ConfigKey : purchaseOrderApprovalConfig   (read by useConfigMaster on the Purchase Order Approval screen)
   Same content as Frontend src/config/masterConfig/purchaseOrderApprovalConfig.ts (the file is the fallback).
   Re-runnable: inserts the row, or updates ConfigJson when the key already exists.
   AFTER RUNNING: clear Redis key  _PageConfig_All  (API caches every page config there without expiry),
   e.g.  redis-cli DEL _PageConfig_All
   ===================================================================================== */
SET NOCOUNT ON;

DECLARE @key  nvarchar(256) = N'purchaseOrderApprovalConfig';
DECLARE @json nvarchar(max) = N'{
  "type": "purchaseOrderApproval",
  "gridCardView": {
    "type": "purchaseOrderApproval",
    "cardType": "purchaseOrderApprovalGrid",
    "cardViewType": "grid",
    "recordIdKey": "poId",
    "gridLeftTop": [
      {
        "label": "Status",
        "keyFromApi": "status"
      }
    ],
    "gridRightTop": [
      {
        "label": "toggle",
        "action": "ListToggleButton"
      }
    ],
    "gridAvatar": [
      {
        "label": "profile",
        "keyFromApi": null
      }
    ],
    "gridId": [
      {
        "label": "PO No",
        "keyFromApi": "poNo"
      }
    ],
    "gridTitle": [
      {
        "label": "PO No",
        "keyFromApi": "poNo"
      }
    ],
    "gridFooterSection": [
      {
        "label": "Vendor",
        "keyFromApi": "vendorName"
      },
      {
        "label": "Store",
        "keyFromApi": "fromDeptName"
      },
      {
        "label": "PO Date",
        "keyFromApi": "poDate"
      },
      {
        "label": "Type",
        "keyFromApi": "poTypes"
      },
      {
        "label": "Priority",
        "keyFromApi": "priority"
      },
      {
        "label": "Items",
        "keyFromApi": "itemCount"
      },
      {
        "label": "GST",
        "keyFromApi": "gstAmountText"
      },
      {
        "label": "Net Amount",
        "keyFromApi": "netAmountText"
      }
    ],
    "gridButtonSection": [
      {
        "label": "Approve",
        "action": "toggleApprovePurchaseOrder"
      },
      {
        "label": "Cancel",
        "action": "toggleCancelPurchaseOrder"
      }
    ]
  },
  "listCardView": {
    "type": "purchaseOrderApproval",
    "cardType": "purchaseOrderApprovalList",
    "cardViewType": "list",
    "recordIdKey": "poId",
    "listLeftButton": [
      {
        "label": "Action",
        "action": "toggleApprovePurchaseOrder"
      }
    ],
    "columns": [
      {
        "label": "PO No",
        "keyFromApi": "poNo",
        "isSortable": true,
        "isSearchable": true,
        "allowColumnFilter": true
      },
      {
        "label": "PO Date",
        "keyFromApi": "poDate",
        "isSortable": true
      },
      {
        "label": "Vendor",
        "keyFromApi": "vendorName",
        "isSortable": true,
        "isSearchable": true,
        "allowColumnFilter": true
      },
      {
        "label": "Store",
        "keyFromApi": "fromDeptName",
        "isSortable": true,
        "isSearchable": true,
        "allowColumnFilter": true
      },
      {
        "label": "Store Type",
        "keyFromApi": "storeType",
        "allowColumnFilter": true
      },
      {
        "label": "Type",
        "keyFromApi": "poTypes",
        "allowColumnFilter": true
      },
      {
        "label": "PO Type",
        "keyFromApi": "poType",
        "allowColumnFilter": true
      },
      {
        "label": "Priority",
        "keyFromApi": "priority",
        "allowColumnFilter": true
      },
      {
        "label": "Status",
        "keyFromApi": "status",
        "allowColumnFilter": true
      },
      {
        "label": "Pending With",
        "keyFromApi": "currentApprover",
        "isSearchable": true
      },
      {
        "label": "Items",
        "keyFromApi": "itemCount"
      },
      {
        "label": "Total Qty",
        "keyFromApi": "totalQty"
      },
      {
        "label": "Gross Amount",
        "keyFromApi": "grossAmountText"
      },
      {
        "label": "GST Amount",
        "keyFromApi": "gstAmountText"
      },
      {
        "label": "Net Amount",
        "keyFromApi": "netAmountText"
      },
      {
        "label": "Approval Slab Up To",
        "keyFromApi": "amountUpToText"
      },
      {
        "label": "Remarks",
        "keyFromApi": "remarks"
      },
      {
        "label": "Approval Remarks",
        "keyFromApi": "approvalRemarks"
      },
      {
        "label": "Level 1 Approved",
        "keyFromApi": "isLevel1Approve"
      },
      {
        "label": "Level 1 Approve On",
        "keyFromApi": "level1ApproveOn"
      },
      {
        "label": "Level 2 Approved",
        "keyFromApi": "isLevel2Approve"
      },
      {
        "label": "Level 2 Approve On",
        "keyFromApi": "level2ApproveOn"
      },
      {
        "label": "Level 3 Approved",
        "keyFromApi": "isLevel3Approve"
      },
      {
        "label": "Level 3 Approve On",
        "keyFromApi": "level3ApproveOn"
      },
      {
        "label": "Level 4 Approved",
        "keyFromApi": "isLevel4Approve"
      },
      {
        "label": "Level 4 Approve On",
        "keyFromApi": "level4ApproveOn"
      },
      {
        "label": "Cancel By",
        "keyFromApi": "cancelBy"
      },
      {
        "label": "Cancel On",
        "keyFromApi": "cancelOn"
      },
      {
        "label": "Cancel Reason",
        "keyFromApi": "cancelReason"
      },
      {
        "label": "Created By",
        "keyFromApi": "createdBy",
        "isSearchable": true
      },
      {
        "label": "Created On",
        "keyFromApi": "createdOn"
      },
      {
        "label": "Can Approve",
        "keyFromApi": "canApprove"
      }
    ]
  }
}';

IF ISJSON(@json) <> 1
    THROW 50001, 'purchaseOrderApprovalConfig: ConfigJson is not valid JSON', 1;

IF EXISTS (SELECT 1 FROM dbo.PageConfigMaster WHERE ConfigKey = @key)
BEGIN
    UPDATE dbo.PageConfigMaster
       SET ConfigJson = @json, IsActive = 1, LastModifiedBy = 1, LastModifiedOn = GETDATE(), IpAddress = N'127.0.0.1'
     WHERE ConfigKey = @key;
    PRINT 'purchaseOrderApprovalConfig updated';
END
ELSE
BEGIN
    INSERT INTO dbo.PageConfigMaster (ConfigKey, ConfigJson, IsActive, CreatedBy, CreatedOn, IpAddress)
    VALUES (@key, @json, 1, 1, GETDATE(), N'127.0.0.1');
    PRINT 'purchaseOrderApprovalConfig inserted';
END
GO
