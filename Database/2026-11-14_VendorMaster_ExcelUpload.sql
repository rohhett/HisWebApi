/* =====================================================================
   Medical Vendor Master - Excel upload (validate rows)  -  2026-11-14
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (CREATE OR ALTER)

   Used by  : POST api/Store/vendorExcelUpload  (StoreRepository.VendorExcelUpload)
   Saving   : each valid row is saved with the existing IU_VendorMaster (no change to it),
              all rows in one transaction. Branches are chosen once in the upload popup,
              uploaded vendors are always active, and the location comes only from the pincode.

   S_ValidateVendorUpload
     @hospId   int
     @rowsJson nvarchar(max)  JSON array: [{ "RowNo":1, "VendorName":"", "Pincode":"" }, ...]
     Result set 1 - one row per input row:
               RowNo,
               PinCountryId, PinStateId, PinDistrictId, PinCityId,
               PinCountryName, PinStateName, PinDistrictName, PinCityName   (from the pincode, same joins as
                                                                             S_getLocationByPincode; NULL when not found)
               ExistingVendorId                                             (VendorMaster row with the same name, or NULL)
     Result set 2 - active branches of the hospital: BranchId, BranchName
   ===================================================================== */

CREATE OR ALTER PROCEDURE dbo.S_ValidateVendorUpload
(
    @hospId   int,
    @rowsJson nvarchar(max)
)
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH r AS
    (
        SELECT  j.RowNo,
                LTRIM(RTRIM(ISNULL(j.VendorName, ''))) AS VendorName,
                TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(j.Pincode, ''))), '') AS int) AS Pincode
        FROM OPENJSON(@rowsJson)
             WITH ( RowNo int, VendorName nvarchar(256), Pincode nvarchar(20) ) j
    )
    SELECT  r.RowNo,
            p.CountryId  AS PinCountryId,  p.StateId  AS PinStateId,
            p.DistrictId AS PinDistrictId, p.CityId   AS PinCityId,
            p.CountryName AS PinCountryName, p.StateName AS PinStateName,
            p.DistrictName AS PinDistrictName, p.CityName AS PinCityName,
            v.VendorId   AS ExistingVendorId
    FROM r
    OUTER APPLY (SELECT TOP 1 cm.CountryId, cm.CountryName, sm.StateId, sm.StateName,
                        dm.DistrictId, dm.DistrictName, cy.CityId, cy.CityName
                 FROM dbo.CountryMaster cm
                 INNER JOIN dbo.StateMaster sm    ON sm.CountryId = cm.CountryId
                 INNER JOIN dbo.DistrictMaster dm ON dm.StateId = sm.StateId
                 INNER JOIN dbo.CityMaster cy     ON cy.DistrictId = dm.DistrictId
                 INNER JOIN dbo.PincodeMaster pm  ON pm.CityId = cy.CityId
                 WHERE r.Pincode IS NOT NULL AND pm.Pincode = r.Pincode
                   AND cm.IsActive = 1 AND sm.IsActive = 1 AND dm.IsActive = 1 AND cy.IsActive = 1 AND pm.IsActive = 1) p
    OUTER APPLY (SELECT TOP 1 vm.VendorId FROM dbo.VendorMaster vm
                 WHERE r.VendorName <> '' AND vm.VendorName = r.VendorName
                 ORDER BY vm.VendorId) v
    ORDER BY r.RowNo;

    SELECT BranchId, BranchName
    FROM   dbo.BranchMaster
    WHERE  HospId = @hospId AND IsActive = 1
    ORDER BY BranchId;
END
GO

PRINT 'S_ValidateVendorUpload created/updated.';
GO
