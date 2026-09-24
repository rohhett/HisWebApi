using System.Data;
using Microsoft.Data.SqlClient;
using HISWEBAPI.Data.Helpers;

namespace HISWEBAPI.Domain
{
    public class Receipts
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int RoleId { get; set; }
        public int BillId { get; set; }
        public int? VisitId { get; set; }
        public int PatientId { get; set; }
        public decimal Amount { get; set; }
        public int UserId { get; set; }
        public string IpAddress { get; set; }
        public string UniqueId { get; set; }
        public int IsStore { get; set; }
        public int IsReturn { get; set; }
        public int IsBloodBank { get; set; }
        public int IsBloodBankReturn { get; set; }
        public int IsExpenseReceipt { get; set; }
        public int IsAdvanceReceipt { get; set; }
        public int IsCopaymentReceipt { get; set; }
        public int ExpenseId { get; set; }
        public int IsCorporateReceipt { get; set; }
        public string Remarks { get; set; }
        public string GuardianName { get; set; }
        public string PlutusTransactionReferenceID { get; set; }
        public string TransactionLogId { get; set; }
        public string ReceiptDate { get; set; }

        public dynamic Create(ICustomSqlHelper sqlHelper, SqlTransaction tnx)
        {
            return sqlHelper.DML(tnx, "I_Receipts", CommandType.StoredProcedure, new
            {
                @hospId = HospId,
                @branchId = BranchId,
                @roleId = RoleId,
                @BillId = BillId,
                @visitId = VisitId,
                @patientId = PatientId,
                @amount = Amount,
                @userId = UserId,
                @IpAddress = IpAddress,
                @uniqueId = UniqueId,
                @isStore = IsStore,
                @isReturn = IsReturn,
                @isBloodBank = IsBloodBank,
                @isBloodBankReturn = IsBloodBankReturn,
                @isExpenseReceipt = IsExpenseReceipt,
                @isAdvanceReceipt = IsAdvanceReceipt,
                @isCopaymentReceipt = IsCopaymentReceipt,
                @expenseId = ExpenseId,
                @isCorporateReceipt = IsCorporateReceipt,
                @remarks = Remarks,
                @plutusTransactionReferenceID = PlutusTransactionReferenceID,
                @transactionLogId = TransactionLogId,
                @GuardianName = GuardianName,
                @ReceiptDate = ReceiptDate
            }, new { result = 0 });
        }
    }
}