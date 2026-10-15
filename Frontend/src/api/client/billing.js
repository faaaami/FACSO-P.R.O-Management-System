import apiClient from '../axios'

/**
 * Retrieves the client's retainer invoices.
 * GET /api/v1/client/retainer-invoices
 */
export async function getRetainerInvoices(params = {}) {
  const response = await apiClient.get('/client/retainer-invoices', { params })
  return response.data.data
}

/**
 * Retrieves a single client retainer invoice by id.
 * GET /api/v1/client/retainer-invoices/{id}
 */
export async function getRetainerInvoice(id) {
  const response = await apiClient.get(`/client/retainer-invoices/${id}`)
  return response.data.data
}

/**
 * Downloads a client retainer invoice PDF.
 * GET /api/v1/client/retainer-invoices/{id}/pdf
 *
 * NOTE: returns the raw Axios response (this endpoint serves raw
 * application/pdf bytes, NOT the ApiResponse envelope). The Blob is available
 * at response.data; the eventual UI layer must create a download URL from it.
 */
export async function getRetainerInvoicePdf(id) {
  return apiClient.get(`/client/retainer-invoices/${id}/pdf`, {
    responseType: 'blob',
  })
}

/**
 * Retrieves the client's service fee invoices.
 * GET /api/v1/client/service-fee-invoices
 */
export async function getServiceFeeInvoices(params = {}) {
  const response = await apiClient.get('/client/service-fee-invoices', { params })
  return response.data.data
}

/**
 * Retrieves a single client service fee invoice by id.
 * GET /api/v1/client/service-fee-invoices/{id}
 */
export async function getServiceFeeInvoice(id) {
  const response = await apiClient.get(`/client/service-fee-invoices/${id}`)
  return response.data.data
}

/**
 * Downloads a client service fee invoice PDF.
 * GET /api/v1/client/service-fee-invoices/{id}/pdf
 *
 * NOTE: returns the raw Axios response (this endpoint serves raw
 * application/pdf bytes, NOT the ApiResponse envelope). The Blob is available
 * at response.data; the eventual UI layer must create a download URL from it.
 */
export async function getServiceFeeInvoicePdf(id) {
  return apiClient.get(`/client/service-fee-invoices/${id}/pdf`, {
    responseType: 'blob',
  })
}

/**
 * Retrieves the client's government fee disbursements.
 * GET /api/v1/client/gov-fee-disbursements
 */
export async function getGovFeeDisbursements(params = {}) {
  const response = await apiClient.get('/client/gov-fee-disbursements', { params })
  return response.data.data
}

/**
 * Retrieves a single client government fee disbursement by id.
 * GET /api/v1/client/gov-fee-disbursements/{id}
 */
export async function getGovFeeDisbursement(id) {
  const response = await apiClient.get(`/client/gov-fee-disbursements/${id}`)
  return response.data.data
}

/**
 * Creates a client payment order for a pending invoice.
 * POST /api/v1/client/payments/create-order
 *
 * Payload is limited to { invoiceType, invoiceId } — company, amount,
 * currency and Razorpay fields are derived server-side.
 */
export async function createClientPaymentOrder(payload) {
  const response = await apiClient.post('/client/payments/create-order', payload)
  return response.data.data
}