using System.Data;
using Microsoft.Data.SqlClient;
using HISWEBAPI.Data.Helpers;

namespace HISWEBAPI.Domain
{
    public class ReceiptsPaymentModeDetails
    {
        public int HospId { get; set; }
        public int BranchId { get; set; }
        public int ReceiptID { get; set; }
        public decimal Amount { get; set; }
        public int PaymentModeId { get; set; }
        public int? BankId { get; set; }
        public string ReferenceNo { get; set; }
        public int UserId { get; set; }
        public string IpAddress { get; set; }

        public dynamic Create(ICustomSqlHelper sqlHelper, SqlTransaction tnx)
        {
            return sqlHelper.DML(tnx, "I_ReceiptsPaymentModeDetails", CommandType.StoredProcedure, new
            {
                @hospId = HospId,
                @branchId = BranchId,
                @receiptID = ReceiptID,
                @amount = Amount,
                @paymentModeId = PaymentModeId,
                @bankId = BankId,
                @referenceNo = ReferenceNo,
                @userId = UserId,
                @IpAddress = IpAddress
            }, new { result = 0 });
        }
    }
}