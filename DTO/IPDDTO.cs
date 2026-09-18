using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.DTO
{
    public class TransferIPDPatientBedRequest
    {
        [Required(ErrorMessage = "BillingTypeId is required")]
        public int BillingTypeId { get; set; }

        [Required(ErrorMessage = "RoomTypeId is required")]
        public int RoomTypeId { get; set; }

        [Required(ErrorMessage = "NewBedId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "NewBedId must be greater than 0")]
        public int NewBedId { get; set; }

        [Required(ErrorMessage = "CurrentBedId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CurrentBedId must be greater than 0")]
        public int CurrentBedId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }
    }
    public class TransferIPDPatientDoctorRequest
    {
        [Required(ErrorMessage = "PrimaryDoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PrimaryDoctorId must be greater than 0")]
        public int PrimaryDoctorId { get; set; }

        /// <summary>Optional list of secondary/consulting doctor Ids</summary>
        public List<int> SecondaryDoctorIds { get; set; } = new List<int>();

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }
    }

    public class UpdateIPDPatientTariffDetailsRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }


        [Required(ErrorMessage = "PatientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than 0")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "InsuranceCompanyId is required")]
        public int InsuranceCompanyId { get; set; }

        [Required(ErrorMessage = "BillingTypeId is required")]
        public int BillingTypeId { get; set; }

        [Required(ErrorMessage = "CorporateId is required")]
        public int CorporateId { get; set; }

        /// <summary>1 = also recalculate tariff/billing for the visit after corporate change</summary>
        public int IsChangeTariff { get; set; } = 0;

        [StringLength(50, ErrorMessage = "Relation cannot exceed 50 characters")]
        public string Relation { get; set; }

        [StringLength(256, ErrorMessage = "RelativeName cannot exceed 256 characters")]
        public string RelativeName { get; set; }

        [StringLength(100, ErrorMessage = "CardNo cannot exceed 100 characters")]
        public string CardNo { get; set; }

        /// <summary>Required when IsChangeTariff = 1</summary>
        public string ChangeTariffFromDate { get; set; }

        /// <summary>Required when IsChangeTariff = 1</summary>
        public string ChangeTariffToDate { get; set; }

        [Required(ErrorMessage = "Transfer Date is required")]
        public string TransferDate { get; set; }
        [StringLength(50, ErrorMessage = "Remarks cannot exceed 512 characters")]
        public string Remarks { get; set; }
        [StringLength(50, ErrorMessage = "ReasonForTransfer cannot exceed 256 characters")]
        public string ReasonForTransfer { get; set; }
        [StringLength(50, ErrorMessage = "AuthorizationNumber cannot exceed 100 characters")]
        public string AuthorizationNumber { get; set; }
    }

    // ─── Corporate Transfer ──────────────────────────────────────────────────────

    public class SaveCorporateTransferRequestApprovalRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "InsuranceCompanyId is required")]
        public int InsuranceCompanyId { get; set; }

        [Required(ErrorMessage = "CorporateId is required")]
        public int CorporateId { get; set; }

        [Required(ErrorMessage = "BillingTypeId is required")]
        public int BillingTypeId { get; set; }

        public int IsChangeTariff { get; set; } = 0;
        public string? ChangeFromDate { get; set; }   // dd-MM-yyyy or yyyy-MM-dd
        public string? ChangeToDate { get; set; }

        public string? Relation { get; set; }
        public string? RelativeName { get; set; }
        public string? CardNo { get; set; }
        [Required(ErrorMessage = "Transfer Date is required")]
        public string TransferDate { get; set; }
        [StringLength(50, ErrorMessage = "Remarks cannot exceed 512 characters")]
        public string Remarks { get; set; }
        [StringLength(50, ErrorMessage = "ReasonForTransfer cannot exceed 256 characters")]
        public string ReasonForTransfer { get; set; }
        [StringLength(50, ErrorMessage = "AuthorizationNumber cannot exceed 100 characters")]
        public string AuthorizationNumber { get; set; }
    }

    public class SaveCorporateTransferRequestApprovalResponse
    {
        public int CorporateTransferId { get; set; }
    }

    public class ApproveCorporateTransferRequestRequest
    {
        [Required(ErrorMessage = "CorporateTransferId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CorporateTransferId must be greater than 0")]
        public int CorporateTransferId { get; set; }

        [Required(ErrorMessage = "Flag is required")]
        [Range(1, 4, ErrorMessage = "Flag must be between 1 and 4")]
        public int Flag { get; set; }

        [StringLength(256, ErrorMessage = "ApprovalRemarks cannot exceed 256 characters")]
        public string? ApprovalRemarks { get; set; }
    }

    public class CancelCorporateTransferRequestRequest
    {
        [Required(ErrorMessage = "CorporateTransferId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CorporateTransferId must be greater than 0")]
        public int CorporateTransferId { get; set; }

        [StringLength(256, ErrorMessage = "CancelReason cannot exceed 256 characters")]
        public string? CancelReason { get; set; }
    }

    public class ConfirmCorporateTransferRequestRequest
    {
        [Required(ErrorMessage = "CorporateTransferId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CorporateTransferId must be greater than 0")]
        public int CorporateTransferId { get; set; }
    }

    public class SaveIPDBillingRequest
    {
        [Required(ErrorMessage = "Visit details are required")]
        public IPDBillingVisitDetailsRequest VisitDetails { get; set; }

        [Required(ErrorMessage = "Billing items are required")]
        [MinLength(1, ErrorMessage = "At least one billing item is required")]
        public List<IPDBillingItemRequest> BillingItems { get; set; }

        public List<PaymentDetailRequest> PaymentDetails { get; set; } = new();

        /// <summary>1 = apply a single bill-level discount across all items</summary>
        public int IsBillDiscount { get; set; } = 0;
    }

    public class IPDBillingVisitDetailsRequest
    {
        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        public int CorporateId { get; set; }

        public decimal GrossBillAmount { get; set; }
        public decimal TotalDiscPerOnBill { get; set; }
        public decimal TotalDiscAmtOnBill { get; set; }
        public decimal RoundOff { get; set; }
        public decimal NetAmount { get; set; }

        public int DiscApprovedById { get; set; }
        public string DiscountReason { get; set; }
        public string Remarks { get; set; }
        public string UniqueId { get; set; }
        public int IsSupplementaryBill { get; set; }

    }

    public class IPDBillingItemRequest
    {
        [Required(ErrorMessage = "ServiceItemId is required")]
        public int ServiceItemId { get; set; }

        public int SubSubCategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int CategoryId { get; set; }

        /// <summary>CategoryTypeId: 1=Consultation, 3=Investigation</summary>
        public int CategoryTypeId { get; set; }

        /// <summary>LabTypeId (mapped from SubCategoryId): 1=Pathology, 2=Radiology, 3=Cardiology</summary>
        public int LabTypeId { get; set; }

        [Required(ErrorMessage = "ServiceName is required")]
        public string ServiceName { get; set; }

        public string Code { get; set; }
        public string? Remarks { get; set; }
        public string CorporateAlias { get; set; }
        public string CorporateCode { get; set; }
        public string DiscountReason { get; set; }

        public int IsNonPayable { get; set; }
        public int RateListId { get; set; }
        public int DoctorId { get; set; }
        public int PerformingDoctorId { get; set; }

        public decimal Qty { get; set; } = 1;
        public decimal Rate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal NetAmt { get; set; }
        public int IsUrgent { get; set; }

        /// <summary>SampleTypeId used to group barcodes per sample type for pathology</summary>
        public int SampleTypeId { get; set; }

        public string BillingDate { get; set; }
     
    }

    public class SaveIPDBillingResponse
    {
        public int VisitId { get; set; }
        public int FTID { get; set; }
        public int ReceiptId { get; set; }
        public bool IsReceipt { get; set; }
        public bool IsLabInvestigations { get; set; }
    }


    public class RemoveIPDServiceItemRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "FTDIdList is required")]
        [RegularExpression(@"^\d+(,\d+)*$", ErrorMessage = "FTDIdList must be a comma-separated list of integers (e.g. 1,2,3)")]
        public string FTDIdList { get; set; }

        [Required(ErrorMessage = "CancelReason is required")]
        [StringLength(512, ErrorMessage = "CancelReason cannot exceed 512 characters")]
        public string CancelReason { get; set; }
    }

    public class UpdateIPDServicePackageRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "FTDIdList is required")]
        [RegularExpression(@"^\d+(,\d+)*$", ErrorMessage = "FTDIdList must be a comma-separated list of integers (e.g. 1,2,3)")]
        public string FTDIdList { get; set; }

        /// <summary>0 = remove from package (re-derive Gross/Disc/Net from service rates), >0 = assign to package (zero out amounts)</summary>
        [Range(0, int.MaxValue, ErrorMessage = "PackageId must be greater than or equal to 0")]
        public int PackageId { get; set; } = 0;
    }

    public class UpdateIPDServiceCorporateNonPayableRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "FTDIdList is required")]
        [RegularExpression(@"^\d+(,\d+)*$", ErrorMessage = "FTDIdList must be a comma-separated list of integers (e.g. 1,2,3)")]
        public string FTDIdList { get; set; }

        [Required(ErrorMessage = "IsNonPayable is required")]
        [Range(0, 1, ErrorMessage = "IsNonPayable must be 0 or 1")]
        public int IsNonPayable { get; set; }
    }

    public class UpdateIPDServiceDiscAmtRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "FTDIdList is required")]
        [RegularExpression(@"^\d+(,\d+)*$", ErrorMessage = "FTDIdList must be a comma-separated list of integers (e.g. 1,2,3)")]
        public string FTDIdList { get; set; }

        [Required(ErrorMessage = "DiscAmt is required")]
        [Range(0, double.MaxValue, ErrorMessage = "DiscAmt must be greater than or equal to 0")]
        public decimal DiscAmt { get; set; }

        [StringLength(512, ErrorMessage = "DiscReason cannot exceed 512 characters")]
        public string DiscReason { get; set; }
    }

    public class UpdateIPDServiceDiscPerRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "FTDIdList is required")]
        [RegularExpression(@"^\d+(,\d+)*$", ErrorMessage = "FTDIdList must be a comma-separated list of integers (e.g. 1,2,3)")]
        public string FTDIdList { get; set; }

        [Required(ErrorMessage = "DiscPer is required")]
        [Range(0, 100, ErrorMessage = "DiscPer must be between 0 and 100")]
        public decimal DiscPer { get; set; }

        [StringLength(512, ErrorMessage = "DiscReason cannot exceed 512 characters")]
        public string DiscReason { get; set; }
    }

    public class UpdateIPDServiceRateRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "FTDIdList is required")]
        [RegularExpression(@"^\d+(,\d+)*$", ErrorMessage = "FTDIdList must be a comma-separated list of integers (e.g. 1,2,3)")]
        public string FTDIdList { get; set; }

        [Required(ErrorMessage = "Rate is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Rate must be greater than or equal to 0")]
        public decimal Rate { get; set; }
    }

    public class UpdateIPDServiceQtyRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "FTDIdList is required")]
        [RegularExpression(@"^\d+(,\d+)*$", ErrorMessage = "FTDIdList must be a comma-separated list of integers (e.g. 1,2,3)")]
        public string FTDIdList { get; set; }

        [Required(ErrorMessage = "Qty is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Qty must be greater than 0")]
        public decimal Qty { get; set; }
    }

    public class SaveIPDPatientAdvanceRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RoleId must be greater than 0")]
        public int RoleId { get; set; }

        /// <summary>P = Payment (advance collection), R = Refund of advance</summary>
        [Required(ErrorMessage = "Type is required")]
        [RegularExpression("^(P|R)$", ErrorMessage = "Type must be 'P' (Payment) or 'R' (Refund)")]
        public string Type { get; set; }

        [Required(ErrorMessage = "PatientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than 0")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "UHID is required")]
        public string Uhid { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        public string UniqueId { get; set; }

        [StringLength(512, ErrorMessage = "Remarks cannot exceed 512 characters")]
        public string Remarks { get; set; }

        [StringLength(256, ErrorMessage = "GuardianName cannot exceed 256 characters")]
        public string GuardianName { get; set; }

        [Required(ErrorMessage = "PaymentDetails is required")]
        [MinLength(1, ErrorMessage = "At least one payment detail is required")]
        public List<PaymentDetailRequest> PaymentDetails { get; set; } = new();
    }

    public class SaveIPDPatientAdvanceResponse
    {
        public int PatientId { get; set; }
        public int VisitId { get; set; }
        public int ReceiptId { get; set; }
    }

    public class InitializePatientDischargeProcessRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "CorporateId must be greater than or equal to 0")]
        public int CorporateId { get; set; } = 0;
    }

    public class StartPatientDischargeProcessRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "DischargeProcessId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DischargeProcessId must be greater than 0")]
        public int DischargeProcessId { get; set; }
    }

    public class CompletePatientDischargeProcessRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "DischargeProcessId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DischargeProcessId must be greater than 0")]
        public int DischargeProcessId { get; set; }

        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters")]
        public string? Remarks { get; set; }
    }



    public class CreateSupplementaryBillFromMainBillRequest
    {
        [Required(ErrorMessage = "Visit details are required")]
        public SupplementaryBillFromMainBillVisitDetailsRequest VisitDetails { get; set; }
        public List<SupplementaryBillFromMainBillItemRequest> BillingItems { get; set; }


        public List<PaymentDetailRequest> PaymentDetails { get; set; } = new();

        /// <summary>1 = apply a single bill-level discount across all items</summary>
        public int IsBillDiscount { get; set; } = 0;
    }

    public class SupplementaryBillFromMainBillVisitDetailsRequest
    {
        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        public decimal GrossBillAmount { get; set; }
        public decimal TotalDiscPerOnBill { get; set; }
        public decimal TotalDiscAmtOnBill { get; set; }
        public decimal RoundOff { get; set; }
        public decimal NetAmount { get; set; }
        public int DiscApprovedById { get; set; }
        public string DiscountReason { get; set; }
        public string Remarks { get; set; }
        public string UniqueId { get; set; }

    }

    public class SupplementaryBillFromMainBillItemRequest
    {

        [Required(ErrorMessage = "ServiceItemId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ServiceItemId must be greater than 0")]

        public int ServiceItemId { get; set; }

        [Required(ErrorMessage = "FTId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "FTId must be greater than 0")]

        public int FTId { get; set; }

        [Required(ErrorMessage = "FTDId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "FTDId must be greater than 0")]

        public int FTDId { get; set; }

    }


    public class CreateSupplementaryBillFromMainBillResponse
    {
        public int VisitId { get; set; }
        public int FTID { get; set; }
        public int ReceiptId { get; set; }
        public bool IsReceipt { get; set; }
    }

    public class SaveIPDDischargeRequest
    {
        [Required(ErrorMessage = "DischargeDate is required")]
        public string DischargeDate { get; set; }

        [Required(ErrorMessage = "DischargeTime is required")]
        public string DischargeTime { get; set; }

        [Required(ErrorMessage = "DischargeType is required")]
        public string DischargeType { get; set; }   // matches existing SP param name/type

        [Required(ErrorMessage = "BedId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BedId must be greater than 0")]
        public int BedId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

       
        // Only ONE of these should be non-null, matching DischargeType
        public NormalDischargeDetailsRequest? NormalDischargeDetails { get; set; }
        public LAMADischargeDetailsRequest? LAMADischargeDetails { get; set; }
        public TransferDischargeDetailsRequest? TransferDischargeDetails { get; set; }
        public DeathDischargeDetailsRequest? DeathDischargeDetails { get; set; }
        public AbscondedDischargeDetailsRequest? AbscondedDischargeDetails { get; set; }
    }

    public class NormalDischargeDetailsRequest
    {
        public string? ConditionAtDischarge { get; set; }
        public string? DischargeAdvice { get; set; }
        public string? FollowUpDate { get; set; }
        public int? FollowUpDepartmentId { get; set; }
        public int? FollowUpDoctorId { get; set; }
    }

    public class LAMADischargeDetailsRequest
    {
        public string? Reason { get; set; }
        public bool? IsRiskExplained { get; set; }
        public string? DeclarationText { get; set; }
        public int? CounsellingByDoctorId { get; set; }
        public string? RelativeName { get; set; }
        public string? Relationship { get; set; }
        public string? SignatureFilePath { get; set; }

        public bool? IsOtpVerified { get; set; }
        public string? OtpVerifiedOn { get; set; }
    }

    public class TransferDischargeDetailsRequest
    {
        public string? TransferHospitalName { get; set; }
        public string? TransferReason { get; set; }
        public string? ConditionAtTransfer { get; set; }
        public bool? IsAmbulanceRequired { get; set; }
        public int? AccompanyingStaffUserId { get; set; }
        public string? ReferralLetterFilePath { get; set; }

    }

    public class DeathDischargeDetailsRequest
    {
        public string? DateOfDeath { get; set; }
        public string? TimeOfDeath { get; set; }
        public string? CauseOfDeath { get; set; }
        public string? DeathSummary { get; set; }
        public string? CertificateStatus { get; set; }
        public string? BodyHandoverDetails { get; set; }
        public string? RelativeName { get; set; }
        public string? Relationship { get; set; }
        public string? ContactNumber { get; set; }
    }

    public class AbscondedDischargeDetailsRequest
    {
        public string? LastSeenDate { get; set; }
        public string? LastSeenTime { get; set; }
        public string? Circumstances { get; set; }
        public bool? IsStaffInformed { get; set; }
        public bool? IsPoliceInformed { get; set; }
        public string? Remarks { get; set; }
        public string? FIRNo { get; set; }
    }

}