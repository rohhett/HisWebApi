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
        public int? IsSampleSegregationRequired { get; set; }
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

    public class LabFormulaMasterComponentRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int typeId { get; set; }

        [Required(ErrorMessage = "Type is required")]
        [StringLength(32, ErrorMessage = "Type cannot exceed 32 characters")]
        public string type { get; set; }

        [Required(ErrorMessage = "Component is required")]
        [StringLength(64, ErrorMessage = "Component cannot exceed 64 characters")]
        public string component { get; set; }

        [Required(ErrorMessage = "SequenceNo is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SequenceNo must be greater than 0")]
        public int sequenceNo { get; set; }
    }

    public class CreateUpdateLabFormulaMasterRequest
    {
        [Required(ErrorMessage = "ObservationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ObservationId must be greater than 0")]
        public int observationId { get; set; }

        [StringLength(1000, ErrorMessage = "FormulaText cannot exceed 1000 characters")]
        public string formulaText { get; set; }

        public string formulaExpression { get; set; }

        public string formulaExpressionRight { get; set; }

        public List<LabFormulaMasterComponentRequest> formulaComponents { get; set; } = new List<LabFormulaMasterComponentRequest>();
    }

    public class DeleteLabFormulaRequest
    {
        [Required(ErrorMessage = "InvestigationId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "InvestigationId must be greater than 0")]
        public int InvestigationId { get; set; }
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


}