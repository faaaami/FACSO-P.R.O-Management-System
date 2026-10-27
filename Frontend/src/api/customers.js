import apiClient from './axios'

const data = (response) => response.data.data

export const createWalkInCustomer = (body) => apiClient.post('/admin/customers/walk-in', body).then(data)
export const lookupCustomers = (q, customerType) =>
  apiClient.get('/admin/customers/lookup', { params: { q, customerType } }).then(data)
export const getCustomerProfile = (id) => apiClient.get(`/admin/customers/${id}/profile`).then(data)
export const updateCustomerProfile = (id, body) => apiClient.put(`/admin/customers/${id}/profile`, body).then(data)
export const listCustomerPersons = (id, personType) =>
  apiClient.get(`/admin/customers/${id}/persons`, { params: { personType } }).then(data)
export const createPerson = (clientEntityId, body) =>
  apiClient.post(`/admin/entities/${clientEntityId}/persons`, body).then(data)
export const updatePerson = (id, body) => apiClient.put(`/admin/persons/${id}`, body).then(data)
