import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  createAdminClient,
  updateAdminClient,
  deleteAdminClient,
  createAdminClientEntity,
  updateAdminClientEntity,
  deleteAdminClientEntity,
  createAdminClientContact,
  updateAdminClientContact,
  deleteAdminClientContact,
  updateAdminClientContactApproval,
} from '../../api/admin/clients'

/**
 * Admin client-company writes: company, entity, contact, and contact approval.
 *
 * These are the ten mutating actions on ClientCompaniesController, all of them
 * `[Authorize(Roles = "Admin")]`. There is no route here for a partial company
 * update or for editing a contact's details, and the comments on each hook say
 * why, because both absences look like oversights in this file otherwise.
 *
 * WHY INVALIDATION IS ENUMERATED PER MUTATION rather than blanket-prefixed on
 * ['admin']. This follows useAdminStaffMutations exactly. The Admin cache holds
 * independently-keyed reads answering different questions, and a write only
 * disturbs the ones whose answers actually changed. A blanket ['admin'] would
 * also refetch the service report, the entity maps and every unrelated Admin
 * list on every contact approval.
 *
 * The keys involved, all of them read from useAdminClients and useAdminEntityMaps:
 *
 *   ['admin','clients', {…}]                 the paginated company list
 *   ['admin','client',  cid, {…}]            one company's detail
 *   ['admin','client',  cid, 'service-report']  its service report
 *   ['admin','client-entities', cid, {…}]    one company's entity list
 *   ['admin','client-entity',  cid, eid, {…}] one entity's detail
 *   ['admin','client-contacts',  cid, {…}]    one company's contact list
 *   ['admin','entity-map','clients']         id -> company name, Admin-wide
 *
 * THE COMPANY MAP IS A COMPANY MAP, NOT AN ENTITY MAP. ['admin','entity-map',
 * 'clients'] is built by useAdminEntityMaps from getAdminClients and maps a
 * clientCompanyId to a companyName; it is the fallback resolver for the Admin
 * tables that carry a bare company id. There is no equivalent map for legal
 * entities — every entity screen reads the name off the entity DTO it already
 * has — so entity writes below invalidate no map at all. Renaming a COMPANY
 * does need the map, exactly as renaming a staff member needs ['admin',
 * 'entity-map','staff'] in useAdminStaffMutations, and for the same reason: a
 * stale entry would show the old name in the company column of another table
 * while this drawer showed the new one.
 *
 * NOTHING IS SEEDED WITH setQueryData. Every hook below invalidates and lets
 * the list refetch. Contact creation is the reason: it carries an Admin-typed
 * password in its request, so a `setQueryData` that echoed the variables would
 * park a plaintext password in a 30-second-stale store readable by any later
 * render under that key, and it would survive a dialog close. The password is
 * never a response field here, so nothing from this route can reach the cache
 * even accidentally.
 */

/** The company map: id -> companyName, used to label company ids across Admin. */
const CLIENT_NAME_MAP_KEY = ['admin', 'entity-map', 'clients']

/** Every page of the company list, regardless of its filter object. */
const COMPANY_LIST_KEYS = [['admin', 'clients']]

/**
 * Matches ONE company's detail query and nothing else.
 *
 * The detail key and the service-report key share the three-element prefix
 * ['admin','client',cid], so a plain prefix invalidation would also refetch the
 * report. That is pure waste for the writes that follow — approving a contact
 * changes no task, invoice or total in that report — and the report has a
 * 60-second staleTime precisely so it is not refetched constantly.
 *
 * The two are told apart by their fourth element: the detail query always
 * carries its filter object `{ includeDeleted }`, the report always carries the
 * string 'service-report'. The check is on the type, not on the value, so it
 * cannot be fooled by a change to the report's key.
 */
function isCompanyDetail(clientCompanyId) {
  return (query) => {
    const key = query.queryKey
    return (
      key[0] === 'admin' &&
      key[1] === 'client' &&
      key[2] === clientCompanyId &&
      typeof key[3] === 'object' &&
      key[3] !== null
    )
  }
}

function invalidateKeys(queryClient, queryKeys) {
  for (const queryKey of queryKeys) {
    queryClient.invalidateQueries({ queryKey })
  }
}

/**
 * POST /api/v1/admin/clients
 *
 * The list AND the company map are invalidated together even though this only
 * creates a row. A new company is not yet referenced by any table, so nothing
 * can mislabel it — but the map is built by paging the company list, so leaving
 * it alone would keep a company that now exists absent from the resolver, and
 * the first page drawn afterwards would show a short-id fallback for a company
 * the Admin can plainly see in the list next to it. The cost is one refetch of a
 * 5-minute-stale map on an infrequent action.
 */
export function useCreateAdminClient() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload) => createAdminClient(payload),
    onSuccess: () => {
      invalidateKeys(queryClient, [...COMPANY_LIST_KEYS, CLIENT_NAME_MAP_KEY])
    },
  })
}

/**
 * PATCH /api/v1/admin/clients/{id}
 *
 * A rename has to reach the company map, for the reason in the file header: the
 * map is how other Admin tables label this company, and a stale entry would
 * show the previous name elsewhere while this drawer showed the new one.
 *
 * The payload is deliberately sparse — see the wrapper for why omitting a field
 * is the only safe way to express "leave this alone" on this route, and why a
 * blank is never forwarded.
 */
export function useUpdateAdminClient() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, ...payload }) => updateAdminClient(clientId, payload),
    onSuccess: (_data, { clientId }) => {
      invalidateKeys(queryClient, [...COMPANY_LIST_KEYS, CLIENT_NAME_MAP_KEY])
      queryClient.invalidateQueries({ queryKey: ['admin', 'client', clientId], predicate: isCompanyDetail(clientId) })
    },
  })
}

/**
 * DELETE /api/v1/admin/clients/{id} — soft delete, one-way, no restore route.
 *
 * The company detail is invalidated rather than removed on purpose. Soft-deleting
 * flips isDeleted on a row the detail GET still returns (it takes
 * includeDeleted), so a refetch produces a real, truthful record showing the
 * company as deleted. Removing the entry instead would leave the drawer showing
 * the pre-delete record with no error, which is the state the confirmation is
 * supposed to prevent.
 *
 * The nested entity and contact lists are NOT invalidated, because refetching
 * them would now 404: their parent lookup hardcodes !IsDeleted. The drawer
 * already warns about exactly this on a deleted company, and those sections
 * already own their own error state, so they surface the failure on their own
 * without being prodded.
 */
export function useDeleteAdminClient() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (clientId) => deleteAdminClient(clientId),
    onSuccess: (_data, clientId) => {
      invalidateKeys(queryClient, [...COMPANY_LIST_KEYS, CLIENT_NAME_MAP_KEY])
      queryClient.invalidateQueries({ queryKey: ['admin', 'client', clientId], predicate: isCompanyDetail(clientId) })
    },
  })
}

/**
 * POST /api/v1/admin/clients/{clientId}/entities
 *
 * The entity list only. No company map, because no entity map exists: every
 * entity screen reads its name from the entity DTO, and a brand-new entity is
 * referenced by no task, document or contract yet, so nothing could mislabel it.
 */
export function useCreateAdminClientEntity() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, ...payload }) => createAdminClientEntity(clientId, payload),
    onSuccess: (_data, { clientId }) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-entities', clientId] })
    },
  })
}

/**
 * PATCH /api/v1/admin/clients/{clientId}/entities/{entityId}
 *
 * Both the list and the entity detail, because an edit made from the detail
 * panel changes the row that panel is displaying. The payload is a full
 * replacement — the wrapper sends all four fields and sends null to clear the
 * optional ones, because on this route null clears and omission does not.
 */
export function useUpdateAdminClientEntity() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, entityId, ...payload }) =>
      updateAdminClientEntity(clientId, entityId, payload),
    onSuccess: (_data, { clientId, entityId }) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-entities', clientId] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-entity', clientId, entityId] })
    },
  })
}

/**
 * DELETE /api/v1/admin/clients/{clientId}/entities/{entityId}
 *
 * The entity detail entry is REMOVED rather than invalidated, and the list is
 * invalidated, which is the opposite of the company delete above for a concrete
 * reason: the entity detail GET is called with includeDeleted = false, so after a
 * soft delete it genuinely 404s. Invalidating it would park a permanent error in
 * the cache behind a panel the user has just navigated away from. The list
 * refetch is what the user actually needs — the row leaves it.
 *
 * The document list that shares the panel is left alone: it is keyed separately
 * under the entity id and the deleted entity's documents are simply no longer
 * reachable through the UI, which is the same truth the confirmation states.
 */
export function useDeleteAdminClientEntity() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, entityId }) => deleteAdminClientEntity(clientId, entityId),
    onSuccess: (_data, { clientId, entityId }) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-entities', clientId] })
      queryClient.removeQueries({ queryKey: ['admin', 'client-entity', clientId, entityId] })
    },
  })
}

/**
 * POST /api/v1/admin/clients/{clientId}/contacts
 *
 * The contacts list only, and nothing is written into the cache.
 *
 * THE RESPONSE IS NEVER CACHED, AND NEITHER IS THE PASSWORD. The request
 * carries an Admin-typed password and the response carries no credential at
 * all, so there is nothing here worth storing and no reason to deviate from
 * invalidating. A `setQueryData` that echoed the mutation variables would put a
 * plaintext password in a store that outlives the dialog, outlives the drawer,
 * and is readable by anything that later renders under that key. The hook
 * invalidates and hands the response back unchanged, and the caller already
 * holds the password because it typed it.
 *
 * The company detail is not invalidated: a new contact changes nothing on the
 * company record, which carries only isActive and isDeleted and is not derived
 * from its contacts.
 */
export function useCreateAdminClientContact() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, ...payload }) => createAdminClientContact(clientId, payload),
    onSuccess: (_data, { clientId }) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-contacts', clientId] })
    },
  })
}

/**
 * PATCH /api/v1/admin/clients/{clientId}/contacts/{contactId}
 *
 * THIS IS A DEACTIVATE TOGGLE, NOT AN EDIT. The handler writes only IsActive;
 * fullName, email and phone are unreachable from Admin and isApproved is not
 * writable on this route. The hook takes `isActive` alone and does not accept a
 * name, an email or a phone, so no caller can build a form that silently loses
 * what it submits. The `role` the validator insists on is added by the wrapper
 * and is inert.
 *
 * The contacts list AND the company detail are both invalidated. The company
 * detail is included because the drawer header renders the company status pill
 * and an onboarding sequence that approves and then deactivates leaves the two
 * panels describing different states until both are refetched.
 */
export function useUpdateAdminClientContact() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, contactId, isActive }) =>
      updateAdminClientContact(clientId, contactId, { isActive }),
    onSuccess: (_data, { clientId }) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-contacts', clientId] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'client', clientId], predicate: isCompanyDetail(clientId) })
    },
  })
}

/**
 * DELETE /api/v1/admin/clients/{clientId}/contacts/{contactId}
 *
 * Soft delete, one-way, no restore. Unlike the deactivate toggle above this also
 * clears the account's IsActive, so a deleted contact is both gone from the list
 * and refused at login; the contacts list refetch is what reflects that.
 */
export function useDeleteAdminClientContact() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, contactId }) => deleteAdminClientContact(clientId, contactId),
    onSuccess: (_data, { clientId }) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-contacts', clientId] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'client', clientId], predicate: isCompanyDetail(clientId) })
    },
  })
}

/**
 * PATCH /api/v1/admin/clients/{clientId}/contacts/{contactId}/approval
 *
 * `approved` is a required boolean, not an optional one: a null makes the
 * controller throw before it dispatches, so the wrapper never forwards it.
 *
 * APPROVAL ENABLES THE ACCOUNT, and the UI must not describe it otherwise. The
 * contact is already EmailVerified — creation is what verifies an email — so
 * approval is not a verification step and the button must not be worded as one.
 * Approve sets IsApproved AND IsActive together, so approving also silently
 * re-enables a contact the Admin had deactivated, and it notifies the contact.
 * Reject clears IsApproved, leaves IsActive alone, and notifies.
 *
 * Both outcomes disturb the same two reads, so there is one hook rather than two
 * that would invalidate identically. The contacts list is what shows the new
 * status; the company detail is refetched so the drawer header and the list
 * cannot disagree while the Admin is looking at them.
 */
export function useUpdateAdminClientContactApproval() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ clientId, contactId, approved }) =>
      updateAdminClientContactApproval(clientId, contactId, approved),
    onSuccess: (_data, { clientId }) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'client-contacts', clientId] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'client', clientId], predicate: isCompanyDetail(clientId) })
    },
  })
}
