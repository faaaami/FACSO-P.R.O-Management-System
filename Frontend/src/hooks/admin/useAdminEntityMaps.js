import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getAdminClients } from '../../api/admin/clients'
import { getAdminStaff } from '../../api/admin/staff'

// Several Admin DTOs reference people and companies by GUID only:
//   RenewalTaskListItemDto.assignedStaffId
//   ServiceRequestListItemDto.clientCompanyId
//   RenewalTaskHistoryDto.changedBy
//   RenewalStepLogDto.completedBy
//   GetRetainerInvoiceListItemDto.clientCompanyId
// There is no join anywhere in the Admin API that returns those names, and no
// aggregate lookup endpoint to add. So we build the maps client-side from the
// two Admin list endpoints that do return names, paging through each once and
// caching the result. Anything past the cap degrades to a short id rather than
// rendering a raw GUID.
const LOOKUP_PAGE_SIZE = 100
const MAX_LOOKUP_PAGES = 10 // hard cap: 1000 rows per list

/**
 * Pages through a { items, totalCount } Admin list until it is exhausted or the
 * page cap is hit. Reads totalCount defensively because the Admin endpoints use
 * three different pagination envelopes.
 */
async function fetchAllPages(fetchPage) {
  const all = []
  for (let page = 1; page <= MAX_LOOKUP_PAGES; page += 1) {
    const result = await fetchPage({ page, pageSize: LOOKUP_PAGE_SIZE })
    const items = Array.isArray(result?.items) ? result.items : []
    all.push(...items)
    const totalCount = result?.totalCount ?? 0
    if (items.length === 0 || all.length >= totalCount) {
      break
    }
  }
  return all
}

function shortId(id) {
  return String(id).slice(0, 8)
}

/**
 * Builds id → display-name maps for the GUID-only references used across the
 * Admin tables, and exposes resolvers that degrade to a short-id fallback when a
 * name cannot be resolved (row beyond the cap, deactivated-and-excluded staff,
 * or a failed lookup) so a raw GUID is never shown as if it were a name.
 *
 * Backing queries:
 *   GET /api/v1/admin/clients  -> { items, page, pageSize, totalCount }
 *   GET /api/v1/admin/staff    -> { items, totalCount }
 *
 * `includeInactive: true` on staff is deliberate: a task assigned to a
 * deactivated staff member must still resolve to their name, and the Admin
 * staff list hides inactive rows by default.
 *
 * Returns { clientNames, staffNames, resolveClient, resolveStaff, isLoading,
 *   isError }.
 */
export function useAdminEntityMaps() {
  const clientsQuery = useQuery({
    queryKey: ['admin', 'entity-map', 'clients'],
    queryFn: () => fetchAllPages((params) => getAdminClients(params)),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  const staffQuery = useQuery({
    queryKey: ['admin', 'entity-map', 'staff'],
    queryFn: () => fetchAllPages((params) => getAdminStaff({ ...params, includeInactive: true })),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  const clientNames = useMemo(() => {
    const map = new Map()
    for (const client of clientsQuery.data ?? []) {
      if (client?.id && client.companyName) {
        map.set(client.id, client.companyName)
      }
    }
    return map
  }, [clientsQuery.data])

  const staffNames = useMemo(() => {
    const map = new Map()
    for (const member of staffQuery.data ?? []) {
      if (member?.id && member.fullName) {
        map.set(member.id, member.fullName)
      }
    }
    return map
  }, [staffQuery.data])

  /**
   * Resolves a client company id. Returns null when there is no id to resolve,
   * otherwise { id, name, resolved, fallback }.
   */
  function resolveClient(clientCompanyId) {
    if (!clientCompanyId) {
      return null
    }
    const name = clientNames.get(clientCompanyId) ?? null
    return {
      id: clientCompanyId,
      name,
      resolved: Boolean(name),
      fallback: `Company ${shortId(clientCompanyId)}`,
    }
  }

  /**
   * Resolves a staff id. Returns null when there is no id to resolve (an
   * unassigned task has assignedStaffId = null), otherwise
   * { id, name, resolved, fallback }.
   */
  function resolveStaff(staffId) {
    if (!staffId) {
      return null
    }
    const name = staffNames.get(staffId) ?? null
    return {
      id: staffId,
      name,
      resolved: Boolean(name),
      fallback: `Staff ${shortId(staffId)}`,
    }
  }

  return {
    clientNames,
    staffNames,
    resolveClient,
    resolveStaff,
    isLoading: clientsQuery.isLoading || staffQuery.isLoading,
    isError: clientsQuery.isError || staffQuery.isError,
  }
}

export default useAdminEntityMaps
