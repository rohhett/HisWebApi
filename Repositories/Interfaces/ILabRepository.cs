using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using HISWEBAPI.Configuration;
using System.Collections.Generic;
using System.Data;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface ILabRepository
    {
        ServiceResult<CreateUpdateSampleTypeMasterResponse> CreateUpdateSampleTypeMaster(CreateUpdateSampleTypeMasterRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SampleTypeMasterModel>> GetAllSampleTypeMaster(int? isActive = null);
        ServiceResult<IEnumerable<SampleContainerColorMasterModel>> GetSampleContainerColorMaster();
        ServiceResult<CreateUpdateLabMethodMasterResponse> CreateUpdateLabMethodMaster(CreateUpdateLabMethodMasterRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<LabMethodMasterModel>> GetLabMethodMaster( int? isActive = null);
        ServiceResult<CreateUpdateSampleRemarksMasterResponse> CreateUpdateSampleRemarksMaster(CreateUpdateSampleRemarksMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SampleRemarksMasterModel>> GetSampleRemarksMaster(int? isActive = null);
        ServiceResult<CreateUpdateSampleRejectionRemarksMasterResponse> CreateUpdateSampleRejectionRemarksMaster(CreateUpdateSampleRejectionRemarksMasterRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SampleRejectionRemarksMasterModel>> GetSampleRejectionRemarksMaster(int? isActive = null);
        ServiceResult<CreateUpdateFieldBoyMasterResponse> CreateUpdateFieldBoyMaster( CreateUpdateFieldBoyMasterRequest request,   AllGlobalValues globalValues);
        ServiceResult<IEnumerable<FieldBoyMasterModel>> GetFieldBoyMaster(int? isActive = null);
        ServiceResult<ServiceItemMasterResponse> CreateUpdateInvestigationServiceItemMaster(CreateUpdateServiceItemRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<ServiceItemMasterModel>> GetInvestigationServiceItemList(int? serviceItemId,int? isActive,string categoryTypeId, string categoryId,int? subCategoryId, int? subSubCategoryId, int? labTypeId, int? reportTypeId, string serviceName);
        ServiceResult<IEnumerable<ObservationMasterModel>> GetObservationMaster(int? observationId = null, int? isActive = null);
        ServiceResult<CreateUpdateObservationMasterResponse> CreateUpdateObservationMaster(CreateUpdateObservationMasterRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<InvastigationObservationMappingModel>> GetInvastigationObservationMapping( int investigationId);
        ServiceResult<SubmitInvastigationObservationMappingResponse> SubmitInvastigationObservationMapping(SubmitInvastigationObservationMappingRequest request,  AllGlobalValues globalValues);
        ServiceResult<IEnumerable<InvastigationObservationRangeMasterModel>> GetInvastigationObservationRangeMaster(int observationId, string gender);
        ServiceResult<SubmitInvastigationObservationRangeMasterResponse> SubmitInvastigationObservationRangeMaster(SubmitInvastigationObservationRangeMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<LabFormulaMasterModel>> GetFormulaMasterByObservationId(int observationId);
        ServiceResult<LabFormulaMasterResponse> CreateUpdateLabFormulaMaster(CreateUpdateLabFormulaMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<ObservationFormulaByInvestigationModel>> GetObservationFormulaByInvestigationId(int investigationId);
        ServiceResult<string> DeleteLabFormulaByObservationid(int Observationid, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientInvestigationForSampleManagement(
    int branchId, int typeId, string uhid, string ipdNo, string labNo,
    string fromDate, string toDate, string barCode, int subCategoryId,
    int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId,int sampleTypeId,int roomTypeId,string isUrgent);

      
        ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientInvestigationForSampleDepartmentReceiving(
   int branchId, int typeId, string uhid, string ipdNo, string labNo,
   string fromDate, string toDate, string barCode, int subCategoryId,
   int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int sampleTypeId, int roomTypeId, string isUrgent);

        ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientInvestigationForSampleSegregation(
 int branchId, int typeId, string uhid, string ipdNo, string labNo,
 string fromDate, string toDate, string barCode, int subCategoryId,
 int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int sampleTypeId, int roomTypeId, string isUrgent);

        ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingPathology(
  int branchId, int typeId, string uhid, string ipdNo, string labNo,
  string fromDate, string toDate, string barCode, int subCategoryId,
  int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId,int canSampleCollect);

        ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingRadiology(
int branchId, int typeId, string uhid, string ipdNo, string labNo,
string fromDate, string toDate, string barCode, int subCategoryId,
int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId);

        ServiceResult<string> UpdateSampleStatus(UpdateSampleStatusRequest request, AllGlobalValues globalValues);
        ServiceResult<string> RejectSampleStatus(RejectSampleStatusRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateReportApproval(UpdateReportApprovalRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetPatientInvestigationDetails(int branchId, string uhid, int labNo, int visitId);
        ServiceResult<string> CreateUpdatePatientInvestigationRemark(CreateUpdatePatientInvestigationRemarkRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetPatientInvestigationRemark(int patientInvestigationId);
        ServiceResult<string> DeletePatientInvestigationRemark(int remarkId, int patientInvestigationId);
        ServiceResult<string> CreateUpdateInvestigationDocumentNameMaster(CreateUpdateInvestigationDocumentNameMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<InvestigationDocumentNameMasterModel>> GetInvestigationDocumentNameMaster();
        ServiceResult<string> InsertPatientInvestigationDocument(InsertPatientInvestigationDocumentRequest request, AllGlobalValues globalValues, string uploadedFilePath);
        ServiceResult<object> GetPatientInvestigationDocumentList(int patientInvestigationId);
        ServiceResult<string> DeletePatientInvestigationDocument(int patientDocumentId, int patientInvestigationId);
        ServiceResult<object> GetPatientTabularReportForResultEntry(int patientInvestigationId);
        ServiceResult<object> GetPatientFreeTextReportForResultEntry(int patientInvestigationId);
        ServiceResult<object> GetAllInvestigationNameOfPatient(int branchId, string uhid, int labNo, int labTypeId, int visitId);
        ServiceResult<string> SavePatientTabularReport(SavePatientTabularReportRequest request, AllGlobalValues globalValues);
        ServiceResult<string> SavePatientFreeTextReport(SavePatientFreeTextReportRequest request, AllGlobalValues globalValues);
        //ServiceResult<string> CreateUpdateInvastigationTemplateCommentMaster(List<InvastigationTemplateCommentMasterRequest> request, AllGlobalValues globalValues);
        ServiceResult<object> CreateUpdateInvastigationTemplateCommentMaster(List<InvastigationTemplateCommentMasterRequest> request, AllGlobalValues globalValues);
        ServiceResult<object> GetInvastigationTemplateCommentMaster(int id, int typeId);
        ServiceResult<object> GetAllInvestigationTemplateComments(int? isActive = null, int? typeId = null);
        ServiceResult<string> CreateUpdateObservationLOVMaster(CreateUpdateObservationLOVMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetObservationListOfValuesMaster();
        ServiceResult<string> SaveInvestigationTemplateInterpretationMappings(List<InvestigationTemplateInterpretationMappingRequest> mappingItems, AllGlobalValues globalValues);
        ServiceResult<object> GetInvestigationTemplateInterpretationMappings(int investigationId);
        ServiceResult<string> SaveObservationCommentsLOVsMappings(List<ObservationCommentLOVsMappingRequest> mappingItems, AllGlobalValues globalValues);
        ServiceResult<object> GetObservationCommentLOVsMappings(int observationId);
        ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForLaboratoryHelpDesk(
int branchId, int typeId, string uhid, string ipdNo, string labNo,
string fromDate, string toDate, string barCode, int subCategoryId,
int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId);

        // Histo Template
        ServiceResult<CreateUpdateHistoTemplateResponse> CreateUpdateHistoTemplateMaster(CreateUpdateHistoTemplateRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<HistoTemplateMasterModel>> GetHistoTemplateMaster(int typeId);

        // Specimen Master
        ServiceResult<CreateUpdateSpecimenMasterResponse> CreateUpdateSpecimenMaster(CreateUpdateSpecimenMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SpecimenMasterModel>> GetSpecimenMaster();

        // Specimen Mapping
        ServiceResult<CreateUpdateSpecimenMappingResponse> CreateUpdateSpecimenMappingMaster(CreateUpdateSpecimenMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<SpecimenMappingMasterModel> GetSpecimenMappingMaster(int specimenNameId);

        // Histo Pending Reason
        ServiceResult<CreateUpdateHistoPendingReasonResponse> CreateUpdateHistoPendingReasonMaster(CreateUpdateHistoPendingReasonRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<HistoPendingReasonMasterModel>> GetHistoPendingReasonMaster();

        // Histo Immuno Antibiotic
        ServiceResult<CreateUpdateHistoImmunoAntibioticResponse> CreateUpdateHistoImmunoAntibioticMaster(CreateUpdateHistoImmunoAntibioticRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<HistoImmunoAntibioticMasterModel>> GetHistoImmunoAntibioticMaster();

        // Organism Group
        ServiceResult<CreateUpdateOrganismGroupResponse> CreateUpdateOrganismGroup(CreateUpdateOrganismGroupRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<OrganismGroupModel>> GetOrganismGroupList();

        // Organism Name
        ServiceResult<CreateUpdateOrganismNameResponse> CreateUpdateOrganismName(CreateUpdateOrganismNameRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<OrganismNameModel>> GetOrganismNameList();

        // Antibiotic Group
        ServiceResult<CreateUpdateAntibioticGroupResponse> CreateUpdateAntibioticGroup(CreateUpdateAntibioticGroupRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<AntibioticGroupModel>> GetAntibioticGroupList();

        // Antibiotic Name
        ServiceResult<CreateUpdateAntibioticNameResponse> CreateUpdateAntibioticName(CreateUpdateAntibioticNameRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<AntibioticNameModel>> GetAntibioticNameList();

        // Micro Template
        ServiceResult<CreateUpdateMicroTemplateResponse> CreateUpdateMicroTemplate(CreateUpdateMicroTemplateRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<MicroTemplateMasterModel>> GetMicroTemplateList(int typeId);

        // Micro Mapping
        ServiceResult<string> CreateUpdateMicroMapping(CreateUpdateMicroMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<MicroMappingModel>> GetMicroMappingByOrganismId(int organismId);

        ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingHisto(
int branchId, int typeId, string uhid, string ipdNo, string labNo,
string fromDate, string toDate, string barCode, int subCategoryId,
int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int canSampleCollect);


        ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingMicro(
int branchId, int typeId, string uhid, string ipdNo, string labNo,
string fromDate, string toDate, string barCode, int subCategoryId,
int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int canSampleCollect);

        ServiceResult<object> GetSampleJourneyByPatientInvestigationId(int patientInvestigationId);

    }
}
