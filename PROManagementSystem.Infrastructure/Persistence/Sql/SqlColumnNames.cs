namespace PROManagementSystem.Infrastructure.Persistence.Sql;

// Authoritative column-name reference for raw SQL.
//
// Tables WITHOUT .HasColumnName mappings keep PascalCase columns and must be
// quoted as seen below. Tables WITH .HasColumnName mappings (users, documents,
// client_companies, client_entities, employees, audit_logs, document_versions,
// document_dependencies) use unquoted snake_case and are kept inline.
internal static class SqlColumnNames
{
    // renewal_tasks
    public const string RenewalTaskId = "\"Id\"";
    public const string RenewalTaskStatus = "\"Status\"";
    public const string RenewalTaskDocumentId = "\"DocumentId\"";
    public const string RenewalTaskClientCompanyId = "\"ClientCompanyId\"";
    public const string RenewalTaskAssignedStaffId = "\"AssignedStaffId\"";
    public const string RenewalTaskBlockedReason = "\"BlockedReason\"";
    public const string RenewalTaskBlockedSince = "\"BlockedSince\"";
    public const string RenewalTaskCompletedAt = "\"CompletedAt\"";
    public const string RenewalTaskServiceFeeAmount = "\"ServiceFeeAmount\"";
    public const string RenewalTaskCreatedAt = "\"CreatedAt\"";
    public const string RenewalTaskUpdatedAt = "\"UpdatedAt\"";
    public const string RenewalTaskIsDeleted = "\"IsDeleted\"";

    // renewal_step_logs
    public const string RenewalStepLogId = "\"Id\"";
    public const string RenewalStepLogStepName = "\"StepName\"";
    public const string RenewalStepLogCompletedBy = "\"CompletedBy\"";
    public const string RenewalStepLogCompletedAt = "\"CompletedAt\"";
    public const string RenewalStepLogReferenceNumber = "\"ReferenceNumber\"";
    public const string RenewalStepLogProofFileUrl = "\"ProofFileUrl\"";
    public const string RenewalStepLogRenewalTaskId = "\"RenewalTaskId\"";
    public const string RenewalStepLogIsDeleted = "\"IsDeleted\"";

    // renewal_task_histories
    public const string RenewalTaskHistoryId = "\"Id\"";
    public const string RenewalTaskHistoryStatus = "\"Status\"";
    public const string RenewalTaskHistoryChangedBy = "\"ChangedBy\"";
    public const string RenewalTaskHistoryChangedAt = "\"ChangedAt\"";
    public const string RenewalTaskHistoryNote = "\"Note\"";
    public const string RenewalTaskHistoryRenewalTaskId = "\"RenewalTaskId\"";
    public const string RenewalTaskHistoryIsDeleted = "\"IsDeleted\"";

    // notifications
    public const string NotificationId = "\"Id\"";
    public const string NotificationTitle = "\"Title\"";
    public const string NotificationMessage = "\"Message\"";
    public const string NotificationIsRead = "\"IsRead\"";
    public const string NotificationReadAt = "\"ReadAt\"";
    public const string NotificationCreatedAt = "\"CreatedAt\"";
    public const string NotificationUserId = "\"UserId\"";
    public const string NotificationIsDeleted = "\"IsDeleted\"";

    // service_requests
    public const string ServiceRequestId = "\"Id\"";
    public const string ServiceRequestClientCompanyId = "\"ClientCompanyId\"";
    public const string ServiceRequestDocumentId = "\"DocumentId\"";
    public const string ServiceRequestStatus = "\"Status\"";
    public const string ServiceRequestType = "\"Type\"";
    public const string ServiceRequestEmployeeId = "\"EmployeeId\"";
    public const string ServiceRequestEntityId = "\"EntityId\"";
    public const string ServiceRequestDescription = "\"Description\"";
    public const string ServiceRequestRejectionReason = "\"RejectionReason\"";
    public const string ServiceRequestConvertedAt = "\"ConvertedAt\"";
    public const string ServiceRequestRejectedAt = "\"RejectedAt\"";
    public const string ServiceRequestConvertedRenewalTaskId = "\"ConvertedRenewalTaskId\"";
    public const string ServiceRequestCreatedAt = "\"CreatedAt\"";
    public const string ServiceRequestUpdatedAt = "\"UpdatedAt\"";
    public const string ServiceRequestIsDeleted = "\"IsDeleted\"";

    // service_fee_invoices
    public const string ServiceFeeInvoiceId = "\"Id\"";
    public const string ServiceFeeInvoiceClientCompanyId = "\"ClientCompanyId\"";
    public const string ServiceFeeInvoiceRenewalTaskId = "\"RenewalTaskId\"";
    public const string ServiceFeeInvoiceInvoiceNumber = "\"InvoiceNumber\"";
    public const string ServiceFeeInvoiceInvoiceDate = "\"InvoiceDate\"";
    public const string ServiceFeeInvoiceDueDate = "\"DueDate\"";
    public const string ServiceFeeInvoiceAmount = "\"Amount\"";
    public const string ServiceFeeInvoiceCurrency = "\"Currency\"";
    public const string ServiceFeeInvoiceStatus = "\"Status\"";
    public const string ServiceFeeInvoiceDescription = "\"Description\"";
    public const string ServiceFeeInvoicePaidAt = "\"PaidAt\"";
    public const string ServiceFeeInvoiceVoidedAt = "\"VoidedAt\"";
    public const string ServiceFeeInvoiceVoidReason = "\"VoidReason\"";
    public const string ServiceFeeInvoiceCreatedAt = "\"CreatedAt\"";
    public const string ServiceFeeInvoiceUpdatedAt = "\"UpdatedAt\"";
    public const string ServiceFeeInvoiceIsDeleted = "\"IsDeleted\"";

    // retainer_invoices
    public const string RetainerInvoiceId = "\"Id\"";
    public const string RetainerInvoiceClientCompanyId = "\"ClientCompanyId\"";
    public const string RetainerInvoiceServiceContractId = "\"ServiceContractId\"";
    public const string RetainerInvoiceInvoiceNumber = "\"InvoiceNumber\"";
    public const string RetainerInvoiceInvoiceDate = "\"InvoiceDate\"";
    public const string RetainerInvoiceDueDate = "\"DueDate\"";
    public const string RetainerInvoicePeriodStart = "\"PeriodStart\"";
    public const string RetainerInvoicePeriodEnd = "\"PeriodEnd\"";
    public const string RetainerInvoiceAmount = "\"Amount\"";
    public const string RetainerInvoiceCurrency = "\"Currency\"";
    public const string RetainerInvoiceStatus = "\"Status\"";
    public const string RetainerInvoiceNotes = "\"Notes\"";
    public const string RetainerInvoicePaidAt = "\"PaidAt\"";
    public const string RetainerInvoiceVoidedAt = "\"VoidedAt\"";
    public const string RetainerInvoiceVoidReason = "\"VoidReason\"";
    public const string RetainerInvoiceCreatedAt = "\"CreatedAt\"";
    public const string RetainerInvoiceUpdatedAt = "\"UpdatedAt\"";
    public const string RetainerInvoiceIsDeleted = "\"IsDeleted\"";

    // payment_orders
    public const string PaymentOrderId = "\"Id\"";
    public const string PaymentOrderInvoiceType = "\"InvoiceType\"";
    public const string PaymentOrderRetainerInvoiceId = "\"RetainerInvoiceId\"";
    public const string PaymentOrderServiceFeeInvoiceId = "\"ServiceFeeInvoiceId\"";
    public const string PaymentOrderClientCompanyId = "\"ClientCompanyId\"";
    public const string PaymentOrderAmount = "\"Amount\"";
    public const string PaymentOrderCurrency = "\"Currency\"";
    public const string PaymentOrderStatus = "\"Status\"";
    public const string PaymentOrderRazorpayOrderId = "\"RazorpayOrderId\"";
    public const string PaymentOrderRazorpayPaymentId = "\"RazorpayPaymentId\"";
    public const string PaymentOrderPaidAt = "\"PaidAt\"";
    public const string PaymentOrderFailedAt = "\"FailedAt\"";
    public const string PaymentOrderCancelledAt = "\"CancelledAt\"";
    public const string PaymentOrderCapturedAmount = "\"CapturedAmount\"";
    public const string PaymentOrderCapturedCurrency = "\"CapturedCurrency\"";
    public const string PaymentOrderReviewReason = "\"ReviewReason\"";
    public const string PaymentOrderNotes = "\"Notes\"";
    public const string PaymentOrderCreatedAt = "\"CreatedAt\"";
    public const string PaymentOrderUpdatedAt = "\"UpdatedAt\"";
    public const string PaymentOrderIsDeleted = "\"IsDeleted\"";

    // gov_fee_disbursements
    public const string GovFeeDisbursementId = "\"Id\"";
    public const string GovFeeDisbursementClientCompanyId = "\"ClientCompanyId\"";
    public const string GovFeeDisbursementRenewalTaskId = "\"RenewalTaskId\"";
    public const string GovFeeDisbursementFeeDescription = "\"FeeDescription\"";
    public const string GovFeeDisbursementGovernmentReference = "\"GovernmentReference\"";
    public const string GovFeeDisbursementAmount = "\"Amount\"";
    public const string GovFeeDisbursementCurrency = "\"Currency\"";
    public const string GovFeeDisbursementStatus = "\"Status\"";
    public const string GovFeeDisbursementPaidByFirmAt = "\"PaidByFirmAt\"";
    public const string GovFeeDisbursementInvoicedAt = "\"InvoicedAt\"";
    public const string GovFeeDisbursementReimbursedAt = "\"ReimbursedAt\"";
    public const string GovFeeDisbursementNotes = "\"Notes\"";
    public const string GovFeeDisbursementCreatedAt = "\"CreatedAt\"";
    public const string GovFeeDisbursementUpdatedAt = "\"UpdatedAt\"";
    public const string GovFeeDisbursementIsDeleted = "\"IsDeleted\"";

    // service_contracts
    public const string ServiceContractId = "\"Id\"";
    public const string ServiceContractContractNumber = "\"ContractNumber\"";
}