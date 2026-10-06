import apiClient from '../axios'

/**
 * Admin client-company endpoints.
 * All routes are `[Authorize(Roles = "Admin")]` under /api/v1/admin/clients.
 * Admin sees every company; there is no per-agent company assignment.
 *
 * Verified pagination shape: { items, page, pageSize, totalCount }
 *
 * READS AND WRITES ARE BOTH WRAPPED. The Phase 2 read-only constraint is gone:
 * the controller's ten mutating actions now have wrappers below, and the Admin
 * onboarding UI (company -> entity -> contact -> approval) is built on them.
 * Nothing here is speculative — every wrapper maps to a controller action.
 *
 * THE ONE DELIBERATE GAP. GET /api/v1/admin/clients/{id}/service-report/pdf has
 * no wrapper, for the reason the read wrapper above sets out: it renders the same
 * all-time report, so a download would put on a page the user can keep the exact
 * figures the UI withholds (no currency, no honoured date range).
 *
 * CONTRACTS ARE NOT HERE. They are not an /admin/clients sub-resource: the
 * verified route is the bare GET /api/v1/clients/{id}/contracts, which lives in
 * api/admin/contracts.js. There is no /api/v1/admin/clients/{id}/contracts.
 *
 * THE ROUTE IDS ARE AUTHORITATIVE AND THE BODY IS NOT. Every mutating action
 * binds its ids from the route and overwrites whatever the body carried
 * (`command with { Id = id }`, `request with { ClientCompanyId = clientId }`,
 * and for entity/contact writes both ids). The wrappers below therefore keep
 * company and entity ids in the PATH ONLY and never put them in the payload: a
 * body that disagreed with the route could not redirect the write, but sending
 * one anyway would misdescribe the contract and invite a future reader to trust
 * the wrong half of it.
 */

/**
 * GET /api/v1/admin/clients
 * Query: page (1), pageSize (20), search (string?), includeDeleted (false)
 * `search` matches company name OR trade licence number only — it does not
 * search phone, email or emirate, so the UI must not label it as global search.
 * Ordering is fixed at created_at DESC, id ASC; there is no sort parameter.
 * Returns { items, page, pageSize, totalCount }
 *   item: ClientCompanyListItemDto { id, companyName, tradeLicenseNumber?, phone?,
 *     email?, emirate?, isActive, isDeleted, createdAt }
 * NOTE: the list item carries no address and no updatedAt — those exist only on
 * the detail DTO, so the list must not render an address column.
 */
export async function getAdminClients(params = {}) {
  const response = await apiClient.get('/admin/clients', { params })
  return response.data.data
}

/**
 * GET /api/v1/admin/clients/{id}
 * Query: includeDeleted (false)
 * Returns GetClientCompanyByIdResponseDto — richer than the list DTO.
 *   { id, companyName, tradeLicenseNumber?, phone?, email?, address?, emirate?,
 *     isActive, isDeleted, createdAt, updatedAt }
 * 404 => CLIENT_COMPANY_NOT_FOUND
 * NOTE: there is no deletedAt on this DTO, so a soft-deleted company can only be
 * identified by isDeleted === true.
 */
export async function getAdminClientById(clientId, params = {}) {
  const response = await apiClient.get(`/admin/clients/${clientId}`, { params })
  return response.data.data
}

/**
 * GET /api/v1/admin/clients/{clientId}/entities
 * Query: page (1), pageSize (20), search (string?), includeDeleted (false)
 * `search` matches entity name OR trade licence number.
 * Returns { items, page, pageSize, totalCount }
 *   item: GetClientEntityListItemDto { id, clientCompanyId, entityName,
 *     tradeLicenseNumber?, emirate?, isActive, isDeleted, createdAt }
 * The entity DTO has NO address, email, phone, establishment card or licence
 * dates — do not render fields that do not exist.
 * 404s when the parent company is soft-deleted even if includeDeleted=true,
 * because the parent existence check hardcodes !IsDeleted.
 */
export async function getAdminClientEntities(clientId, params = {}) {
  const response = await apiClient.get(`/admin/clients/${clientId}/entities`, {
    params,
  })
  return response.data.data
}

/**
 * GET /api/v1/admin/clients/{clientId}/entities/{entityId}
 * Query: includeDeleted (false)
 * Returns GetClientEntityByIdResponseDto — the list item plus updatedAt.
 *   { id, clientCompanyId, entityName, tradeLicenseNumber?, emirate?, isActive,
 *     isDeleted, createdAt, updatedAt }
 * 404 => CLIENT_ENTITY_NOT_FOUND. Scoped by BOTH ids, so an entity belonging to
 * another company is a 404 rather than a cross-tenant leak.
 * NOTE: there is no /api/v1/admin/entities/{entityId} route.
 */
export async function getAdminClientEntityById(clientId, entityId, params = {}) {
  const response = await apiClient.get(`/admin/clients/${clientId}/entities/${entityId}`, {
    params,
  })
  return response.data.data
}

/**
 * GET /api/v1/admin/clients/{clientId}/contacts
 * Query: page (1), pageSize (20), search (string?), includeDeleted (false)
 * `search` matches full name OR email.
 * Returns { items, page, pageSize, totalCount }
 *   item: GetClientContactListItemDto { id, clientCompanyId, fullName, email,
 *     phone?, role, isActive, isApproved, isDeleted, createdAt }
 *
 * THERE IS NO `role` QUERY PARAMETER. An earlier version of this file documented
 * one; the controller accepts only page/pageSize/search/includeDeleted and
 * hardcodes the role filter to client contacts. Sending `role` is silently
 * ignored, so it must never be passed or documented.
 *
 * The contact DTO has no position and no primary-contact flag. `role` is a
 * free-form string, not an enum. `isApproved` is a real, distinct state and is
 * worth showing; do not infer an approved/pending state for the COMPANY, which
 * only has isActive and isDeleted.
 * 404s when the parent company is soft-deleted, same as /entities.
 */
export async function getAdminClientContacts(clientId, params = {}) {
  const response = await apiClient.get(`/admin/clients/${clientId}/contacts`, {
    params,
  })
  return response.data.data
}

/**
 * GET /api/v1/admin/clients/{id}/service-report
 *
 * NO DATE RANGE IS SENT, and the wrapper takes no params at all. The action
 * accepts optional `from` and `to`, and the validator even caps `to` at
 * `from + 365 days`, but ClientServiceReportReadRepository never references
 * either value: all three queries filter on client id and is_deleted only, and
 * the parameter object it passes holds only ClientId. A range would therefore
 * be accepted, silently ignored, and then displayed as though it had filtered
 * something. Omitting it keeps the request and the resulting claim in agreement,
 * and the report is presented as all-time.
 *
 * Response ClientServiceReportDto { client, tasks, invoices, summary }:
 *   client:   { id, companyName } � companyName is the literal string "Unknown"
 *             when the company row is missing, so it is not used as a label
 *   task:     { id, documentNumber?, status, completedAt?, assignedStaffName? }
 *   invoice:  { id, invoiceNumber, type, amount, status, createdAt }
 *   summary:  { totalTasks, completedTasks, pendingTasks, totalInvoiced,
 *               totalPaid, totalInvoices }
 *
 * The invoice list and the two decimal summary totals are NOT surfaced by the
 * hook. `amount` is the only money field on the invoice row and the DTO carries
 * no currency, while totalInvoiced and totalPaid are summed across retainer and
 * service-fee rows with no currency grouping, so none of the three can be shown
 * as money. summary.totalInvoices is a count and IS surfaced.
 *
 * summary.completedTasks and summary.pendingTasks are NOT the field names they
 * appear to be. completedTasks counts rows whose status is exactly "Updated",
 * and pendingTasks is simply every other row � so Approved and Blocked tasks are
 * both counted as "pending". The hook renames them on the way out rather than
 * passing through labels that would misdescribe the data.
 *
 * There is no sibling PDF wrapper. /service-report/pdf renders the same report
 * from the same repository, so it inherits the ignored date range and the
 * uncurrency'd totals; a download of it would put exactly the figures this
 * section withholds onto a page the user can keep.
 */
export async function getAdminClientServiceReport(clientId) {
  const response = await apiClient.get(`/admin/clients/${clientId}/service-report`)
  return response.data.data
}

/* ---------------------------------------------------------------------------
 * Company writes
 * ------------------------------------------------------------------------- */

/**
 * POST /api/v1/admin/clients
 * Body: CreateClientCompanyCommand { companyName, tradeLicenseNumber?, phone?,
 *   email?, address?, emirate? }
 *
 * `companyName` is the only required field. The optional strings are OMITTED
 * when blank rather than sent as "", because "" is a real value to the handler:
 * it would be trimmed and stored as an empty string, which is not the same thing
 * as "not supplied" and would render as a blank value rather than as absent.
 *
 * The backend applies no uniqueness rule here, so two companies may share a name
 * and two may share a trade licence. There is nothing in the wrapper to prevent
 * that and nothing to detect it — the caller must not disable its own submit
 * button and must not claim duplicates are rejected.
 *
 * There is no `isActive` on the create payload: the record does not carry one
 * from the caller. Returns CreateClientCompanyResponseDto, 201.
 */
export async function createAdminClient(payload) {
  const body = { companyName: payload.companyName }
  if (payload.tradeLicenseNumber) body.tradeLicenseNumber = payload.tradeLicenseNumber
  if (payload.phone) body.phone = payload.phone
  if (payload.email) body.email = payload.email
  if (payload.address) body.address = payload.address
  if (payload.emirate) body.emirate = payload.emirate

  const response = await apiClient.post('/admin/clients', body)
  return response.data.data
}

/**
 * PATCH /api/v1/admin/clients/{id}
 * Body: UpdateClientCompanyCommand { companyName?, tradeLicenseNumber?, phone?,
 *   email?, address?, emirate?, isActive? }  — the id comes from the route.
 *
 * THIS IS A TRUE PARTIAL UPDATE, and the difference from the entity route is the
 * single most important thing in this file. The handler assigns each field only
 * `if (request.X is not null)`, so an omitted or null field means "leave the
 * stored value alone". Two consequences the caller must respect:
 *
 *   1. A field can never be CLEARED. There is no value that means "empty":
 *      null is taken as "unchanged". Blanking a field in the UI and sending null
 *      silently does nothing, so the wrapper must not offer one.
 *   2. Sending "" is NOT the same as omitting it. "" is not null, so it passes
 *      the validator (which only applies MaximumLength, guarded by
 *      `When(x => x is not null)` — there is no NotEmpty on the update) and the
 *      handler stores an empty string. An edit that sends "" therefore quietly
 *      empties a field that looked optional, and the name has no server-side
 *      guard at all. The wrapper therefore omits blanks rather than forwarding
 *      them.
 *
 * `isActive` is guarded by HasValue, so false is a real value and IS forwarded.
 * Returns UpdateClientCompanyResponseDto, 200. Note that response omits
 * `isDeleted` and `createdAt`, which the detail GET does carry.
 */
export async function updateAdminClient(clientId, payload) {
  const body = {}
  if (payload.companyName) body.companyName = payload.companyName
  if (payload.tradeLicenseNumber) body.tradeLicenseNumber = payload.tradeLicenseNumber
  if (payload.phone) body.phone = payload.phone
  if (payload.email) body.email = payload.email
  if (payload.address) body.address = payload.address
  if (payload.emirate) body.emirate = payload.emirate
  if (typeof payload.isActive === 'boolean') body.isActive = payload.isActive

  const response = await apiClient.patch(`/admin/clients/${clientId}`, body)
  return response.data.data
}

/**
 * DELETE /api/v1/admin/clients/{id} — NO REQUEST BODY.
 *
 * SOFT DELETE, and one-way: the handler sets IsDeleted and there is no restore
 * action anywhere in the controller. It does NOT cascade — entities and
 * contacts are left in place, but every nested read is scoped to a parent that
 * is not deleted, so they become unreachable through this company while their
 * rows survive. The confirmation dialog must say that, not "permanently
 * deletes".
 *
 * The controller documents a 409, raised when the company is already deleted.
 * 404 for an unknown id.
 */
export async function deleteAdminClient(clientId) {
  const response = await apiClient.delete(`/admin/clients/${clientId}`)
  return response.data.data
}

/* ---------------------------------------------------------------------------
 * Entity writes
 * ------------------------------------------------------------------------- */

/**
 * POST /api/v1/admin/clients/{clientId}/entities
 * Body: CreateClientEntityCommand { entityName, tradeLicenseNumber?, emirate? }
 *   — clientCompanyId comes from the route.
 *
 * `entityName` is required (NotEmpty, max 200). Optional strings are omitted
 * when blank, as above. 404 when the parent company is missing or soft-deleted.
 * There is no uniqueness rule on entity name or trade licence.
 * Returns CreateClientEntityResponseDto, 201.
 */
export async function createAdminClientEntity(clientId, payload) {
  const body = { entityName: payload.entityName }
  if (payload.tradeLicenseNumber) body.tradeLicenseNumber = payload.tradeLicenseNumber
  if (payload.emirate) body.emirate = payload.emirate

  const response = await apiClient.post(`/admin/clients/${clientId}/entities`, body)
  return response.data.data
}

/**
 * PATCH /api/v1/admin/clients/{clientId}/entities/{entityId}
 * Body: UpdateClientEntityCommand { entityName, tradeLicenseNumber?, emirate?,
 *   isActive }  — both ids come from the route.
 *
 * THIS IS A FULL REPLACEMENT, the opposite of the company route. The handler
 * assigns every field unconditionally, so the payload is always complete:
 *
 *   entityName         request.EntityName.Trim()  (validator requires it)
 *   tradeLicenseNumber request.TradeLicenseNumber?.Trim()
 *   emirate            request.Emirate?.Trim()
 *   isActive           request.IsActive
 *
 * The practical consequence is that null DOES clear `tradeLicenseNumber` and
 * `emirate` here, and it is sent as explicit null rather than omitted — which
 * would leave the stored licence or emirate in place and make the UI claim a
 * change that never happened. Omitting them would be a silent no-op, so this
 * wrapper always sends all four fields.
 *
 * `isActive` is a non-nullable bool, so it is always present. A partial payload
 * is not merely ignored here, it is rejected by the validator.
 */
export async function updateAdminClientEntity(clientId, entityId, payload) {
  const response = await apiClient.patch(
    `/admin/clients/${clientId}/entities/${entityId}`,
    {
      entityName: payload.entityName,
      tradeLicenseNumber: payload.tradeLicenseNumber ?? null,
      emirate: payload.emirate ?? null,
      isActive: payload.isActive,
    },
  )
  return response.data.data
}

/**
 * DELETE /api/v1/admin/clients/{clientId}/entities/{entityId} — NO REQUEST BODY.
 *
 * Soft delete, one-way, no restore route. The lookup is scoped by BOTH ids, so
 * an entity belonging to another company is a 404 rather than a cross-tenant
 * write. The parent company must not be soft-deleted.
 */
export async function deleteAdminClientEntity(clientId, entityId) {
  const response = await apiClient.delete(
    `/admin/clients/${clientId}/entities/${entityId}`,
  )
  return response.data.data
}

/* ---------------------------------------------------------------------------
 * Contact writes
 * ------------------------------------------------------------------------- */

/**
 * POST /api/v1/admin/clients/{clientId}/contacts
 * Body: CreateClientContactCommand { fullName, email, phone? }
 *   — clientCompanyId comes from the route.
 *
 * Validation: fullName required max 200, email required + valid + max 255, phone
 * optional max 30. Note the email cap is 255 here against 320 on the company
 * DTO; the form follows the stricter of the two. password is REQUIRED, min 8,
 * max 100.
 *
 * EMAIL UNIQUENESS IS GLOBAL, NOT PER COMPANY. The handler checks the address
 * against every non-deleted User, so a contact whose email matches an existing
 * agent, staff account or other contact is rejected with 409 however unrelated
 * the company. That is a real constraint on the Admin workflow, and it is why
 * this can fail on a field the user cannot see a conflict for.
 *
 * `password` is the Admin's own chosen value and travels one way: it is hashed by
 * the server, never stored or returned in plaintext, never audited, never
 * emailed, and never cached here. The Admin shares it with the client directly.
 *
 * The response is CreateClientContactResponseDto and it carries NO credential:
 *
 *   { id, clientCompanyId, fullName, email, phone, role, isActive, isApproved }
 *
 * `role` is always Client, `isApproved` is always false and the account is
 * created EmailVerified.
 */
export async function createAdminClientContact(clientId, payload) {
  const body = {
    fullName: payload.fullName,
    email: payload.email,
    password: payload.password,
  }
  if (payload.phone) body.phone = payload.phone

  const response = await apiClient.post(`/admin/clients/${clientId}/contacts`, body)
  return response.data.data
}

/**
 * PATCH /api/v1/admin/clients/{clientId}/contacts/{contactId}
 * Body: UpdateClientContactCommand { role, isActive }  — both ids from the route.
 *
 * THIS WRITES ONE FIELD. Despite the four-property DTO, the handler assigns
 * only `contact.IsActive`. FullName, Email and Phone are unreachable from Admin,
 * and IsApproved is not writable here either — it belongs to the approval route.
 * So this is a deactivate/reactivate toggle wearing the shape of an update, and
 * no "edit contact" form may be built on top of it, because three of its four
 * inputs would be silently discarded.
 *
 * `role` is still REQUIRED, and this is the one genuinely odd part of the
 * contract. The validator demands `Equal(UserRole.Client)` with the message "A
 * client contact must have the Client role.", but the handler never reads
 * `request.Role` — the record it loads is already filtered to Role == Client.
 * Omitting it therefore fails validation for no behavioural reason, so the
 * constant is sent explicitly. It is inert, and the UI must not offer a role
 * control: a contact can only ever be a Client.
 *
 * The lookup requires Role == Client, so an id belonging to an agent account or
 * to another company is a 404 rather than a cross-tenant write.
 */
export async function updateAdminClientContact(clientId, contactId, payload) {
  const response = await apiClient.patch(
    `/admin/clients/${clientId}/contacts/${contactId}`,
    {
      // Inert but validator-required. UserRole is serialized as a string
      // project-wide, so "Client" is the wire form of the enum member.
      role: payload.role ?? 'Client',
      isActive: payload.isActive,
    },
  )
  return response.data.data
}

/**
 * DELETE /api/v1/admin/clients/{clientId}/contacts/{contactId} — NO REQUEST BODY.
 *
 * Soft delete, one-way, no restore route. Unlike the company delete this DOES
 * also set IsActive = false on the underlying user, so a deleted contact is both
 * hidden from the list and refused at login. Scoped by both ids and by
 * Role == Client.
 */
export async function deleteAdminClientContact(clientId, contactId) {
  const response = await apiClient.delete(
    `/admin/clients/${clientId}/contacts/${contactId}`,
  )
  return response.data.data
}

/**
 * PATCH /api/v1/admin/clients/{clientId}/contacts/{contactId}/approval
 * Body: ClientApprovalRequest { approved }
 *
 * ONE ROUTE, TWO OUTCOMES. The controller dispatches on the boolean: true sends
 * ApproveClientCommand, false sends RejectClientCommand. They are not the same
 * operation with an inverted flag — see the two effects below — so this is a
 * single wrapper with a required boolean rather than two near-identical ones.
 *
 * `approved` MUST be present. A null makes the controller throw
 * ArgumentException("The 'approved' value is required."), which surfaces as a
 * 500 rather than a validation error, so the wrapper takes a plain boolean and
 * never forwards a nullable.
 *
 * APPROVAL IS NOT VERIFICATION. A contact is already EmailVerified when created;
 * approval is an account-enablement decision made by the Admin. Approve sets
 * IsApproved = true AND IsActive = true together, so approving a contact that
 * was deactivated silently re-enables it. Reject sets IsApproved = false and
 * leaves IsActive as it is, and notifies the contact. Both notify.
 *
 * There is no separate revoke route, and none is needed: the same route with
 * approved = false is the reverse operation.
 */
export async function updateAdminClientContactApproval(clientId, contactId, approved) {
  const response = await apiClient.patch(
    `/admin/clients/${clientId}/contacts/${contactId}/approval`,
    { approved },
  )
  return response.data.data
}
