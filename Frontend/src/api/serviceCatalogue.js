import apiClient from './axios'

const data = (response) => response.data.data

/** Admin: full catalogue with pricing. */
export const listServices = (params) => apiClient.get('/admin/services', { params }).then(data)
export const getService = (id) => apiClient.get(`/admin/services/${id}`).then(data)
export const createService = (body) => apiClient.post('/admin/services', body).then(data)
export const updateService = (id, body) => apiClient.put(`/admin/services/${id}`, body).then(data)
export const setServiceActive = (id, isActive) =>
  apiClient.patch(`/admin/services/${id}/active`, { isActive }).then(data)

/** Any signed-in role: active services without internal pricing. */
export const listPublicServices = (params) => apiClient.get('/services', { params }).then(data)

/** Subscription pricing per contract. */
export const listContractPrices = (contractId) =>
  apiClient.get(`/admin/contracts/${contractId}/service-prices`).then(data)
export const setContractPrice = (contractId, serviceId, body) =>
  apiClient.put(`/admin/contracts/${contractId}/service-prices/${serviceId}`, body).then(data)
export const deleteContractPrice = (contractId, serviceId) =>
  apiClient.delete(`/admin/contracts/${contractId}/service-prices/${serviceId}`).then(data)
