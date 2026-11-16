/* =====================================================================
   Medical Vendor Master - Credit Days  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Design field "Credit Days" (MediStock Supplier master; knowledge base conflict C18: OLD supplier fields kept,
   design's credit days added). Optional, whole days 0-365.

   Objects
     1. TABLE  VendorMaster              ADD CreditDays int NULL
     2. PROC   IU_VendorMaster           + @creditDays int = NULL (default keeps any other caller working);
                                         otherwise the existing logic unchanged (-1 = vendor name exists)
     3. PROC   S_getdllVendorMasterList  + CreditDays column
   Callers checked: both procedures are used only by StoreRepository (Store/createUpdateVendorMaster, getVendorMasterList).
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Column ---------- */
IF COL_LENGTH('dbo.VendorMaster', 'CreditDays') IS NULL
    ALTER TABLE dbo.VendorMaster ADD CreditDays int NULL;
GO

/* ---------- 2. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE [dbo].[IU_VendorMaster]
(
  @hospId int,
  @typeId int,
  @type nvarchar(20),
  @vendorId int,
  @vendorName nvarchar(256),
  @contactNo nvarchar(30)=Null,
  @email nvarchar(200)=Null,
  @dlNo nvarchar(200)=Null,
  @gstinNo nvarchar(200)=Null,
  @address nvarchar(1000) =Null,
  @countryId int,
  @stateId int,
  @districtId int,
  @cityId int,
  @isActive int,
  @mappingBranch nvarchar(1000),
  @userId int,
  @Pincode int,
  @IpAddress nvarchar(20),
  @creditDays int = NULL,
  @Result int=null output
)
 as
 begin
 declare @PrintAddress nvarchar(1024)='';
 declare @CountryName nvarchar(128)='';
 declare @StateName nvarchar(128)='';
 declare @DistrictName nvarchar(128)='';
 declare @CityName nvarchar(128)='';

 begin
 select top 1
			@CountryName=cm.CountryName,
			@StateName=sm.StateName,
			@DistrictName=dm.DistrictName,
			@CityName=city.CityName
			from CountryMaster CM
			inner join StateMaster SM on SM.CountryId=cm.CountryId
			inner join DistrictMaster DM on DM.StateId=sm.StateId
			inner join CityMaster city on city.DistrictId=dm.DistrictId
			where city.CityId=@cityId;

set @PrintAddress=concat(@address,' ',@Pincode,', ',@CityName,', ',@DistrictName,', ',@StateName,', ',@CountryName);

 end

	if exists(select 1 from [dbo].[VendorMaster] where VendorName=@vendorName and VendorId<>@vendorId)
	begin
	SET @Result=-1;
	end
	else
		begin
		if(@vendorId=0)
		begin

		insert into VendorMaster(
			 HospId
			,TypeId
			,Type
			,VendorName
			,ContactNo
			,Email
			,DLNO
			,GSTINNo
			,Address
			,CountryId
			,StateId
			,DistrictId
			,CityId
			,MappingBranch
			,IsActive
			,CreatedBy
			,IpAddress
			,Pincode
			,PrintAddress
			,CreditDays
		)
		values
		(
		@hospId,
		@typeId
		,@type
		,@vendorName
		,@contactNo
		,@email
		,@dlNo
		,@gstinNo
		,@address
		,@countryId
		,@stateId
		,@districtId
		,@cityId
		,@mappingBranch
		,@isActive
		,@userId
		,@IpAddress
		,@Pincode
		,@PrintAddress
		,@creditDays
		)

			SET @Result = SCOPE_IDENTITY();
		end
	else
		begin

		update
			VendorMaster
		set
			 HospId=@hospId
			,TypeId=@typeId
			,Type=@type
			,VendorName=@vendorName
			,ContactNo=@contactNo
			,Email=@email
			,DLNO=@dlNo
			,GSTINNo=@gstinNo
			,Address=@address
			,CountryId=@countryId
			,StateId=@stateId
			,DistrictId=@districtId
			,CityId=@cityId
			,MappingBranch=@mappingBranch
			,IsActive=@isActive
			,LastModifiedBy=@userId
			,LastModifiedOn=GETDATE()
			,IpAddress=@IpAddress
			,Pincode=@Pincode
			,PrintAddress=@PrintAddress
			,CreditDays=@creditDays
		where
			VendorId=@vendorId

			SET @Result=@vendorId
		end
	end
	Select @Result
 end
GO

/* ---------- 3. List ---------- */
CREATE OR ALTER PROCEDURE [dbo].[S_getdllVendorMasterList]
(
  @activeStatus nvarchar(max)
)
 as
 begin
 Select
 VendorId
 ,TypeId
 ,Type
 ,VendorName
 ,ContactNo
 ,Email
 ,DLNO
 ,GSTINNo
 ,PrintAddress as fullAddress
 ,Address
 ,CountryId
 ,StateId
 ,DistrictId
 ,CityId
 ,MappingBranch
 ,IsActive
 ,Pincode
 ,CreditDays
   ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = EVM.CreatedBy) AS CreatedBy
        ,FORMAT(EVM.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
        ,isnull((SELECT TOP 1 name FROM UserMaster um WHERE um.id = EVM.LastModifiedBy),'') AS LastModifiedBy
        ,isnull(FORMAT(EVM.LastModifiedOn, 'dd-MM-yyyy'),'') AS LastModifiedOn
 from VendorMaster EVM
 end
GO
