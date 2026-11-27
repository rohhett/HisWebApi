using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.DTO
{
    public class CreateUpdateSampleTypeMasterRequest
    {
        public int SampleTypeId { get; set; }

        [Required(ErrorMessage = "Sample Type is required")]
        [StringLength(256, ErrorMessage = "Sample Type cannot exceed 256 characters")]
        public string SampleType { get; set; }

        [Required(ErrorMessage = "Container Color is required")]
        public int ContainerColorId { get; set; }

        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateSampleTypeMasterResponse
    {
        public int SampleTypeId { get; set; }
    }

    public class CreateUpdateLabMethodMasterRequest
    {
        public int MethodId { get; set; }

        [Required(ErrorMessage = "Method is required")]
        [StringLength(256, ErrorMessage = "Method cannot exceed 256 characters")]
        public string Method { get; set; }

        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateLabMethodMasterResponse
    {
        public int MethodId { get; set; }
    }

    public class CreateUpdateSampleRemarksMasterRequest
    {
        public int SampleRemarksID { get; set; } = 0;

        [Required(ErrorMessage = "SampleRemarks is required")]
        public string SampleRemarks { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateSampleRemarksMasterResponse
    {
        public int SampleRemarksID { get; set; }
    }


    public class CreateUpdateSampleRejectionRemarksMasterRequest
    {
        public int SampleRejectionRemarksID { get; set; } = 0;

        [Required(ErrorMessage = "SampleRejectionRemarks is required")]
        public string SampleRejectionRemarks { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateSampleRejectionRemarksMasterResponse
    {
        public int SampleRejectionRemarksID { get; set; }
    }

    public class CreateUpdateFieldBoyMasterRequest
    {
        public int FieldBoyId { get; set; } = 0;

        [Required(ErrorMessage = "FieldBoyName is required")]
        public string FieldBoyName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateFieldBoyMasterResponse
    {
        public int FieldBoyId { get; set; }
    }


    public class CreateUpdateServiceItemRequest
    {
        public int ServiceItemId { get; set; } = 0;

        [Required(ErrorMessage = "CategoryId is required")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "SubCategoryId is required")]
        public int SubCategoryId { get; set; }

        [Required(ErrorMessage = "SubSubCategoryId is required")]
        public int SubSubCategoryId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(256, ErrorMessage = "Name cannot exceed 256 characters")]
        public string Name { get; set; }

        public string Code { get; set; }
        public int? ReportTypeId { get; set; }
        public string ReportType { get; set; }
        public int? IsSampleRequired { get; set; }
        public int? SampleTypeId { get; set; }
        public string SampleTypeList { get; set; }
        public int? LabMethodId { get; set; }
        public int? ForGenderId { get; set; }
        public string ForGender { get; set; }
        public int IsOutSource { get; set; } = 0;
        public int? IsPrintAlone { get; set; }
        public int? IsSentToDepartmentRequired { get; set; }
        public int? IsDepartmentReceivingRequired { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        public string ShortName { get; set; }
        public string SampleVolume { get; set; }
        public string InvestigationComment { get; set; }
        public int TatInMin { get; set; } = 0;

        [StringLength(50, ErrorMessage = "SNOMED Code cannot exceed 50 characters")]
        public string? SNOMEDCode { get; set; }
        public int IsRequiredSeparatePerformingDoctor { get; set; } = 0;
        public string? DoctorDepartmentIds { get; set; }
    }




    public class GetObservationMasterRequest
    {
        public int? ObservationId { get; set; }

        public int? IsActive { get; set; }
    }


    public class CreateUpdateObservationMasterRequest
    {
        public int ObservationId { get; set; } = 0;

        [Required(ErrorMessage = "ObservationName is required")]
        [StringLength(250, ErrorMessage = "ObservationName cannot exceed 250 characters")]
        public string ObservationName { get; set; }

        [StringLength(100)]
        public string PrefixName { get; set; }

        [StringLength(100)]
        public string SuffixName { get; set; }

        public int MethodId { get; set; } = 0;

        public int ShowInDS { get; set; } = 1;

        [StringLength(10)]
        public string RoundUp { get; set; }

        [StringLength(100)]
        public string FieldType { get; set; }

        public int FieldTypeId { get; set; } = 1;
    }

    public class InvastigationObservationMappingItem
    {
        [Range(1, int.MaxValue, ErrorMessage = "InvastigationId must be greater than 0")]
        public int InvastigationId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "ObservationId must be greater than 0")]
        public int ObservationId { get; set; }

        public bool IsHeader { get; set; } = false;
        public bool IsBold { get; set; } = false;
        public bool IsUnderLine { get; set; } = false;
        public bool IsMandatory { get; set; } = false;
    }

    public class SubmitInvastigationObservationMappingRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "InvastigationId must be greater than 0")]
        public int InvastigationId { get; set; }

        public List<InvastigationObservationMappingItem> Observations { get; set; }
            = new List<InvastigationObservationMappingItem>();
    }

 

    public class SubmitInvastigationObservationMappingResponse
    {
        public int InvastigationId { get; set; }
        public int InsertedCount { get; set; }
    }

    public class InvastigationObservationRangeItem
    {
        [Range(1, int.MaxValue, ErrorMessage = "ObservationId must be greater than 0")]
        public int ObservationId { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^(M|F|B)$", ErrorMessage = "Gender must be M, F, or B")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "FromAge is required")]
        [StringLength(10)]
        public string FromAge { get; set; }

        [Required(ErrorMessage = "ToAge is required")]
        [StringLength(10)]
        public string ToAge { get; set; }

        [StringLength(500)]
        public string DefaultValue { get; set; }

        public decimal? MinValue { get; set; }

        public decimal? MaxValue { get; set; }

        [StringLength(50)]
        public string Unit { get; set; }

        [StringLength(100)]
        public string DisplayValue { get; set; }
    }

    public class SubmitInvastigationObservationRangeMasterRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "ObservationId must be greater than 0")]
        public int ObservationId { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^(M|F|B)$", ErrorMessage = "Gender must be M, F, or B")]
        public string Gender { get; set; }

      
        public List<InvastigationObservationRangeItem> Ranges { get; set; }
            = new List<InvastigationObservationRangeItem>();
    }

    public class SampleStatusUpdateData
    {
        [Required(ErrorMessage = "PatientInvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientInvestigationId must be greater than 0")]
        public int PatientInvestigationId { get; set; }

        [Required(ErrorMessage = "BarCode is required")]
        [StringLength(256, ErrorMessage = "BarCode cannot exceed 256 characters")]
        [RegularExpression(@"^\S+.*\S+$|^\S$", ErrorMessage = "BarCode cannot be blank or whitespace")]
        public string BarCode { get; set; }

        [Required(ErrorMessage = "sampleDateTime is required")]
        public string sampleDateTime { get; set; }

        [Required(ErrorMessage = "StatusId is required")]
        public int StatusId { get; set; }

        public int DefaultSampleTypeId { get; set; } = 0;
        public int DepartmentId { get; set; } = 0;

        [Required(ErrorMessage = "LabNo is required")]
        [Range(1, int.MaxValue, ErrorMessage = "LabNo must be greater than 0")]
        public int LabNo { get; set; }
    }

    public class UpdateSampleStatusRequest
    {
        [Required(ErrorMessage = "At least one sample status update is required")]
        [MinLength(1, ErrorMessage = "At least one sample status update is required")]
        public List<SampleStatusUpdateData> Samples { get; set; } = new();
    }

    public class RejectSampleStatusItemRequest
    {
        [Required(ErrorMessage = "PatientInvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientInvestigationId must be greater than 0")]
        public int PatientInvestigationId { get; set; }

        [Required(ErrorMessage = "StatusId is required")]
        [Range(1, 4, ErrorMessage = "StatusId must be 1 (sample Rejected), 2 (Rejected Sample Accepted), 3 (Hold), 4 (Un-Approved)")]
        public int StatusId { get; set; }

        [StringLength(512, ErrorMessage = "CancellationReason cannot exceed 512 characters")]
        public string? CancellationReason { get; set; }
    }

    public class RejectSampleStatusRequest
    {
        [Required(ErrorMessage = "At least one sample reject status update is required")]
        [MinLength(1, ErrorMessage = "At least one sample reject status update is required")]
        public List<RejectSampleStatusItemRequest> Samples { get; set; } = new();
    }

    public class UpdateReportApprovalRequest
    {
        [Required(ErrorMessage = "At least one PatientInvestigationId is required")]
        [MinLength(1, ErrorMessage = "At least one PatientInvestigationId is required")]
        public List<int> PatientInvestigationIds { get; set; } = new();

        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "ApprovedByDoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ApprovedByDoctorId must be greater than 0")]
        public int ApprovedByDoctorId { get; set; }
    }

    public class CreateUpdatePatientInvestigationRemarkRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "PatientInvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientInvestigationId must be greater than 0")]
        public int PatientInvestigationId { get; set; }

        [StringLength(512, ErrorMessage = "TestRemark cannot exceed 512 characters")]
        public string TestRemark { get; set; }

        [StringLength(512, ErrorMessage = "TestComment cannot exceed 512 characters")]
        public string TestComment { get; set; }

        public int TestCommentId { get; set; } = 0;

        public int IsInternal { get; set; } = 0;
    }

    public class DeletePatientInvestigationRemarkRequest
    {
        [Required(ErrorMessage = "RemarkId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RemarkId must be greater than 0")]
        public int RemarkId { get; set; }
    }

    public class CreateUpdateInvestigationDocumentNameMasterRequest
    {
        public int DocumentId { get; set; } = 0;

        [Required(ErrorMessage = "DocumentName is required")]
        [StringLength(256, ErrorMessage = "DocumentName cannot exceed 256 characters")]
        public string DocumentName { get; set; }
    }

    public class InsertPatientInvestigationDocumentRequest
    {
        [Required(ErrorMessage = "PatientInvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientInvestigationId must be greater than 0")]
        public int PatientInvestigationId { get; set; }

        [Required(ErrorMessage = "InvestigationDocumentNameId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvestigationDocumentNameId must be greater than 0")]
        public int InvestigationDocumentNameId { get; set; }

        [Required(ErrorMessage = "Document file is required")]
        public IFormFile UploadFile { get; set; }
    }


    public class SavePatientTabularReportRequest
    {
        [Required(ErrorMessage = "PatientInvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientInvestigationId must be greater than 0")]
        public int PatientInvestigationId { get; set; }

        [Required(ErrorMessage = "InvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvestigationId must be greater than 0")]
        public int InvestigationId { get; set; }

        public string? InvestigationComments { get; set; }
        public int? InvestigationCommentId { get; set; }
        public string? InterpretationValue { get; set; }

        [Required(ErrorMessage = "IsAbnormalResult is required")]
        [Range(0, 1, ErrorMessage = "IsAbnormalResult must be 0 or 1")]
        public int IsAbnormalResult { get; set; }

        [Required(ErrorMessage = "TabularReport list is required")]
        public List<PatientInvestigationsTabularReport> TabularReport { get; set; } = new List<PatientInvestigationsTabularReport>();
    }

    public class PatientInvestigationsTabularReport
    {
        [Required(ErrorMessage = "ObservationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ObservationId must be greater than 0")]
        public int ObservationId { get; set; }

        public string? ResultValue { get; set; }
        public string? MinValue { get; set; }
        public string? MaxValue { get; set; }
        public string? DisplayRange { get; set; }
        public string? Unit { get; set; }
        public string? MachineResult { get; set; }
        public string? MachineDisplayRange { get; set; }
        public string? MachineUnit { get; set; }
        public string? SampleRemark { get; set; }
        public int IsHeader { get; set; }
        public int IsResultBold { get; set; }
    }

    public class SavePatientFreeTextReportRequest
    {
        [Required(ErrorMessage = "PatientInvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientInvestigationId must be greater than 0")]
        public int PatientInvestigationId { get; set; }

        [Required(ErrorMessage = "InvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvestigationId must be greater than 0")]
        public int InvestigationId { get; set; }

        public string? ResultValue { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "TemplateId must be greater than or equal to 0")]
        public int TemplateId { get; set; } = 0;

        public string? InvestigationComments { get; set; }

        [Required(ErrorMessage = "IsAbnormalResult is required")]
        [Range(0, 1, ErrorMessage = "IsAbnormalResult must be 0 or 1")]
        public int IsAbnormalResult { get; set; }
    }

    public class InvastigationTemplateCommentMasterRequest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, 3, ErrorMessage = "TypeId: Template=1, Interpretation=2, Comment=3")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "Type is required")]
        [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters")]
        public string Name { get; set; }

        public string ContentValue { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; } = 1;
    }

   
    public class CreateUpdateObservationLOVMasterRequest
    {
        public int LOVId { get; set; }

        [Required(ErrorMessage = "LOVName is required")]
        [StringLength(512, ErrorMessage = "LOVName cannot exceed 512 characters")]
        public string LOVName { get; set; }
    }

    public class InvestigationTemplateInterpretationMappingRequest
    {
        [Required(ErrorMessage = "typeId is required")]
        [Range(1, 3, ErrorMessage = "TypeId: Template=1, Interpretation=2, Comment=3")]
        public int typeId { get; set; }

        [Required(ErrorMessage = "type is required")]
        [StringLength(512, ErrorMessage = "type cannot exceed 512 characters")]
        public string type { get; set; }

        [Required(ErrorMessage = "investigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvestigationId must be greater than 0")]
        public int investigationId { get; set; }

        [Required(ErrorMessage = "itemid is required")]
        public int itemid { get; set; }
    }

    public class ObservationCommentLOVsMappingRequest
    {
        [Required(ErrorMessage = "typeId is required")]
        [Range(1, 2, ErrorMessage = "TypeId: Comments=1, LOVs=2")]

        public int typeId { get; set; }

        [Required(ErrorMessage = "type is required")]
        [StringLength(512, ErrorMessage = "type cannot exceed 512 characters")]
        public string type { get; set; }

        [Required(ErrorMessage = "observationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "observationId must be greater than 0")]
        public int observationId { get; set; }

        [Required(ErrorMessage = "itemid is required")]
        public int itemid { get; set; }
    }

    // HistoTemplate DTOs
    public class CreateUpdateHistoTemplateRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "Type is required")]
        [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters")]
        public string Name { get; set; }

        public string ContentValue { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateHistoTemplateResponse
    {
        public int Id { get; set; }
    }

    // Specimen Master DTOs
    public class CreateUpdateSpecimenMasterRequest
    {
        public int ID { get; set; } = 0;

        [Required(ErrorMessage = "SpecimenName is required")]
        public string SpecimenName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateSpecimenMasterResponse
    {
        public int ID { get; set; }
    }

    // Specimen Mapping DTOs
    public class CreateUpdateSpecimenMappingRequest
    {
        [Required(ErrorMessage = "SpecimenNameId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SpecimenNameId must be greater than 0")]
        public int SpecimenNameId { get; set; }

        public string GrossIdList { get; set; }
        public string MicroscopicIdList { get; set; }
        public string ImpressionIdList { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateSpecimenMappingResponse
    {
        public int SpecimenNameId { get; set; }
    }

    // Histo Pending Reason DTOs
    public class CreateUpdateHistoPendingReasonRequest
    {
        public int ID { get; set; } = 0;

        [Required(ErrorMessage = "PendingReason is required")]
        public string PendingReason { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateHistoPendingReasonResponse
    {
        public int ID { get; set; }
    }

    // Histo Immuno Antibiotic DTOs
    public class CreateUpdateHistoImmunoAntibioticRequest
    {
        public int ID { get; set; } = 0;

        [Required(ErrorMessage = "AntibioticName is required")]
        public string AntibioticName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateHistoImmunoAntibioticResponse
    {
        public int ID { get; set; }
    }


    // ─── Organism Group ───────────────────────────────────────────────────────

    public class CreateUpdateOrganismGroupRequest
    {
        public int OrganismGroupId { get; set; } = 0;

        [Required(ErrorMessage = "Organism Group Name is required")]
        [StringLength(256, ErrorMessage = "Organism Group Name cannot exceed 256 characters")]
        public string OrganismGroupName { get; set; }
    }

    public class CreateUpdateOrganismGroupResponse
    {
        public int OrganismGroupId { get; set; }
    }

    // ─── Organism Name ────────────────────────────────────────────────────────

    public class CreateUpdateOrganismNameRequest
    {
        public int OrganismNameId { get; set; } = 0;

        [Required(ErrorMessage = "Organism Name is required")]
        [StringLength(256, ErrorMessage = "Organism Name cannot exceed 256 characters")]
        public string OrganismName { get; set; }

        [Required(ErrorMessage = "OrganismGroupId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "OrganismGroupId must be greater than 0")]
        public int OrganismGroupId { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateOrganismNameResponse
    {
        public int OrganismNameId { get; set; }
    }

    // ─── Antibiotic Group ─────────────────────────────────────────────────────

    public class CreateUpdateAntibioticGroupRequest
    {
        public int AntibioticGroupId { get; set; } = 0;

        [Required(ErrorMessage = "Antibiotic Group Name is required")]
        [StringLength(256, ErrorMessage = "Antibiotic Group Name cannot exceed 256 characters")]
        public string AntibioticGroupName { get; set; }
    }

    public class CreateUpdateAntibioticGroupResponse
    {
        public int AntibioticGroupId { get; set; }
    }

    // ─── Antibiotic Name ──────────────────────────────────────────────────────

    public class CreateUpdateAntibioticNameRequest
    {
        public int AntibioticNameId { get; set; } = 0;

        [Required(ErrorMessage = "Antibiotic Name is required")]
        [StringLength(256, ErrorMessage = "Antibiotic Name cannot exceed 256 characters")]
        public string AntibioticName { get; set; }

        [Required(ErrorMessage = "AntibioticGroupId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "AntibioticGroupId must be greater than 0")]
        public int AntibioticGroupId { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateAntibioticNameResponse
    {
        public int AntibioticNameId { get; set; }
    }

    // ─── Micro Template ───────────────────────────────────────────────────────

    public class CreateUpdateMicroTemplateRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TypeId must be greater than 0")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "Type is required")]
        [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(1500, ErrorMessage = "Name cannot exceed 1500 characters")]
        public string Name { get; set; }

        public string ContentValue { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateMicroTemplateResponse
    {
        public int Id { get; set; }
    }

    // ─── Micro Mapping ────────────────────────────────────────────────────────

    public class MicroMappingItemRequest
    {
        public string OrganismName { get; set; }
        public string AntibioticName { get; set; }
        public int AntibioticNameId { get; set; }
        public string AntibioticClassName { get; set; }
        public string BreakPoint { get; set; }
        public string SDD { get; set; }
        public string RefRangeI { get; set; }
        public string RefRangeS { get; set; }
        public string RefRangeR { get; set; }
        public string Resistant { get; set; }
    }

    public class CreateUpdateMicroMappingRequest
    {
        [Required(ErrorMessage = "OrganismId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "OrganismId must be greater than 0")]
        public int OrganismId { get; set; }

        [Required(ErrorMessage = "AntibioticIdList is required")]
        public string AntibioticIdList { get; set; }

        [Required(ErrorMessage = "AntibioticClassId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "AntibioticClassId must be greater than 0")]
        public int AntibioticClassId { get; set; }

        [Required(ErrorMessage = "MicroMappings list is required")]
        [MinLength(1, ErrorMessage = "At least one mapping entry is required")]
        public List<MicroMappingItemRequest> MicroMappings { get; set; } = new();
    }
    public class SavePatientInvestigationTechnicalNoteRequest
    {
        // 0 = insert, >0 = update
        [Range(0, int.MaxValue, ErrorMessage = "Id must be 0 or greater")]
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "InvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvestigationId must be greater than 0")]
        public int InvestigationId { get; set; }

        [Required(ErrorMessage = "TechnicalNote is required")]
        public string TechnicalNote { get; set; }
    }

    public class SavePatientInvestigationTechnicalNoteResponse
    {
        public int Id { get; set; }
    }

    public class CreateUpdateInvestigationResultEntryCommentRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "InvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvestigationId must be greater than 0")]
        public int InvestigationId { get; set; }

        [Required(ErrorMessage = "CommentName is required")]
        [StringLength(200, ErrorMessage = "CommentName cannot exceed 200 characters")]
        public string CommentName { get; set; }

        [Required(ErrorMessage = "CommentValue is required")]
        public string CommentValue { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateInvestigationResultEntryCommentResponse
    {
        public int Id { get; set; }
    }

}

namespace HISWEBAPI.DTO
{
    // ================= Lab TAT Monitor (2026-11-26) =================
    public class LabTATSaveResponse
    {
        public int Id { get; set; }
    }

    public class CreateUpdateLabTATRuleRequest
    {
        public int RuleId { get; set; } = 0;                       // 0 = insert; update changes minutes / clock / note only

        [Required(ErrorMessage = "Rule level is required")]
        [StringLength(10, ErrorMessage = "Rule level cannot exceed 10 characters")]
        public string RuleLevel { get; set; } = "ALL";             // TEST | DEPT | ALL

        public int InvestigationId { get; set; } = 0;
        public int DepartmentId { get; set; } = 0;
        public int PriorityId { get; set; } = 0;                   // 0 = any
        public int ShiftId { get; set; } = 0;                      // 0 = any

        [Range(1, 43200, ErrorMessage = "TAT minutes must be between 1 and 43200")]
        public int TatMinutes { get; set; }

        [Required(ErrorMessage = "Clock is required")]
        [StringLength(10, ErrorMessage = "Clock cannot exceed 10 characters")]
        public string ClockType { get; set; } = "INHERIT";        // INHERIT | WORKING | 24X7

        [StringLength(200, ErrorMessage = "Note cannot exceed 200 characters")]
        public string? Note { get; set; }
    }

    public class UpdateLabTATRuleStatusRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Rule is required")]
        public int RuleId { get; set; }
    }

    public class UpdateLabTATPriorityClockRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Priority is required")]
        public int PriorityId { get; set; }

        [Required(ErrorMessage = "Clock is required")]
        [StringLength(10, ErrorMessage = "Clock cannot exceed 10 characters")]
        public string ClockType { get; set; } = "WORKING";        // WORKING | 24X7
    }

    public class UpdateLabTATShiftRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Shift is required")]
        public int ShiftId { get; set; }

        [Required(ErrorMessage = "From time is required")]
        [StringLength(5, ErrorMessage = "From time must be HH:mm")]
        public string FromTime { get; set; } = string.Empty;

        [Required(ErrorMessage = "To time is required")]
        [StringLength(5, ErrorMessage = "To time must be HH:mm")]
        public string ToTime { get; set; } = string.Empty;
    }

    public class SaveLabTATSettingRequest
    {
        [Required(ErrorMessage = "TAT start is required")]
        [StringLength(20, ErrorMessage = "TAT start cannot exceed 20 characters")]
        public string TatStartPoint { get; set; } = "received";

        [Required(ErrorMessage = "TAT end is required")]
        [StringLength(20, ErrorMessage = "TAT end cannot exceed 20 characters")]
        public string TatEndPoint { get; set; } = "verified";

        [Range(5, 90, ErrorMessage = "At-risk % must be between 5 and 90")]
        public int AtRiskPercent { get; set; } = 25;

        [Range(50, 100, ErrorMessage = "On-time target % must be between 50 and 100")]
        public int OnTimeTargetPercent { get; set; } = 90;
    }

    public class SaveLabTATWorkingHoursRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Department is required")]
        public int DepartmentId { get; set; }

        [Range(0, 6, ErrorMessage = "Day must be 0 (Sunday) to 6 (Saturday)")]
        public int DayOfWeekNo { get; set; }

        public int IsOpen { get; set; } = 1;

        [StringLength(5, ErrorMessage = "From time must be HH:mm")]
        public string? FromTime { get; set; }

        [StringLength(5, ErrorMessage = "To time must be HH:mm")]
        public string? ToTime { get; set; }
    }

    public class CreateUpdateLabTATHolidayRequest
    {
        public int HolidayId { get; set; } = 0;

        [Required(ErrorMessage = "Holiday date is required")]
        public System.DateTime? HolidayDate { get; set; }

        [Required(ErrorMessage = "Holiday name is required")]
        [StringLength(100, ErrorMessage = "Holiday name cannot exceed 100 characters")]
        public string HolidayName { get; set; } = string.Empty;

        public int DepartmentId { get; set; } = 0;                 // 0 = all departments
    }

    public class UpdateLabTATHolidayStatusRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Holiday is required")]
        public int HolidayId { get; set; }
    }

    public class SaveLabTATDelayReasonRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Sample is required")]
        public int PatientInvestigationId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Delay reason is required")]
        public int DelayReasonId { get; set; }

        [StringLength(256, ErrorMessage = "Remark cannot exceed 256 characters")]
        public string? Remark { get; set; }
    }

    public class SimulateLabTATRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Test is required")]
        public int InvestigationId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Priority is required")]
        public int PriorityId { get; set; }

        [Required(ErrorMessage = "TAT start is required")]
        public System.DateTime? StartOn { get; set; }
    }

    // ================= Lab Approval Authority Master (2026-11-27) =================
    public class CreateUpdateLabApprovalAuthorityRequest
    {
        public int LabApprovalAuthorityId { get; set; } = 0;

        /// <summary>0 = default rule (all branches without their own)</summary>
        [Range(0, int.MaxValue, ErrorMessage = "Branch cannot be negative")]
        public int BranchId { get; set; }

        [Range(1, 3, ErrorMessage = "Category is required")]
        public int LabTypeId { get; set; }

        [Required(ErrorMessage = "Select at least one sub department")]
        [StringLength(1000)]
        public string SubSubCategoryIds { get; set; } = "";

        [Range(1, 2, ErrorMessage = "Approval flow is required")]
        public int ApprovalFlowId { get; set; }

        [Required, StringLength(100)]
        public string ApprovalFlow { get; set; } = "";

        [Range(0, 1, ErrorMessage = "All Approvals Required must be 0 or 1")]
        public int IsAllApprovalRequired { get; set; } = 1;

        [Range(1, 4, ErrorMessage = "Approval level is required")]
        public int ApprovalLevelId { get; set; }

        [Required, StringLength(100)]
        public string ApprovalLevel { get; set; } = "";

        [Required(ErrorMessage = "Select at least one level 1 doctor")]
        [StringLength(1000)]
        public string Level1DoctorIds { get; set; } = "";

        [StringLength(1000)]
        public string? Level2DoctorIds { get; set; }

        [StringLength(1000)]
        public string? Level3DoctorIds { get; set; }

        [StringLength(1000)]
        public string? Level4DoctorIds { get; set; }

        [StringLength(1000)]
        public string? ProvisionalUserIds { get; set; }

        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; } = 1;

        /// <summary>Login branch and role: the setup right is checked for them.</summary>
        [Range(1, int.MaxValue, ErrorMessage = "SessionBranchId is required")]
        public int SessionBranchId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }
    }

    public class UpdateLabApprovalAuthorityStatusRequest
    {
        [Range(1, int.MaxValue)]
        public int LabApprovalAuthorityId { get; set; }

        [Range(0, 1)]
        public int IsActive { get; set; }

        [Range(1, int.MaxValue)]
        public int SessionBranchId { get; set; }

        [Range(1, int.MaxValue)]
        public int RoleId { get; set; }
    }

    public class LabApprovalAuthorityResponse
    {
        public int LabApprovalAuthorityId { get; set; }
    }

    // ================= Lab Formula + Result Validation setup (2026-11-27) =================
    public class CreateUpdateInvestigationFormulaRequest
    {
        public int FormulaId { get; set; } = 0;                    // 0 = insert, >0 = update

        [Range(1, int.MaxValue, ErrorMessage = "Investigation is required")]
        public int InvestigationId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Target observation is required")]
        public int ObservationId { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^(B|M|F)$", ErrorMessage = "Gender must be B, M or F")]
        public string Gender { get; set; } = "B";

        [Required(ErrorMessage = "Formula is required")]
        [StringLength(2000, ErrorMessage = "Formula cannot exceed 2000 characters")]
        public string Expression { get; set; } = "";

        [StringLength(2000, ErrorMessage = "Formula text cannot exceed 2000 characters")]
        public string? FormulaText { get; set; }

        [Range(0, 6, ErrorMessage = "Decimal places must be between 0 and 6")]
        public int DecimalPlaces { get; set; } = 2;

        public int IsActive { get; set; } = 1;
    }

    public class ValidationObservationItem
    {
        [Range(1, int.MaxValue, ErrorMessage = "Observation is required")]
        public int ObservationId { get; set; }
        public int SequenceNo { get; set; }
    }

    public class CreateUpdateInvestigationValidationRequest
    {
        public int ValidationId { get; set; } = 0;                 // 0 = insert, >0 = update

        [Range(1, int.MaxValue, ErrorMessage = "Investigation is required")]
        public int InvestigationId { get; set; }

        [Required(ErrorMessage = "Validation name is required")]
        [StringLength(100, ErrorMessage = "Validation name cannot exceed 100 characters")]
        public string ValidationName { get; set; } = "";

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^(B|M|F)$", ErrorMessage = "Gender must be B, M or F")]
        public string Gender { get; set; } = "B";

        [Required(ErrorMessage = "Check type is required")]
        [StringLength(20, ErrorMessage = "Check type cannot exceed 20 characters")]
        public string CheckType { get; set; } = "";

        [StringLength(2000, ErrorMessage = "Expression cannot exceed 2000 characters")]
        public string? Expression { get; set; }

        [StringLength(2000, ErrorMessage = "Expression text cannot exceed 2000 characters")]
        public string? ExpressionText { get; set; }

        [Required(ErrorMessage = "Condition is required")]
        [StringLength(12, ErrorMessage = "Condition cannot exceed 12 characters")]
        public string Operator { get; set; } = "";

        public decimal Value1 { get; set; }
        public decimal? Value2 { get; set; }
        public decimal Tolerance { get; set; } = 0;

        [Range(1, 2, ErrorMessage = "Action must be 1 (block save) or 2 (warning)")]
        public int ActionType { get; set; } = 1;

        public int SkipIfBlank { get; set; } = 1;

        [StringLength(500, ErrorMessage = "Error message cannot exceed 500 characters")]
        public string? ErrorMessage { get; set; }

        public int IsActive { get; set; } = 1;

        public List<ValidationObservationItem> Observations { get; set; } = new();
    }

    public class LabFormulaSetupSaveResponse
    {
        public int Id { get; set; }
    }
}
