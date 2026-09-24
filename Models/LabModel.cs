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
    public class LabFormulaMasterModel
    {
        public string FormulaText { get; set; }
        public int TypeId { get; set; }
        public string Type { get; set; }
        public string Component { get; set; }
        public int SequenceNo { get; set; }
        public string FormulaExpressionRight { get; set; }
    }

    public class ObservationFormulaByInvestigationModel
    {
        public string InvestigationName { get; set; }
        public string ObservationName { get; set; }
        public string FormulaText { get; set; }
        public int ObservationId { get; set; }
        public int InvastigationId { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
    }

    public class LabFormulaMasterResponse
    {
        public int FormulaId { get; set; }
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