namespace HISWEBAPI.Models
{
    public class SampleTypeMasterModel
    {
        public int SampleTypeId { get; set; }
        public string SampleType { get; set; }
        public int ContainerColorId { get; set; }
        public string ColorName { get; set; }
        public string ColorCode { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
        public int IsActive { get; set; }
    }

    public class SampleContainerColorMasterModel
    {
        public int ColorId { get; set; }
        public string ColorName { get; set; }
        public string ColorCode { get; set; }
    }

    public class LabMethodMasterModel
    {
        public int MethodId { get; set; }
        public string Method { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
        public int IsActive { get; set; }
    }
    public class SampleRemarksMasterModel
    {
        public int SampleRemarksID { get; set; }
        public string SampleRemarks { get; set; } = string.Empty;
        public int IsActive { get; set; }
    }


    public class SampleRejectionRemarksMasterModel
    {
        public int SampleRejectionRemarksID { get; set; }
        public string SampleRejectionRemarks { get; set; } = string.Empty;
        public int IsActive { get; set; }
    }

    public class FieldBoyMasterModel
    {
        public int FieldBoyId { get; set; }
        public string FieldBoyName { get; set; } = string.Empty;
        public int IsActive { get; set; }
    }

    public class ServiceItemMasterModel
    {
        public int ServiceItemId { get; set; }
        public int HospId { get; set; }
        public int CategoryTypeId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }

        public int SubCategoryId { get; set; }
        public string SubCategoryName { get; set; }

        public int SubSubCategoryId { get; set; }
        public string SubSubCategoryName { get; set; }

        public string Name { get; set; }
        public string Code { get; set; }
        public int? ReportTypeId { get; set; }
        public int? LabTypeId { get; set; }
        public string ReportType { get; set; }
        public int? IsSampleRequired { get; set; }
        public int? SampleTypeId { get; set; }
        public string SampleTypeIdList { get; set; }
        public int? LabMethodId { get; set; }
        public int? ForGenderId { get; set; }
        public string ForGender { get; set; }
        public int IsOutSource { get; set; }
        public int? IsPrintAlone { get; set; }
        public int? IsSentToDepartmentRequired { get; set; }
        public int? IsDepartmentReceivingRequired { get; set; }
        public string ShortName { get; set; }
        public string SampleVolume { get; set; }
        public string InvestigationComment { get; set; }
        public int TatInMin { get; set; }
        public int IsActive { get; set; }
        public decimal GSTPer { get; set; }
        public int? RoomTypeId { get; set; }
        public string RoomType { get; set; }
        public int? IsICU { get; set; }
        public string? SNOMEDCode { get; set; }
        public string? DoctorDepartmentIds { get; set; }
        public int? IsRequiredSeparatePerformingDoctor { get; set; }
        public int? OPDConsultationTypeId { get; set; }
        public string? OPDConsultationType { get; set; }
        public int? IsOnlineConsultationAllow { get; set; }
        public int? IsTeleConsultationService { get; set; }
        public int? IsRegistrationCharge { get; set; }
        public int? RegistrationChargeValidityDays { get; set; }
        public int? PackageDurationDays { get; set; }
        public string? StartsFrom { get; set; }
        public string? ExpiresOn { get; set; }
        public int? IsPackageExpired { get; set; }
        public string SaltName { get; set; }



    }

    public class ServiceItemMasterResponse
    {
        public int ServiceItemId { get; set; }
    }

    public class ObservationMasterModel
    {
        public int ObservationId { get; set; }
        public string ObservationName { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public string Method { get; set; }
        public int MethodId { get; set; }
        public int ShowInDischargeSummary { get; set; }
        public string RoundUp { get; set; }
        public int FieldTypeId { get; set; }
        public int IsActive { get; set; }
    }

    public class CreateUpdateObservationMasterResponse
    {
        public int ObservationId { get; set; }
    }

    public class InvastigationObservationMappingModel
    {
        public int MappingId { get; set; }
        public int InvastigationId { get; set; }
        public int ObservationId { get; set; }
        public string ObservationName { get; set; }
        public string Method { get; set; }
        public int MethodId { get; set; }
        public bool IsHeader { get; set; }
        public bool IsBold { get; set; }
        public bool IsUnderLine { get; set; }
        public int IsMandatory { get; set; }
        public string RoundUp { get; set; }
    }

    public class InvastigationObservationRangeMasterModel
    {
        public int Id { get; set; }
        public int ObservationId { get; set; }
        public string ObservationName { get; set; }
        public string Gender { get; set; }
        public string FromAge { get; set; }
        public string ToAge { get; set; }
        public int IsActive { get; set; }
        public string DefaultValue { get; set; }
        public string MinValue { get; set; }
        public string MaxValue { get; set; }
        public string Unit { get; set; }
        public string DisplayValue { get; set; }
    }

    public class SubmitInvastigationObservationRangeMasterResponse
    {
        public int ObservationId { get; set; }
        public string Gender { get; set; }
        public int InsertedCount { get; set; }
    }
    public class PatientInvestigationRemarkModel
    {
        public int Id { get; set; }
        public int PatientInvestigationId { get; set; }
        public string TestRemark { get; set; }
        public string TestComment { get; set; }
        public int TestCommentId { get; set; }
        public int IsInternal { get; set; }
        public string CreatedOn { get; set; }
        public string CreatedBy { get; set; }
    }

    public class InvestigationDocumentNameMasterModel
    {
        public int DocumentId { get; set; }
        public string Name { get; set; }
        public int IsActive { get; set; }
        public int? CreatedBy { get; set; }
    }

    public class InvastigationTemplateCommentMasterModel
    {
        public int Id { get; set; }
        public int TypeId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string ContentValue { get; set; }
        public int IsActive { get; set; }
        public int UserId { get; set; }
        public int IPAddress { get; set; }
    }

    public class InvestigationTemplateInterpretationMappingModelRaw
    {
        public int typeId { get; set; }
        public string type { get; set; }
        public int investigationId { get; set; }
        public int itemid { get; set; }
    }

    public class ObservationCommentLOVsMappingModel
    {
        public int typeId { get; set; }
        public string type { get; set; }
        public int observationId { get; set; }
        public int itemid { get; set; }
    }

    public class HistoTemplateMasterModel
    {
        public int Id { get; set; }
        public int TypeId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string ContentValue { get; set; }
        public int IsActive { get; set; }
        public string IpAddress { get; set; }
    }

    public class SpecimenMasterModel
    {
        public int ID { get; set; }
        public string SpecimenName { get; set; }
        public int IsActive { get; set; }
    }

    public class SpecimenMappingMasterModel
    {
        public int SpecimenNameId { get; set; }
        public string GrossIdList { get; set; }
        public string MicroscopicIdList { get; set; }
        public string ImpressionIdList { get; set; }
        public int IsActive { get; set; }
    }

    public class HistoPendingReasonMasterModel
    {
        public int ID { get; set; }
        public string PendingReason { get; set; }
        public int IsActive { get; set; }
    }

    public class HistoImmunoAntibioticMasterModel
    {
        public int ID { get; set; }
        public string AntibioticName { get; set; }
        public int IsActive { get; set; }
    }


    // ─── Organism Group ───────────────────────────────────────────────────────

    public class OrganismGroupModel
    {
        public int OrganismGroupId { get; set; }
        public string OrganismGroupName { get; set; }
        public int IsActive { get; set; }
    }

    // ─── Organism Name ────────────────────────────────────────────────────────

    public class OrganismNameModel
    {
        public int OrganismNameId { get; set; }
        public string OrganismName { get; set; }
        public int OrganismGroupId { get; set; }
        public string OrganismGroup { get; set; }
        public int IsActive { get; set; }
    }

    // ─── Antibiotic Group ─────────────────────────────────────────────────────

    public class AntibioticGroupModel
    {
        public int AntibioticGroupId { get; set; }
        public string AntibioticGroupName { get; set; }
        public int IsActive { get; set; }
    }

    // ─── Antibiotic Name ──────────────────────────────────────────────────────

    public class AntibioticNameModel
    {
        public int AntibioticNameId { get; set; }
        public string AntibioticName { get; set; }
        public int AntibioticGroupId { get; set; }
        public string AntibioticGroup { get; set; }
        public int IsActive { get; set; }
    }

    // ─── Micro Template ───────────────────────────────────────────────────────

    public class MicroTemplateMasterModel
    {
        public int Id { get; set; }
        public int TypeId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string ContentValue { get; set; }
        public int IsActive { get; set; }
        public string IpAddress { get; set; }
    }

    // ─── Micro Mapping ────────────────────────────────────────────────────────

    public class MicroMappingModel
    {
        public string AntibioticIdList { get; set; }
        public int OrganismId { get; set; }
        public string OrganismName { get; set; }
        public string AntibioticName { get; set; }
        public int AntibioticNameId { get; set; }
        public string AntibioticClassName { get; set; }
        public int AntibioticClassId { get; set; }
        public string BreakPoint { get; set; }
        public string SDD { get; set; }
        public string RefRangeI { get; set; }
        public string RefRangeS { get; set; }
        public string RefRangeR { get; set; }
        public string Resistant { get; set; }
    }
}

namespace HISWEBAPI.Models
{
    // ================= Lab TAT Monitor (2026-11-26) =================
    public class LabTATSettingModel
    {
        public string TatStartPoint { get; set; } = "received";   // order | collected | received
        public string TatEndPoint { get; set; } = "verified";     // entered | verified | dispatched
        public int AtRiskPercent { get; set; } = 25;
        public int OnTimeTargetPercent { get; set; } = 90;
    }

    public class LabTATPriorityModel
    {
        public int PriorityId { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public string ClockType { get; set; } = "WORKING";        // WORKING | 24X7
        public string ColorCode { get; set; } = string.Empty;
        public int? UrgentFlag { get; set; }
        public int SequenceNo { get; set; }
    }

    public class LabTATShiftModel
    {
        public int ShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public string FromTime { get; set; } = string.Empty;
        public string ToTime { get; set; } = string.Empty;
        public int SequenceNo { get; set; }
    }

    public class LabTATRuleModel
    {
        public int RuleId { get; set; }                            // < 0 = Investigation Master TAT (read only)
        public string RuleLevel { get; set; } = "ALL";             // TEST | DEPT | ALL
        public int InvestigationId { get; set; }
        public int DepartmentId { get; set; }
        public int PriorityId { get; set; }                        // 0 = any
        public int ShiftId { get; set; }                           // 0 = any
        public int TatMinutes { get; set; }
        public string ClockType { get; set; } = "INHERIT";         // INHERIT | WORKING | 24X7
        public string Note { get; set; } = string.Empty;
        public bool IsMasterRule { get; set; }
        public bool IsDefault { get; set; }
        public string LevelText { get; set; } = string.Empty;
        public string AppliesTo { get; set; } = string.Empty;
        public string PriorityName { get; set; } = "Any";
        public string ShiftName { get; set; } = "Any";
        public int SampleCount { get; set; }
    }

    public class LabTATWorkingHoursModel
    {
        public int DepartmentId { get; set; }
        public int DayOfWeekNo { get; set; }
        public int IsOpen { get; set; }
        public string FromTime { get; set; } = string.Empty;
        public string ToTime { get; set; } = string.Empty;
    }

    public class LabTATHolidayModel
    {
        public int HolidayId { get; set; }
        public string HolidayDate { get; set; } = string.Empty;    // yyyy-MM-dd
        public string HolidayName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = "All";
    }

    public class LabTATDelayReasonModel
    {
        public int DelayReasonId { get; set; }
        public string ReasonName { get; set; } = string.Empty;
    }

    public class LabTATDepartmentModel
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
    }

    public class LabTATTestModel
    {
        public int InvestigationId { get; set; }
        public string InvestigationName { get; set; } = string.Empty;
        public string InvestigationCode { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int MasterTatMinutes { get; set; }
    }

    public class LabTATTestMatrixCellModel
    {
        public int PriorityId { get; set; }
        public int? TatMinutes { get; set; }
        public string RuleLevel { get; set; } = string.Empty;
    }

    public class LabTATTestMatrixModel
    {
        public int InvestigationId { get; set; }
        public string InvestigationCode { get; set; } = string.Empty;
        public string InvestigationName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public List<LabTATTestMatrixCellModel> Cells { get; set; } = new();
    }

    public class LabTATConfigModel
    {
        public LabTATSettingModel Setting { get; set; } = new();
        public List<LabTATPriorityModel> Priorities { get; set; } = new();
        public List<LabTATShiftModel> Shifts { get; set; } = new();
        public List<LabTATRuleModel> Rules { get; set; } = new();
        public List<LabTATWorkingHoursModel> WorkingHours { get; set; } = new();
        public List<LabTATHolidayModel> Holidays { get; set; } = new();
        public List<LabTATDelayReasonModel> DelayReasons { get; set; } = new();
        public List<LabTATDepartmentModel> Departments { get; set; } = new();
        public List<LabTATTestModel> Tests { get; set; } = new();
        public List<LabTATTestMatrixModel> TestMatrix { get; set; } = new();
        public string BasisText { get; set; } = string.Empty;
    }

    public class LabTATStatModel
    {
        public int Samples { get; set; }
        public int Reported { get; set; }
        public int OnTime { get; set; }
        public int Late { get; set; }
        public double? OnTimePercent { get; set; }
        public double? AvgTargetMinutes { get; set; }
        public double? AvgTatMinutes { get; set; }
        public double? MedianTatMinutes { get; set; }
        public double? P90TatMinutes { get; set; }
        public int Pending { get; set; }
        public int Breached { get; set; }
        public int AtRisk { get; set; }
    }

    public class LabTATGroupStatModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public LabTATStatModel Stats { get; set; } = new();
    }

    public class LabTATMatrixCellModel
    {
        public string RowKey { get; set; } = string.Empty;
        public string ColKey { get; set; } = string.Empty;
        public LabTATStatModel Stats { get; set; } = new();
    }

    public class LabTATMatrixModel
    {
        public List<LabTATGroupStatModel> Rows { get; set; } = new();   // Stats = row total ("All" column)
        public List<LabTATGroupStatModel> Cols { get; set; } = new();
        public List<LabTATMatrixCellModel> Cells { get; set; } = new();
    }

    public class LabTATLateReasonModel
    {
        public string Reason { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percent { get; set; }
        public double CumPercent { get; set; }
    }

    public class LabTATTopLateTestModel
    {
        public string InvestigationName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int Reported { get; set; }
        public int Late { get; set; }
        public double LatePercent { get; set; }
    }

    public class LabTATDashboardModel
    {
        public int TargetPercent { get; set; }
        public int AtRiskPercent { get; set; }
        public string BasisText { get; set; } = string.Empty;
        public string PeriodText { get; set; } = string.Empty;
        public string PrintedOn { get; set; } = string.Empty;
        public LabTATStatModel Summary { get; set; } = new();
        public List<LabTATGroupStatModel> ByPriority { get; set; } = new();
        public List<LabTATGroupStatModel> ByShift { get; set; } = new();
        public List<LabTATGroupStatModel> ByDepartment { get; set; } = new();
        public List<LabTATGroupStatModel> ByTest { get; set; } = new();
        public List<LabTATGroupStatModel> ByDay { get; set; } = new();
        public LabTATMatrixModel DeptPriority { get; set; } = new();
        public LabTATMatrixModel ShiftPriority { get; set; } = new();
        public LabTATMatrixModel DeptShift { get; set; } = new();
        public List<LabTATLateReasonModel> LateReasons { get; set; } = new();
        public List<LabTATTopLateTestModel> TopLateTests { get; set; } = new();
    }

    public class LabTATSampleModel
    {
        public int PatientInvestigationId { get; set; }
        public int LabNo { get; set; }
        public string BarCode { get; set; } = string.Empty;
        public string UHID { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public int InvestigationId { get; set; }
        public string InvestigationName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int PriorityId { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public string ShiftName { get; set; } = string.Empty;
        public string StartOn { get; set; } = string.Empty;
        public string DueOn { get; set; } = string.Empty;
        public string EndOn { get; set; } = string.Empty;
        public double TargetMinutes { get; set; }
        public double? TatMinutes { get; set; }
        public double DelayMinutes { get; set; }                    // positive = late / overdue, negative = time left
        public double UsedPercent { get; set; }
        public string Status { get; set; } = string.Empty;          // On time | Late | On track | At risk | Breached
        public string RuleText { get; set; } = string.Empty;
        public string ClockType { get; set; } = string.Empty;
        public int DelayReasonId { get; set; }
        public string DelayReason { get; set; } = string.Empty;
    }

    public class LabTATWorklistModel
    {
        public string Now { get; set; } = string.Empty;
        public int Pending { get; set; }
        public int Breached { get; set; }
        public int AtRisk { get; set; }
        public int AtRiskPercent { get; set; }
        public List<LabTATSampleModel> Rows { get; set; } = new();
    }

    public class LabTATSimulationStepModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class LabTATSimulationCandidateModel
    {
        public string Text { get; set; } = string.Empty;
        public bool IsChosen { get; set; }
    }

    public class LabTATSimulationModel
    {
        public string DueOn { get; set; } = string.Empty;
        public double SpanMinutes { get; set; }
        public int? RuleMinutes { get; set; }
        public List<LabTATSimulationStepModel> Steps { get; set; } = new();
        public List<LabTATSimulationCandidateModel> Candidates { get; set; } = new();
        public List<string> Trace { get; set; } = new();
    }

    // ================= Lab Approval Authority Master (2026-11-27) =================
    public class LabApprovalAuthorityLookupsModel
    {
        public List<LabApprovalLabTypeModel> LabTypes { get; set; } = new();
        public List<LabApprovalSubDepartmentModel> SubDepartments { get; set; } = new();
        public List<LabApprovalDoctorModel> Doctors { get; set; } = new();
        public List<LabApprovalUserModel> ProvisionalUsers { get; set; } = new();
    }

    public class LabApprovalLabTypeModel
    {
        public int LabTypeId { get; set; }
        public string LabTypeName { get; set; } = "";
    }

    public class LabApprovalSubDepartmentModel
    {
        public int SubSubCategoryId { get; set; }
        public string SubSubCategoryName { get; set; } = "";
        public int LabTypeId { get; set; }
    }

    public class LabApprovalDoctorModel
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
    }

    public class LabApprovalUserModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
    }

    public class LabApprovalAuthorityModel
    {
        public int LabApprovalAuthorityId { get; set; }
        public int BranchId { get; set; }
        public string? BranchName { get; set; }
        public int LabTypeId { get; set; }
        public string? LabTypeName { get; set; }
        public string? SubSubCategoryIds { get; set; }
        public string? SubDepartmentNames { get; set; }
        public int ApprovalFlowId { get; set; }
        public string? ApprovalFlow { get; set; }
        public int IsAllApprovalRequired { get; set; }
        public int ApprovalLevelId { get; set; }
        public string? ApprovalLevel { get; set; }
        public string? Level1DoctorIds { get; set; }
        public string? Level2DoctorIds { get; set; }
        public string? Level3DoctorIds { get; set; }
        public string? Level4DoctorIds { get; set; }
        public string? Level1DoctorNames { get; set; }
        public string? Level2DoctorNames { get; set; }
        public string? Level3DoctorNames { get; set; }
        public string? Level4DoctorNames { get; set; }
        public string? ProvisionalUserIds { get; set; }
        public string? ProvisionalUserNames { get; set; }
        public int IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
    }

    // ================= Lab Formula + Result Validation setup (2026-11-27) =================
    public class LabInvestigationFormulaModel
    {
        public int FormulaId { get; set; }
        public int InvestigationId { get; set; }
        public int ObservationId { get; set; }
        public string ObservationName { get; set; } = "";
        public string Gender { get; set; } = "B";
        public string Expression { get; set; } = "";
        public string FormulaText { get; set; } = "";
        public int DecimalPlaces { get; set; }
        public int IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
    }

    public class LabValidationObservationModel
    {
        public int ObservationId { get; set; }
        public string ObservationName { get; set; } = "";
        public int SequenceNo { get; set; }
    }

    public class LabInvestigationValidationModel
    {
        public int ValidationId { get; set; }
        public int InvestigationId { get; set; }
        public string ValidationName { get; set; } = "";
        public string Gender { get; set; } = "B";
        public string CheckType { get; set; } = "";
        public string Expression { get; set; } = "";
        public string ExpressionText { get; set; } = "";
        public string Operator { get; set; } = "";
        public decimal Value1 { get; set; }
        public decimal? Value2 { get; set; }
        public decimal Tolerance { get; set; }
        public int ActionType { get; set; }
        public int SkipIfBlank { get; set; }
        public string ErrorMessage { get; set; } = "";
        public int IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
        public List<LabValidationObservationModel> Observations { get; set; } = new();
    }

    public class LabFormulaSetupModel
    {
        public List<LabInvestigationFormulaModel> Formulas { get; set; } = new();
        public List<LabInvestigationValidationModel> Validations { get; set; } = new();
    }

    /// <summary>Rules for one patient test; formulas are already chosen for the patient's gender (else Both).</summary>
    public class ResultEntryFormulaSetupModel
    {
        public int InvestigationId { get; set; }
        public string Gender { get; set; } = "B";
        public int? AgeYears { get; set; }
        public int? AgeMonths { get; set; }
        public int? AgeDays { get; set; }
        public List<LabInvestigationFormulaModel> Formulas { get; set; } = new();
        public List<LabInvestigationValidationModel> Validations { get; set; } = new();
    }
}
