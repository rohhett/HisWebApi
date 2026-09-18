using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HISWEBAPI.DTO
{
    public class CreateUpdateAllergyMasterRequest
    {
        public int AllergyId { get; set; } = 0;

        [Required(ErrorMessage = "AllergyName is required")]
        [StringLength(256, ErrorMessage = "AllergyName cannot exceed 256 characters")]
        public string AllergyName { get; set; }

        [Required(ErrorMessage = "AllergyTypeId is required")]
        public int AllergyTypeId { get; set; }

        [StringLength(100, ErrorMessage = "AllergyType cannot exceed 100 characters")]
        public string AllergyType { get; set; }

        public string SnomedCode { get; set; }

        [Required(ErrorMessage = "Active is required")]
        [Range(0, 1, ErrorMessage = "Active must be 0 or 1")]
        public int Active { get; set; }
    }

    public class DeleteAllergyMasterRequest
    {
        [Required(ErrorMessage = "AllergyId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "AllergyId must be greater than 0")]
        public int AllergyId { get; set; }
    }

    // ─── Patient Allergy Details ──────────────────────────────────────────────────

    public class CreateUpdatePatientAllergyDetailsRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "PatientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than 0")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "AllergyId is required")]
        public int AllergyId { get; set; }

        [StringLength(256, ErrorMessage = "AllergyName cannot exceed 256 characters")]
        public string AllergyName { get; set; }

        [Required(ErrorMessage = "AllergyTypeId is required")]
        public int AllergyTypeId { get; set; }

        [StringLength(256, ErrorMessage = "AllergyType cannot exceed 256 characters")]
        public string AllergyType { get; set; }

        [StringLength(512, ErrorMessage = "Reaction cannot exceed 512 characters")]
        public string Reaction { get; set; }

        [StringLength(512, ErrorMessage = "Remarks cannot exceed 512 characters")]
        public string Remarks { get; set; }

        [StringLength(100, ErrorMessage = "InteractionSeverity cannot exceed 100 characters")]
        public string InteractionSeverity { get; set; }

        [StringLength(100, ErrorMessage = "ClinicalStatus cannot exceed 100 characters")]
        public string ClinicalStatus { get; set; }

        [StringLength(100, ErrorMessage = "VerificationStatus cannot exceed 100 characters")]
        public string VerificationStatus { get; set; }

        [StringLength(100, ErrorMessage = "SnomedCode cannot exceed 100 characters")]
        public string SnomedCode { get; set; }

        public int NotKnownAllergy { get; set; } = 0;
    }

    public class CreateUpdatePatientAllergyDetailsResponse
    {
        public int Id { get; set; }
    }

    public class DeletePatientAllergyDetailsRequest
    {
        [Required(ErrorMessage = "Id is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Id must be greater than 0")]
        public int Id { get; set; }

        [Required(ErrorMessage = "PatientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than 0")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "DeactivationRemarks is required")]
        [StringLength(512, ErrorMessage = "DeactivationRemarks cannot exceed 512 characters")]
        public string DeactivationRemarks { get; set; }
    }

    public class CreateUpdateDiagnosisMasterRequest
    {
        public int DiagnosisId { get; set; } = 0;

        [Required(ErrorMessage = "DiagnosisName is required")]
        [StringLength(256, ErrorMessage = "DiagnosisName cannot exceed 256 characters")]
        public string DiagnosisName { get; set; }

        [StringLength(100, ErrorMessage = "SnomedCode cannot exceed 100 characters")]
        public string? SnomedCode { get; set; }

        [Required(ErrorMessage = "Active is required")]
        [Range(0, 1, ErrorMessage = "Active must be 0 or 1")]
        public int Active { get; set; }
    }

    public class CreateUpdateProcedureMasterRequest
    {
        public int ProcedureId { get; set; } = 0;

        [Required(ErrorMessage = "ProcedureName is required")]
        [StringLength(256, ErrorMessage = "ProcedureName cannot exceed 256 characters")]
        public string ProcedureName { get; set; }

        [StringLength(100, ErrorMessage = "SnomedCode cannot exceed 100 characters")]
        public string? SnomedCode { get; set; }

        [Required(ErrorMessage = "Active is required")]
        [Range(0, 1, ErrorMessage = "Active must be 0 or 1")]
        public int Active { get; set; }
    }


    public class CreateUpdateEMRSectionMasterRequest
    {
        public int SectionId { get; set; } = 0;

        [Required(ErrorMessage = "SectionName is required")]
        [StringLength(256, ErrorMessage = "SectionName cannot exceed 256 characters")]
        public string SectionName { get; set; }

        [StringLength(256, ErrorMessage = "DisplayName cannot exceed 256 characters")]
        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; }

        public List<EMRSectionHeaderMappingItem> HeaderMappings { get; set; } = new();
    }

    public class EMRSectionHeaderMappingItem
    {
        [Required(ErrorMessage = "HeaderId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "HeaderId must be greater than 0")]
        public int HeaderId { get; set; }

        public int SequenceNo { get; set; } = 0;
    }
    public class SaveEMRSectionDepartmentMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TypeId must be greater than 0")]
        public int TypeId { get; set; }

        [StringLength(100)]
        public string TypeName { get; set; }

        [Required(ErrorMessage = "RelatedToId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RelatedToId must be greater than 0")]
        public int RelatedToId { get; set; }

        public List<EMRSectionDepartmentMappingItemRequest> HeaderMappingData { get; set; }
    }

    public class EMRSectionDepartmentMappingItemRequest
    {



        [Required(ErrorMessage = "SectionId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SectionId must be greater than 0")]
        public int SectionId { get; set; }
        public int SequenceNo { get; set; } = 0;
    }

    public class SaveEMRSectionScoreFormulaRequest
    {
        [Required(ErrorMessage = "SectionId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SectionId must be greater than 0")]
        public int SectionId { get; set; }

        public List<EMRSectionScoreFormulaItem> FormulaItems { get; set; } = new();
    }

    public class EMRSectionScoreFormulaItem
    {
        [Required(ErrorMessage = "HeaderId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "HeaderId must be greater than equal to 0")]
        public int HeaderId { get; set; }

        [StringLength(256, ErrorMessage = "ReferenceName cannot exceed 256 characters")]
        public string? ReferenceName { get; set; }

        [StringLength(512, ErrorMessage = "FormulaDefinition cannot exceed 512 characters")]
        public string? FormulaDefinition { get; set; }
    }

    public class SaveEMRSectionAttributeConditionRequest
    {
        [Required(ErrorMessage = "SectionId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SectionId must be greater than 0")]
        public int SectionId { get; set; }

        public List<AttributeConditionGroup> AttributeConditions { get; set; } = new();
    }

    public class AttributeConditionGroup
    {
        [Required(ErrorMessage = "TargetHeaderId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TargetHeaderId must be greater than 0")]
        public int TargetHeaderId { get; set; }

        [Required(ErrorMessage = "Conditions are required")]
        public List<AttributeConditionItem> Conditions { get; set; } = new();
    }

    public class AttributeConditionItem
    {
        [Required(ErrorMessage = "HeaderId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "HeaderId must be greater than 0")]
        public int HeaderId { get; set; }

        [Required(ErrorMessage = "Operator is required")]
        [StringLength(10, ErrorMessage = "Operator cannot exceed 10 characters")]
        public string Operator { get; set; }

        [StringLength(256, ErrorMessage = "Value cannot exceed 256 characters")]
        public string? Value { get; set; }

        [StringLength(10, ErrorMessage = "Connector cannot exceed 10 characters")]
        public string? Connector { get; set; }
    }

    public class SaveDoctorFavouriteEMRSectionsRequest
    {
        [Required(ErrorMessage = "DoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than 0")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "SectionIds is required")]
        public List<int> SectionIds { get; set; } = new List<int>();
    }

    public class CreateUpdateChiefComplaintMasterRequest
    {
        public int ComplaintId { get; set; } = 0;

        [Required(ErrorMessage = "ComplaintName is required")]
        [StringLength(512, ErrorMessage = "ComplaintName cannot exceed 512 characters")]
        public string ComplaintName { get; set; }

        [StringLength(100, ErrorMessage = "SnomedCode cannot exceed 100 characters")]
        public string? SnomedCode { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateChiefComplaintMasterResponse
    {
        public int ComplaintId { get; set; }
    }

    public class SaveDoctorFavouriteTableEntryRequest
    {
        [Required(ErrorMessage = "DoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than 0")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "EntityId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "EntityId must be greater than 0")]
        public int EntityId { get; set; }

        [Required(ErrorMessage = "RecordId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RecordId must be greater than 0")]
        public int RecordId { get; set; }

        [Required(ErrorMessage = "IsFavorite is required")]
        public bool IsFavorite { get; set; }

        // Accepts a raw JSON object (or array/string) from the client.
        [Required(ErrorMessage = "Entry is required")]
        public JsonElement Entry { get; set; }
    }

    public class DeleteDoctorFavouriteTableEntryRequest
    {
        [Required(ErrorMessage = "Id is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Id must be greater than 0")]
        public int Id { get; set; }
    }


    public class UploadEMRControlDocumentRequest
    {
        [Required(ErrorMessage = "HeaderId is required")]
        public int HeaderId { get; set; }

        [Required(ErrorMessage = "DocumentId is required")]
        public int DocumentId { get; set; }

        [StringLength(100, ErrorMessage = "Image name cannot exceed 100 characters")]
        public string ImageName { get; set; }

        [Required(ErrorMessage = "Document file is required")]
        public IFormFile DocumentFile { get; set; }
    }


    public class CreateUpdateDoseMasterRequest
    {
        public int DoseId { get; set; } = 0;

        [Required(ErrorMessage = "Dose is required")]
        [StringLength(100, ErrorMessage = "Dose cannot exceed 100 characters")]
        public string Dose { get; set; }

        [StringLength(256, ErrorMessage = "DoseTimes cannot exceed 256 characters")]
        public string? DoseTimes { get; set; }

        [StringLength(256, ErrorMessage = "DoseTimeLabels cannot exceed 256 characters")]
        public string? DoseTimeLabels { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; }
    }

    public class UploadEMRDocumentRequest
    {
        [Required(ErrorMessage = "VisitId is required")]
        public int VisitId { get; set; }

        [Required(ErrorMessage = "DocumentId is required")]
        public int DocumentId { get; set; }

        [Required(ErrorMessage = "Document file is required")]
        public IFormFile DocumentFile { get; set; }
    }

    public class DoctorConsultationDetailsRequest
    {
        [Required(ErrorMessage = "DoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than 0")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "PatientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than 0")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "VisitId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VisitId must be greater than 0")]
        public int VisitId { get; set; }

        /// <summary>1 = OPD, 2 = IPD</summary>
        [Required(ErrorMessage = "VisitTypeId is required")]
        [Range(1, 2, ErrorMessage = "VisitTypeId must be 1 (OPD) or 2 (IPD)")]
        public int VisitTypeId { get; set; }

        /// <summary>1 = mark file closed (consultation done), 0 = mark patient out</summary>
        public int IsFileClosed { get; set; } = 0;
        public int IsTemperatureRoomOut { get; set; } = 0;
        public int PatientVitalId { get; set; } = 0;
        public DateTime VitalDateTime { get; set; }


    }

    public class ConsultationHeaderDataRequest
    {
        /// <summary>0 = insert new, >0 = existing row Id to delete/replace</summary>
        public int DataId { get; set; } = 0;

        [Required(ErrorMessage = "SectionId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SectionId must be greater than 0")]
        public int SectionId { get; set; }

        [Required(ErrorMessage = "HeaderId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "HeaderId must be greater than 0")]
        public int HeaderId { get; set; }

        [Required(ErrorMessage = "ControlTypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ControlTypeId must be greater than 0")]
        public int ControlTypeId { get; set; }

        [Required(ErrorMessage = "TemplateId is required")]
        public int TemplateId { get; set; } = 0;

        public string? HeaderValue { get; set; }

    }

    public class PatientVitalValueRequest
    {
        [Required(ErrorMessage = "VitalId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VitalId must be greater than 0")]
        public int VitalId { get; set; }
        public string? VitalValue { get; set; }
    }

    public class SavePatientConsultationRequest
    {
        [Required(ErrorMessage = "ConsultationDetails is required")]
        public DoctorConsultationDetailsRequest ConsultationDetails { get; set; }

        public List<ConsultationHeaderDataRequest> ConsultationHeadersData { get; set; } = new();
        public List<PatientVitalValueRequest> PatientVitalValue { get; set; } = new();
    }

    public class SavePatientConsultationResponse
    {
        public int VisitId { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
    }


    public class CreateUpdateTemplateCategoryMasterRequest
    {
        public int TemplateCategoryId { get; set; } = 0;

        [Required(ErrorMessage = "TemplateCategoryName is required")]
        [StringLength(256, ErrorMessage = "TemplateCategoryName cannot exceed 256 characters")]
        public string TemplateCategoryName { get; set; }
    }

    public class CreateUpdateEMRTemplateMasterRequest
    {
        public int TemplateId { get; set; } = 0;

        [Required(ErrorMessage = "TemplateName is required")]
        [StringLength(256, ErrorMessage = "TemplateName cannot exceed 256 characters")]
        public string TemplateName { get; set; }

        [StringLength(256, ErrorMessage = "DisplayName cannot exceed 256 characters")]
        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "TemplateCategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TemplateCategoryId must be greater than 0")]
        public int TemplateCategoryId { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; }
        public int IsMultipleEntryAllow { get; set; } = 0;
        public int ApplicableTo { get; set; } = 0;

        public List<EMRTemplateSectionMappingItem> SectionMappings { get; set; } = new();
    }

    public class EMRTemplateSectionMappingItem
    {
        [Required(ErrorMessage = "SectionId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SectionId must be greater than 0")]
        public int SectionId { get; set; }

        public int SequenceNo { get; set; } = 0;
    }

    public class SaveEMRTemplateDepartmentMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TypeId must be greater than 0")]
        public int TypeId { get; set; }

        [StringLength(100)]
        public string TypeName { get; set; }

        [Required(ErrorMessage = "RelatedToId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RelatedToId must be greater than 0")]
        public int RelatedToId { get; set; }

        public List<EMRTemplateDepartmentMappingItemRequest> SectionMappingData { get; set; }
    }

    public class EMRTemplateDepartmentMappingItemRequest
    {
        [Required(ErrorMessage = "TemplateId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TemplateId must be greater than 0")]
        public int TemplateId { get; set; }

        public int SequenceNo { get; set; } = 0;
    }

    public class CarePlanHeaderDataItemRequest
    {
        [Required(ErrorMessage = "SectionId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SectionId must be greater than 0")]
        public int SectionId { get; set; }

        [Required(ErrorMessage = "HeaderId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "HeaderId must be greater than 0")]
        public int HeaderId { get; set; }

        [Required(ErrorMessage = "ControlTypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ControlTypeId must be greater than 0")]
        public int ControlTypeId { get; set; }

        public string? HeaderValue { get; set; }
    }

    public class CreateUpdateCarePlanRequest
    {
        public int CarePlanId { get; set; } = 0;

        [Required(ErrorMessage = "CarePlanName is required")]
        [StringLength(256, ErrorMessage = "CarePlanName cannot exceed 256 characters")]
        public string CarePlanName { get; set; }

        [Required(ErrorMessage = "DoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than 0")]
        public int DoctorId { get; set; }

        public List<CarePlanHeaderDataItemRequest> HeadersData { get; set; } = new();
    }

    public class CreateUpdateCarePlanResponse
    {
        public int CarePlanId { get; set; }
    }
}