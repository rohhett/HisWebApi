using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.DTO
{
    public class CreateUpdatePatientMasterRequest
    {
        public int PatientId { get; set; } = 0;

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "Title is required")]
        [StringLength(20, ErrorMessage = "Title cannot exceed 20 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "FirstName is required")]
        [StringLength(100, ErrorMessage = "FirstName cannot exceed 100 characters")]
        public string FirstName { get; set; }

        [StringLength(100)]
        public string? MiddleName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "AgeYears is required")]
        public int AgeYears { get; set; }

        [Required(ErrorMessage = "AgeMonths is required")]
        public int AgeMonths { get; set; }

        [Required(ErrorMessage = "AgeDays is required")]
        public int AgeDays { get; set; }

        [Required(ErrorMessage = "DOB is required")]
        public string Dob { get; set; } // Format: dd-MM-yyyy

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female, or Other")]
        public string Gender { get; set; }

        [StringLength(50)]
        public string? MaritalStatus { get; set; }

        [StringLength(50)]
        public string? Relation { get; set; }

        [StringLength(256)]
        public string? RelativeName { get; set; }

        [StringLength(100)]
        public string? IdProofName { get; set; }

        [StringLength(100)]
        public string? IdProofNumber { get; set; }

        [Required(ErrorMessage = "ContactNumber is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Contact must be exactly 10 digits")]
        public string SelfContactNumber { get; set; }

        [StringLength(20)]
        public string? EmergencyContactNumber { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? PrivilegedCardNumber { get; set; }

        [StringLength(1000)]
        public string? Address { get; set; }

        public int CountryId { get; set; }
        public string? Country { get; set; }
        public int StateId { get; set; }
        public string? State { get; set; }
        public int DistrictId { get; set; }
        public string? District { get; set; }
        public int CityId { get; set; }
        public string? City { get; set; }

        public int InsuranceCompanyId { get; set; } = 0;
        public int CorporateId { get; set; }

        [StringLength(100)]
        public string? CardNo { get; set; }

        // Optional patient image file
        public IFormFile? PatientImageFile { get; set; }

        public int IsVaccination { get; set; } = 0;
        public int? VipPatient { get; set; }
        public string? PolicyNo { get; set; }
        public string? PolicyCardNo { get; set; }
        public string? ExpiryDate { get; set; }
        public string? CardHolder { get; set; }
        public string? ReferalNo { get; set; }
        public string? ReferalDate { get; set; }
        public int OnlinePtId { get; set; } = 0;
        public string? HealthId { get; set; }
        public string? HealthIdNumber { get; set; }
        public string? LandlineNo { get; set; }
        public string? BirthPlace { get; set; }
        public string? Religion { get; set; }
        public string? RelationPhone { get; set; }
        public int? RelationAge { get; set; }
        public string? RelationGender { get; set; }
        public string? EMG_FirstName { get; set; }
        public string? EMG_LastName { get; set; }
        public string? EMG_Relation { get; set; }
        public string? EMG_MobileNo { get; set; }
        public string? EMG_ResidentNo { get; set; }
        public string? EMG_Address { get; set; }
        public int IsInternational { get; set; } = 0;
        public string? Locality { get; set; }
        public string? PassportNumber { get; set; }
        public string? InternationalNo { get; set; }
        public string? MembershipNo { get; set; }
        public string? PatientType { get; set; }
        public string? IdentityMark { get; set; }
        public string? IdentityMark2 { get; set; }
        public string? ReferenceType { get; set; }
        public string? Remarks { get; set; }

    }

    public class CreateUpdatePatientMasterResponse
    {
        public int PatientId { get; set; }
        public string? PatientImagePath { get; set; }
    }

    public class GetPatientMasterRequest
    {
        public int? PatientId { get; set; }
        public string? Uhid { get; set; }
        public string? ContactNumber { get; set; }
        public int? BranchId { get; set; }
    }

    // ─── OPD Billing Request ─────────────────────────────────────────────────────

    public class SaveOPDBillingRequest
    {
        [Required(ErrorMessage = "Visit details are required")]
        public PatientOPDVisitDetailsRequest VisitDetails { get; set; }

        [Required(ErrorMessage = "Billing items are required")]
        [MinLength(1, ErrorMessage = "At least one billing item is required")]
        public List<OPDBillingItemRequest> BillingItems { get; set; }

        public List<PaymentDetailRequest> PaymentDetails { get; set; } = new();

        /// <summary>1 = apply a single bill-level discount across all items</summary>
        public int IsBillDiscount { get; set; } = 0;
    }

    public class PatientOPDVisitDetailsRequest
    {
        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "UHID is required")]
        public string Uhid { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "CurrentAge is required")]
        public string CurrentAge { get; set; }

        public int InsuranceCompanyId { get; set; }
        public int CorporateId { get; set; }
        public int doctorId { get; set; } = 0;
        public int ReferDoctorId { get; set; }

        public decimal GrossBillAmount { get; set; }
        public decimal TotalDiscPerOnBill { get; set; }
        public decimal TotalDiscAmtOnBill { get; set; }
        public decimal RoundOff { get; set; }
        public decimal NetAmount { get; set; }

        public int DiscApprovedById { get; set; }
        public string DiscountReason { get; set; }
        public string Remarks { get; set; }
        public string UniqueId { get; set; }

        public string Mlc { get; set; }
        public string Pi { get; set; }
        public string Remark { get; set; }

        public string PolicyNo { get; set; }
        public string PolicyCardNo { get; set; }
        public string ExpiryDate { get; set; }
        public string CardHolder { get; set; }
        public string ReferalNo { get; set; }
        public string ReferalDate { get; set; }

        public int DiagnosisId { get; set; }
        public int ProId { get; set; }
        public string ProName { get; set; }
        public int IsSendMRD { get; set; }
    }

    public class OPDBillingItemRequest
    {
        [Required(ErrorMessage = "ServiceItemId is required")]
        public int ServiceItemId { get; set; }

        public int SubSubCategoryId { get; set; }

       
        public int SubCategoryId { get; set; }

  
        public int CategoryId { get; set; }

        /// <summary>CategoryId: 1=Consultation, 3=Investigation</summary>
        public int CategoryTypeId { get; set; }

        /// <summary>SubCategoryId: 1=Pathology, 2=Radiology, 3=Cardiology</summary>
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
        public int ValidityDays { get; set; }
        public int DoctorId { get; set; }
        public int PerformingDoctorId { get; set; }

        public decimal Qty { get; set; } = 1;
        public decimal Rate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal NetAmt { get; set; }

        public int IsUnderPackage { get; set; }
        public int PackageId { get; set; }

        public int IsUrgent { get; set; }

        /// <summary>SampleTypeId used to group barcodes per sample type for pathology</summary>
        public int SampleTypeId { get; set; }
    }

    public class PaymentDetailRequest
    {
        [Required(ErrorMessage = "PaymentModeId is required")]
        public int PaymentModeId { get; set; }

        /// <summary>PaymentModeTypeId 4 = Credit (excluded from receipt payment mode details)</summary>
        public int PaymentModeTypeId { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        public decimal Amount { get; set; }

        public int IsCopaymentReceipt { get; set; } = 0;
        public int IsPatientAdvanceAmount { get; set; } = 0;
        public int BankId { get; set; }
        public string RefNo { get; set; }
        public string PlutusTransactionReferenceID { get; set; }
        public string TransactionLogId { get; set; }
    }

    // ─── OPD Billing Response ────────────────────────────────────────────────────

    public class SaveOPDBillingResponse
    {
        public int VisitId { get; set; }
        public int FTID { get; set; }
        public int ReceiptId { get; set; }
        public bool IsReceipt { get; set; }
        public bool IsDoctorAppointment { get; set; }
        public bool IsLabInvestigations { get; set; }
    }

    public class UploadPatientDocumentRequest
    {
        [Required(ErrorMessage = "DocumentId is required")]
        public int DocumentId { get; set; }

        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Document file is required")]
        public IFormFile DocumentFile { get; set; }
    }

    public class PatientDocumentMappingResponse
    {
        public int DocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentCode { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public int IsMandatory { get; set; }

    }

    public class GetReceiptDetailsByFTIDRequest
    {
        [Required(ErrorMessage = "FTID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "FTID must be greater than 0")]
        public int FTID { get; set; }

        [Required(ErrorMessage = "isReceipt is required")]
        [RegularExpression("^(0|1)$", ErrorMessage = "isReceipt must be 0 or 1")]
        public string IsReceipt { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "ReceiptId must be greater than or equal to 0")]
        public int ReceiptId { get; set; }
    }

    public class GetOPDReceiptListRequest
    {
        [Required(ErrorMessage = "VisitNo is required")]
        [Range(1, long.MaxValue, ErrorMessage = "VisitNo must be greater than 0")]
        public long VisitNo { get; set; }
    }

    public class GetOPDCardDetailsRequest
    {
        [Required(ErrorMessage = "FTID is required")]
        [Range(1, long.MaxValue, ErrorMessage = "FTID must be greater than 0")]
        public long FTID { get; set; }
    }

 

    public class SearchPatientForConsultationRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        public string Uhid { get; set; }

        public int AppNo { get; set; } = 0;

        public int DoctorId { get; set; } = 0;

        public int DoctorDepartmentId { get; set; } = 0;

        /// <summary>1 = OPD, 2 = IPD</summary>
        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, 20, ErrorMessage = "TypeId must be 1 (OPD) or 2 (IPD) or 6 (DayCare) or 7 (Dialysis) or 9 (Emergency)")]
        public int TypeId { get; set; }

        /// <summary>IPD only – 0 = to get all IPD Admitted Patient,  1 = AdmissionDate, 2 = DischargeDate</summary>
        public int DateTypeId { get; set; } = 0;

        public int StatusId { get; set; } = 0;

        public int BedTypeId { get; set; } = 0;

        [Required(ErrorMessage = "FromDate is required")]
        public string FromDate { get; set; }

        [Required(ErrorMessage = "ToDate is required")]
        public string ToDate { get; set; }
        public int isTempratureRoomOut { get; set; } = 0;

    }


    public class SaveIPDAdmissionRequest
    {
        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "UHID is required")]
        public string Uhid { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "CurrentAge is required")]
        public string CurrentAge { get; set; }

        [Required(ErrorMessage = "PrimaryDoctorId is required")]
        public int PrimaryDoctorId { get; set; }

        public List<int> SecondaryDoctorIds { get; set; } = new();

        public int InsuranceCompanyId { get; set; }
        public int CorporateId { get; set; }
        public int ReferDoctorId { get; set; }
        public int ProId { get; set; }
        public string ProName { get; set; }

        public string AdmissionType { get; set; }  

        [Required(ErrorMessage = "BillingTypeId is required")]
        public int BillingTypeId { get; set; }

        [Required(ErrorMessage = "RoomTypeId is required")]
        public int RoomTypeId { get; set; }

        [Required(ErrorMessage = "BedId is required")]
        public int BedId { get; set; }

        [Required(ErrorMessage = "AdmissionDate is required")]
        public string AdmissionDate { get; set; }

        [Required(ErrorMessage = "AdmissionTime is required")]
        public string AdmissionTime { get; set; }


        // Attendant details
        public string AttendantRelation { get; set; }
        public string AttendantName { get; set; }
        public string AttendantContactNumber { get; set; }
        public int? HandleWithCare { get; set; }
        public int? NameMasking { get; set; }

        // MLC fields (only when AdmissionType == "MLC")
        public string MlcNo { get; set; }
        public int? MlcTypeId { get; set; }
        public string MlcType { get; set; }
        public int? InjuryTypeId { get; set; }
        public string InjuryType { get; set; }
        public string BroughtBy { get; set; }
        public int? TransportId { get; set; }
        public string Transport { get; set; }
        public string PlaceOfAccident { get; set; }
        public string PoliceStation { get; set; }
        public string OfficerName { get; set; }
        public string OfficerPhone { get; set; }
        public string ComplaintNo { get; set; }
        public string BuckleNoOfPolice { get; set; }
        public DateTime? DateOfInjury { get; set; }
        public DateTime? DateOfInitiation { get; set; }
        public string CauseOfAccident { get; set; }
        public string IdentificationMarks { get; set; }
        public string Remarks { get; set; }
    }

    public class SaveIPDAdmissionResponse
    {
        public int VisitId { get; set; }
    }

    public class SearchIPDPatientRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "SearchBy is required")]
        [StringLength(50)]
        public string SearchBy { get; set; }

        public string SearchValue { get; set; } = string.Empty;

        /// <summary>
        /// 0=All, 1=Admitted, 2=Discharged, 3=Bill Generated Pending,
        /// 4=File Closed Pending, 5=Today Admitted, 6=Today Discharged,
        /// 7=Zero Advance, 8=Cash, 9=Corporate, 10=Discharge Summary Ready
        /// </summary>
        public int StatusId { get; set; } = 0;
    }


    public class UploadVisitWisePatientDocumentRequest
    {
        [Required(ErrorMessage = "DocumentId is required")]
        public int DocumentId { get; set; }

        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "DocumentCategoryId is required")]
        public int DocumentCategoryId { get; set; }

        [Required(ErrorMessage = "Document file is required")]
        public IFormFile DocumentFile { get; set; }
    }


    public class SaveOPDBookingRequest
    {
        [Required(ErrorMessage = "Visit details are required")]
        public PatientOPDBookingRequest VisitDetails { get; set; }

        [Required(ErrorMessage = "Billing items are required")]
        [MinLength(1, ErrorMessage = "At least one billing item is required")]
        public List<OPDBookingItemRequest> BillingItems { get; set; }
    }

    public class PatientOPDBookingRequest
    {
        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }
        public int RoleId { get; set; } = 0;

        public int InsuranceCompanyId { get; set; } = 0;

        [Required(ErrorMessage = "CorporateId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CorporateId must be greater than 0")]
        public int CorporateId { get; set; }
        public int ReferDoctorId { get; set; }
        public int IsDiscountApprovalRequired { get; set; } = 0;

        public decimal GrossBillAmount { get; set; }
        public decimal TotalDiscPerOnBill { get; set; }
        public decimal TotalDiscAmtOnBill { get; set; }
        public decimal RoundOff { get; set; }
        public decimal NetAmount { get; set; }
        public int DiscountApprovedID { get; set; } = 0;
        public string? DiscountApprovedName { get; set; }
        public string? DiscountReason { get; set; }
        public string? Remark { get; set; }
        public string? PolicyNo { get; set; }
        public string? PolicyCardNo { get; set; }
        public string? ExpiryDate { get; set; }
        public string? CardHolder { get; set; }
        public string? ReferalNo { get; set; }
        public string? ReferalDate { get; set; }
    }

    public class OPDBookingItemRequest
    {
        [Required(ErrorMessage = "ServiceItemId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ServiceItemId must be greater than 0")]
        public int ServiceItemId { get; set; }

        [Required(ErrorMessage = "SubSubCategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SubSubCategoryId must be greater than 0")]
        public int SubSubCategoryId { get; set; }

        [Required(ErrorMessage = "ServiceName is required")]
        public string ServiceName { get; set; }

        public string? Code { get; set; }
        public string? Remarks { get; set; }

        public int RateListId { get; set; } = 0;

        [Required(ErrorMessage = "DoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than 0")]
        public int DoctorId { get; set; }
        public int PerformingDoctorId { get; set; } = 0;
        public decimal Qty { get; set; } = 1;
        public decimal Rate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal NetAmt { get; set; }

        public int IsUrgent { get; set; } = 0;
    }

    public class SaveOPDBookingResponse
    {
        public int BookingId { get; set; }
    }

    public class CancelOPDBookingRequest
    {
        [Required(ErrorMessage = "BookingId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BookingId must be greater than 0")]
        public int BookingId { get; set; }

        [StringLength(256, ErrorMessage = "CancelReason cannot exceed 256 characters")]
        public string? CancelReason { get; set; }
    }

    public class ApproveOPDBookingDiscountRequest
    {
        [Required(ErrorMessage = "BookingId is required")]
        [Range(1, long.MaxValue, ErrorMessage = "BookingId must be greater than 0")]
        public long BookingId { get; set; }

        [Required(ErrorMessage = "Flag is required")]
        [Range(0, 4, ErrorMessage = "Flag must be between 0 and 4")]
        public int Flag { get; set; }

        [Range(0, 100, ErrorMessage = "ApprovedPer must be between 0 and 100")]
        public decimal ApprovedPer { get; set; } = 0;

        [StringLength(256, ErrorMessage = "ApprovalRemarks cannot exceed 256 characters")]
        public string? ApprovalRemarks { get; set; }
    }

    // ─── Patient Advance (Ledger) ────────────────────────────────────────────────

    public class SavePatientAdvanceRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RoleId must be greater than 0")]
        public int RoleId { get; set; }


        [Required(ErrorMessage = "PatientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than 0")]
        public int PatientId { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "PatientLedgerId must be greater than or equal to 0")]
        public int PatientLedgerId { get; set; } = 0;
        public int IsRefund { get; set; } = 0;


        [Required(ErrorMessage = "PaymentDetails is required")]
        [MinLength(1, ErrorMessage = "At least one payment detail is required")]
        public List<PaymentDetailRequest> PaymentDetails { get; set; } = new();
    }

    public class SavePatientAdvanceResponse
    {
        public int PatientId { get; set; }
        public int LedgerId { get; set; }
        public int ReceiptId { get; set; }
    }

    public class SaveOPDRefundBillingRequest
    {
        [Required(ErrorMessage = "Visit details are required")]
        public PatientOPDVisitDetailsRequest VisitDetails { get; set; }

        [Required(ErrorMessage = "Refund items are required")]
        [MinLength(1, ErrorMessage = "At least one refund item is required")]
        public List<OPDRefundBillingItemRequest> RefundItems { get; set; }

        public List<PaymentDetailRequest> PaymentDetails { get; set; } = new();
    }

    public class OPDRefundBillingItemRequest
    {
        /// <summary>FTDId of the original FinancialTransactionDetails row being refunded</summary>
        [Required(ErrorMessage = "FTDId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "FTDId must be greater than 0")]
        public int FTDId { get; set; }

        [Required(ErrorMessage = "ServiceItemId is required")]
        public int ServiceItemId { get; set; }

        public int SubSubCategoryId { get; set; }

        /// <summary>SubCategoryId: 1=Pathology, 2=Radiology, 3=Cardiology</summary>
        public int SubCategoryId { get; set; }

        /// <summary>CategoryId: 1=Consultation, 3=Investigation</summary>
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "ServiceName is required")]
        public string ServiceName { get; set; }

        public string Code { get; set; }
        public string CorporateAlias { get; set; }
        public string CorporateCode { get; set; }

        public int IsNonPayable { get; set; }
        public int RateListId { get; set; }
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Qty is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Qty must be greater than 0")]
        public decimal Qty { get; set; }

        public decimal Rate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal NetAmt { get; set; }

        public int IsUnderPackage { get; set; }
    }

    public class SaveOPDRefundBillingResponse
    {
        public int VisitId { get; set; }
        public int FTID { get; set; }
        public int ReceiptId { get; set; }
    }




        public class SaveOPDRefundRequestApprovalRequest
        {
            [Required(ErrorMessage = "Visit details are required")]
            public OPDRefundVisitDetailsRequest VisitDetails { get; set; }

            [Required(ErrorMessage = "Billing items are required")]
            [MinLength(1, ErrorMessage = "At least one refund item is required")]
            public List<OPDRefundItemRequest> BillingItems { get; set; }
        }

        public class OPDRefundVisitDetailsRequest
        {
            [Required(ErrorMessage = "BranchId is required")]
            public int BranchId { get; set; }

            public int RoleId { get; set; } = 0;

            [Required(ErrorMessage = "PatientId is required")]
            public int PatientId { get; set; }

            [Required(ErrorMessage = "VisitId is required")]
            public int VisitId { get; set; }

            public decimal GrossBillAmount { get; set; }
            public decimal TotalDiscPerOnBill { get; set; }
            public decimal TotalDiscAmtOnBill { get; set; }
            public decimal RoundOff { get; set; }
            public decimal NetAmount { get; set; }

            public int RefundApprovedID { get; set; } = 0;
            public string? RefundApprovedName { get; set; }
            public string? RefundReason { get; set; }
            public string? RefundRemark { get; set; }
        }

        public class OPDRefundItemRequest
        {
            [Required(ErrorMessage = "FTDId is required")]
            [Range(1, int.MaxValue, ErrorMessage = "FTDId must be greater than 0")]
            public int FTDId { get; set; }

            [Required(ErrorMessage = "RefundQty is required")]
            public decimal RefundQty { get; set; }
        }

        public class SaveOPDRefundRequestApprovalResponse
        {
            public int RefundId { get; set; }
        }

        public class ApproveOPDRefundRequestRequest
        {
            [Required(ErrorMessage = "RefundId is required")]
            [Range(1, int.MaxValue, ErrorMessage = "RefundId must be greater than 0")]
            public int RefundId { get; set; }

            [Required(ErrorMessage = "Flag is required")]
            [Range(1, 4, ErrorMessage = "Flag must be between 1 and 4")]
            public int Flag { get; set; }


            [StringLength(256, ErrorMessage = "ApprovalRemarks cannot exceed 256 characters")]
            public string? ApprovalRemarks { get; set; }
        }

        public class CancelOPDRefundRequestRequest
        {
            [Required(ErrorMessage = "RefundId is required")]
            [Range(1, int.MaxValue, ErrorMessage = "RefundId must be greater than 0")]
            public int RefundId { get; set; }

            [StringLength(256, ErrorMessage = "CancelReason cannot exceed 256 characters")]
            public string? CancelReason { get; set; }
        }

        public class paymentOPDRefundRequestRequest
        {
            [Required(ErrorMessage = "RefundId is required")]
            [Range(1, int.MaxValue, ErrorMessage = "RefundId must be greater than 0")]
            public int RefundId { get; set; }
        }

    public class SaveCreditNoteRequestApprovalRequest
    {
        [Required(ErrorMessage = "Visit details are required")]
        public CreditNoteVisitDetailsRequest VisitDetails { get; set; }

        [Required(ErrorMessage = "Billing items are required")]
        [MinLength(1, ErrorMessage = "At least one credit note item is required")]
        public List<CreditNoteItemRequest> BillingItems { get; set; }
    }

    public class CreditNoteVisitDetailsRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "BillId is required")]
        public int BillId { get; set; }

        public decimal TotalCreditNoteAmount { get; set; }

        public int CreditNoteApprovedID { get; set; } = 0;
        public string? CreditNoteApprovedName { get; set; }
        public string? CreditNoteReason { get; set; }
        public string? CreditNoteRemark { get; set; }
    }

    public class CreditNoteItemRequest
    {
        [Required(ErrorMessage = "FTDId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "FTDId must be greater than 0")]
        public int FTDId { get; set; }

        public decimal CreditNotePer { get; set; } = 0;
        public decimal CreditNoteAmt { get; set; } = 0;
    }

    public class SaveCreditNoteRequestApprovalResponse
    {
        public int CreditNoteId { get; set; }
    }

    public class ApproveCreditNoteRequestRequest
    {
        [Required(ErrorMessage = "CreditNoteId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CreditNoteId must be greater than 0")]
        public int CreditNoteId { get; set; }

        [Required(ErrorMessage = "Flag is required")]
        [Range(1, 4, ErrorMessage = "Flag must be between 1 and 4")]
        public int Flag { get; set; }

        [StringLength(256, ErrorMessage = "ApprovalRemarks cannot exceed 256 characters")]
        public string? ApprovalRemarks { get; set; }
    }

    public class CancelCreditNoteRequestRequest
    {
        [Required(ErrorMessage = "CreditNoteId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CreditNoteId must be greater than 0")]
        public int CreditNoteId { get; set; }

        [StringLength(256, ErrorMessage = "CancelReason cannot exceed 256 characters")]
        public string? CancelReason { get; set; }
    }

    public class CollectCreditNoteRequestRequest
    {
        [Required(ErrorMessage = "CreditNoteId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CreditNoteId must be greater than 0")]
        public int CreditNoteId { get; set; }
    }

    public class SaveWriteOffRequestApprovalRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "BillId is required")]
        public int BillId { get; set; }

        public decimal TotalWriteOffAmount { get; set; }

        public int WriteOffApprovedID { get; set; } = 0;
        public string? WriteOffApprovedName { get; set; }
        public string? WriteOffReason { get; set; }
        public string? WriteOffRemark { get; set; }
    }

    public class SaveWriteOffRequestApprovalResponse
    {
        public int WriteOffId { get; set; }
    }

    public class ApproveWriteOffRequestRequest
    {
        [Required(ErrorMessage = "WriteOffId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "WriteOffId must be greater than 0")]
        public int WriteOffId { get; set; }

        [Required(ErrorMessage = "Flag is required")]
        [Range(1, 4, ErrorMessage = "Flag must be between 1 and 4")]
        public int Flag { get; set; }

        [StringLength(256, ErrorMessage = "ApprovalRemarks cannot exceed 256 characters")]
        public string? ApprovalRemarks { get; set; }
    }

    public class CancelWriteOffRequestRequest
    {
        [Required(ErrorMessage = "WriteOffId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "WriteOffId must be greater than 0")]
        public int WriteOffId { get; set; }

        [StringLength(256, ErrorMessage = "CancelReason cannot exceed 256 characters")]
        public string? CancelReason { get; set; }
    }

    public class CollectWriteOffRequestRequest
    {
        [Required(ErrorMessage = "WriteOffId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "WriteOffId must be greater than 0")]
        public int WriteOffId { get; set; }
    }

    public class SaveCreditNoteRequest
    {
        [Required(ErrorMessage = "Visit details are required")]
        public SaveCreditNoteVisitDetailsRequest VisitDetails { get; set; }

        [Required(ErrorMessage = "Billing items are required")]
        [MinLength(1, ErrorMessage = "At least one credit note item is required")]
        public List<SaveCreditNoteItemRequest> BillingItems { get; set; }
    }

    public class SaveCreditNoteVisitDetailsRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "BillId is required")]
        public int BillId { get; set; }

        public decimal TotalCreditNoteAmount { get; set; }

        public int CreditNoteApprovedID { get; set; } = 0;
        public string? CreditNoteApprovedName { get; set; }
        public string? CreditNoteReason { get; set; }
        public string? CreditNoteRemark { get; set; }
    }

    public class SaveCreditNoteItemRequest
    {
        [Required(ErrorMessage = "FTDId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "FTDId must be greater than 0")]
        public int FTDId { get; set; }

        public decimal CreditNotePer { get; set; } = 0;
        public decimal CreditNoteAmt { get; set; } = 0;
    }

    public class SaveCreditNoteResponse
    {
        public int CreditNoteId { get; set; }
    }

    public class SaveWriteOffRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }
        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "PatientId is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "BillId is required")]
        public int BillId { get; set; }
        public decimal TotalWriteOffAmount { get; set; }
        public int WriteOffApprovedID { get; set; } = 0;
        public string? WriteOffApprovedName { get; set; }
        public string? WriteOffReason { get; set; }
        public string? WriteOffRemark { get; set; }
    }

    public class SaveWriteOffResponse
    {
        public int WriteOffId { get; set; }
    }

    public class SaveOPDAppointmentRequest
    {
        [Required(ErrorMessage = "VisitDetails are required")]
        public OPDAppointmentVisitDetails VisitDetails { get; set; }

        [Required(ErrorMessage = "PatientDetails are required")]
        public OPDAppointmentPatientDetails PatientDetails { get; set; }

        public List<PaymentDetailRequest> PaymentDetails { get; set; } = new();
    }

    public class OPDAppointmentPatientDetails
    {
        public int PatientId { get; set; } = 0;
        public int BranchId { get; set; } = 0;
        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "Title is required")]
        [StringLength(20, ErrorMessage = "Title cannot exceed 20 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "FirstName is required")]
        [StringLength(100, ErrorMessage = "FirstName cannot exceed 100 characters")]
        public string FirstName { get; set; }

        [StringLength(100)]
        public string? MiddleName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "AgeYears is required")]
        public int AgeYears { get; set; }

        [Required(ErrorMessage = "AgeMonths is required")]
        public int AgeMonths { get; set; }

        [Required(ErrorMessage = "AgeDays is required")]
        public int AgeDays { get; set; }

        [Required(ErrorMessage = "DOB is required")]
        public string Dob { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female, or Other")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "SelfContactNumber is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Contact must be exactly 10 digits")]
        public string SelfContactNumber { get; set; }

        public string? Address { get; set; }
        public int CountryId { get; set; } = 0;
        public string? Country { get; set; }
        public int StateId { get; set; } = 0;
        public string? State { get; set; }
        public int DistrictId { get; set; } = 0;
        public string? District { get; set; }
        public int CityId { get; set; } = 0;
        public string? City { get; set; }
        public int InsuranceCompanyId { get; set; } = 0;
        public int CorporateId { get; set; } = 0;
    }

    public class OPDAppointmentVisitDetails
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "DoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than 0")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "AppDateTime is required")]
        public DateTime AppDateTime { get; set; }

        public int RoleId { get; set; } = 0;
        public int InsuranceCompanyId { get; set; } = 0;
        public int CorporateId { get; set; } = 0;
        public int? ServiceItemId { get; set; }
        public string? ServiceName { get; set; }
        public decimal Amount { get; set; } = 0;
        public int? SlotId { get; set; }
        public string? SourceType { get; set; }
    }

    public class SaveOPDAppointmentResponse
    {
        public int AppId { get; set; }
        public int PatientId { get; set; }
        public int ReceiptId { get; set; }
        public int LedgerId { get; set; }
    }
}