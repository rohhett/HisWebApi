using HISWEBAPI.DTO;
using HISWEBAPI.Models;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface IIPDRepository
    {
        ServiceResult<object> GetIPDPatientBedHistory(int visitId);
        ServiceResult<string> TransferIPDPatientBed(TransferIPDPatientBedRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetIPDPatientDoctorHistory(int visitId);
        ServiceResult<string> TransferIPDPatientDoctor(TransferIPDPatientDoctorRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetIPDPatientCorporateHistory(int visitId);
        ServiceResult<string> UpdateIPDPatientTariffDetails(UpdateIPDPatientTariffDetailsRequest request, AllGlobalValues globalValues);
        ServiceResult<SaveCorporateTransferRequestApprovalResponse> SaveCorporateTransferRequestApproval(SaveCorporateTransferRequestApprovalRequest request, AllGlobalValues globalValues);
        ServiceResult<string> ApproveCorporateTransferRequest(ApproveCorporateTransferRequestRequest request, AllGlobalValues globalValues);
        ServiceResult<string> CancelCorporateTransferRequest(CancelCorporateTransferRequestRequest request, AllGlobalValues globalValues);
        ServiceResult<string> ConfirmCorporateTransferRequest(ConfirmCorporateTransferRequestRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetCorporateTransferRequestListForApproval(string fromDate, string toDate, int branchId, AllGlobalValues globalValues);
        ServiceResult<object> GetCorporateTransferRequestDetailsByCorporateTransferId(int corporateTransferId);
        ServiceResult<object> GetCorporateTransferRequestApprovalDetails(int corporateTransferId);
        ServiceResult<object> GetCorporateTransferRequestDetailsByVisitId(int visitId);
        ServiceResult<SaveIPDBillingResponse> SaveIPDBilling(SaveIPDBillingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetIPDBillingSummary(int branchId, int visitId);
        ServiceResult<object> GetIPDPatientBillAmounts(int visitId, int patientId);
        ServiceResult<object> GetIPDPatientOrderDetails(int ftid, AllGlobalValues globalValues);

        ServiceResult<string> RemoveIPDServiceItem(RemoveIPDServiceItemRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateIPDServicePackage(UpdateIPDServicePackageRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateIPDServiceCorporateNonPayable(UpdateIPDServiceCorporateNonPayableRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateIPDServiceDiscAmt(UpdateIPDServiceDiscAmtRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateIPDServiceDiscPer(UpdateIPDServiceDiscPerRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateIPDServiceRate(UpdateIPDServiceRateRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateIPDServiceQty(UpdateIPDServiceQtyRequest request, AllGlobalValues globalValues);
        ServiceResult<SaveIPDPatientAdvanceResponse> SaveIPDPatientAdvance(SaveIPDPatientAdvanceRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<Dictionary<string, object>>> GetIPDReceiptDetails(int receiptId);

        ServiceResult<object> InitializePatientDischargeProcess(InitializePatientDischargeProcessRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetPatientDischargeProcess(int visitId,int branchId, AllGlobalValues globalValues);
        ServiceResult<object> GetCurrentDischargeProcess(int visitId);
        ServiceResult<object> StartPatientDischargeProcess(StartPatientDischargeProcessRequest request, AllGlobalValues globalValues);
        ServiceResult<object> CompletePatientDischargeProcess(CompletePatientDischargeProcessRequest request, AllGlobalValues globalValues);
        ServiceResult<object> ValidatePatientDischargeProcess(int visitId);
        ServiceResult<string> SaveIPDDischarge(SaveIPDDischargeRequest request, AllGlobalValues globalValues);
        ServiceResult<CreateSupplementaryBillFromMainBillResponse> CreateSupplementaryBillFromMainBill(CreateSupplementaryBillFromMainBillRequest request, AllGlobalValues globalValues);

    }
}