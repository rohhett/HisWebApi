using System.Data;
using Microsoft.Data.SqlClient;
using HISWEBAPI.Data.Helpers;

namespace HISWEBAPI.Domain
{
    public class FinancialTransactionDetails
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int FTID { get; set; }
        public int? VisitId { get; set; }
        public int? BillId { get; set; }
        public int? PatientId { get; set; }
        public int ServiceItemId { get; set; }
        public int SubSubCategoryId { get; set; }
        public string ServiceName { get; set; }
        public string ServiceCode { get; set; }
        public string Remarks { get; set; }
        public string CorporateAlias { get; set; }
        public string CorporateCode { get; set; }
        public int? DoctorId { get; set; }
        public int? PerformingDoctorId { get; set; }
        public int? CorporateId { get; set; }
        public decimal Rate { get; set; }
        public decimal Qty { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal TotalTaxPer { get; set; }
        public decimal TotalTaxAmt { get; set; }
        public decimal NetAmt { get; set; }
        public int IsCorporateNonPayable { get; set; }
        public int IsUnderPackage { get; set; }
        public string DiscountReason { get; set; }
        public int RateListId { get; set; }
        public int UserId { get; set; }
        public long? StockId { get; set; }
        public long? EquipmentId { get; set; }
        public string IpAddress { get; set; }
        public int FromFTDID { get; set; }
        public int PackageId { get; set; }
        public string BillingDate { get; set; }
        public decimal SpecialDiscPer { get; set; }
        public decimal SpecialDiscAmt { get; set; }
        public int Deal1 { get; set; }
        public int Deal2 { get; set; }
        public decimal GstPer { get; set; }
        public decimal GstAmt { get; set; }

        public dynamic Create(ICustomSqlHelper sqlHelper, SqlTransaction tnx)
        {
            return sqlHelper.DML(tnx, "I_FinancialTransactionDetails", CommandType.StoredProcedure, new
            {
                @hospId = HospId,
                @branchId = BranchId,
                @FTID = FTID,
                @visitId = VisitId,
                @billId = BillId,
                @patientId = PatientId,
                @serviceItemId = ServiceItemId,
                @subSubCategoryId = SubSubCategoryId,
                @serviceName = ServiceName,
                @serviceCode = ServiceCode,
                @remarks = Remarks,
                @corporateAlias = CorporateAlias,
                @corporateCode = CorporateCode,
                @doctorId = DoctorId,
                @performingDoctorId = PerformingDoctorId,
                @corporateId = CorporateId,
                @rate = Rate,
                @qty = Qty,
                @grossAmt = GrossAmt,
                @discPer = DiscPer,
                @discAmt = DiscAmt,
                @totalTaxPer = TotalTaxPer,
                @totalTaxAmt = TotalTaxAmt,
                @netAmt = NetAmt,
                @isCorporateNonPayable = IsCorporateNonPayable,
                @isUnderPackage = IsUnderPackage,
                @discountReason = DiscountReason,
                @rateListId = RateListId,
                @stockId = StockId,
                @EquipmentId = EquipmentId,
                @userId = UserId,
                @IpAddress = IpAddress,
                @fromFTDID = FromFTDID,
                @packageId = PackageId,
                @billingDate = BillingDate,
                @specialDiscPer = SpecialDiscPer,
                @specialDiscAmt = SpecialDiscAmt,
                @deal1 = Deal1,
                @deal2 = Deal2,
                @gstPer = GstPer,
                @gstAmt = GstAmt
            }, new { result = 0 });
        }
    }
}