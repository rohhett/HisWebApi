/*  Delete 7 unused roles (owner request 2026-11-26).
    RoleIds 19 CT-Scan, 24 New, 27 Canteen, 31 string, 1014 XXXXXXXX, 2014 Day Care, 3014 SURENDRA.
    Also removes their user assignments (UserRoleMapping). Deleted rows are copied first to
    RoleMaster_Deleted_20261126 and UserRoleMapping_Deleted_20261126.
    A role is deleted only if its id AND name match and it has no menu mapped, so a server
    with different data is not touched. Re-runnable.
    After running, delete the Redis key _RoleMaster_All (and _UserRoleMapping_* keys).            */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @list TABLE (RoleId int PRIMARY KEY, RoleName nvarchar(200));
INSERT @list VALUES (19, 'CT-Scan'), (24, 'New'), (27, 'Canteen'), (31, 'string'),
                    (1014, 'XXXXXXXX'), (2014, 'Day Care'), (3014, 'SURENDRA');

DECLARE @del TABLE (RoleId int PRIMARY KEY);
INSERT @del
SELECT r.RoleId
FROM dbo.RoleMaster r
JOIN @list l ON l.RoleId = r.RoleId AND LTRIM(RTRIM(r.RoleName)) = l.RoleName
WHERE NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster m WHERE m.RoleId = r.RoleId)
  AND NOT EXISTS (SELECT 1 FROM dbo.UserMenuMaster u WHERE u.RoleId = r.RoleId AND u.IsActive = 1);

-- empty copies for the backup (UNION ALL drops the identity property)
IF OBJECT_ID('dbo.RoleMaster_Deleted_20261126') IS NULL
    SELECT * INTO dbo.RoleMaster_Deleted_20261126 FROM dbo.RoleMaster WHERE 1 = 0
    UNION ALL SELECT * FROM dbo.RoleMaster WHERE 1 = 0;
IF OBJECT_ID('dbo.UserRoleMapping_Deleted_20261126') IS NULL
    SELECT * INTO dbo.UserRoleMapping_Deleted_20261126 FROM dbo.UserRoleMapping WHERE 1 = 0
    UNION ALL SELECT * FROM dbo.UserRoleMapping WHERE 1 = 0;

BEGIN TRANSACTION;

INSERT INTO dbo.RoleMaster_Deleted_20261126
SELECT r.* FROM dbo.RoleMaster r WHERE r.RoleId IN (SELECT RoleId FROM @del);

INSERT INTO dbo.UserRoleMapping_Deleted_20261126
SELECT m.* FROM dbo.UserRoleMapping m WHERE m.RoleId IN (SELECT RoleId FROM @del);

DELETE FROM dbo.UserRoleMapping WHERE RoleId IN (SELECT RoleId FROM @del);
PRINT CONCAT('UserRoleMapping rows deleted: ', @@ROWCOUNT);

DELETE FROM dbo.RoleMaster WHERE RoleId IN (SELECT RoleId FROM @del);
PRINT CONCAT('Roles deleted: ', @@ROWCOUNT);

COMMIT TRANSACTION;

