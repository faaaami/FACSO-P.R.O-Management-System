import apiClient from './axios'

/**
 * Service jobs (any catalogue service performed for a customer).
 *
 * The same resource is exposed per role, with scope enforced by the API:
 *   Admin  /admin/service-jobs   all jobs; create, assign, cancel, reprice
 *   Agent  /agent/service-jobs   jobs assigned to the agent
 *   Client /client/service-jobs  the company's own jobs, read-only
 */
export const JOB_BASE = {
  Admin: '/admin/service-jobs',
  Agent: '/agent/service-jobs',
  Client: '/client/service-jobs',
}

const data = (response) => response.data.data

export const listJobs = (role, params) => apiClient.get(JOB_BASE[role], { params }).then(data)
export const getJob = (role, id) => apiClient.get(`${JOB_BASE[role]}/${id}`).then(data)
export const getJobSummary = (role) => apiClient.get(`${JOB_BASE[role]}/summary`).then(data)

export const createJob = (body) => apiClient.post(JOB_BASE.Admin, body).then(data)
export const assignJob = (id, assignedStaffId) =>
  apiClient.patch(`${JOB_BASE.Admin}/${id}/assign`, { assignedStaffId }).then(data)
export const cancelJob = (id, reason) => apiClient.post(`${JOB_BASE.Admin}/${id}/cancel`, { reason }).then(data)
export const convertRequestToJob = (serviceRequestId, body) =>
  apiClient.post(`${JOB_BASE.Admin}/from-request/${serviceRequestId}`, body).then(data)

export const startJob = (role, id) => apiClient.post(`${JOB_BASE[role]}/${id}/start`).then(data)
export const holdJob = (role, id, reason) => apiClient.post(`${JOB_BASE[role]}/${id}/hold`, { reason }).then(data)
export const resumeJob = (role, id) => apiClient.post(`${JOB_BASE[role]}/${id}/resume`).then(data)
export const completeJob = (role, id, body) => apiClient.post(`${JOB_BASE[role]}/${id}/complete`, body).then(data)
export const updateJobDetails = (role, id, body) => apiClient.patch(`${JOB_BASE[role]}/${id}`, body).then(data)
export const updateJobStep = (role, id, stepId, body) =>
  apiClient.patch(`${JOB_BASE[role]}/${id}/steps/${stepId}`, body).then(data)
export const setJobDocument = (role, id, documentId, isReceived) =>
  apiClient.patch(`${JOB_BASE[role]}/${id}/documents/${documentId}`, { isReceived }).then(data)
export const addJobNote = (role, id, note, isInternal) =>
  apiClient.post(`${JOB_BASE[role]}/${id}/notes`, { note, isInternal }).then(data)
export const addJobGovFee = (role, id, body) =>
  apiClient.post(`${JOB_BASE[role]}/${id}/government-fees`, body).then(data)
