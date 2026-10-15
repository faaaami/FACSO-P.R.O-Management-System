import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getAdminEmployees } from '../../api/admin/employees'
import { presentText } from '../../components/admin/clients/clientDisplay'
import { shortGuid } from '../../components/admin/documents/documentDisplay'

// A registry row carries owner GUIDs and nothing else:
//
//   ExpiringDocumentListItemDto  clientEntityId?, employeeId?  — no names,
//                                and no clientCompanyId either
//
// so an owner label has to be built client-side, the same way useAdminEntityMaps
// does it for the tasks and invoices that reference a company or a staff member
// by id alone. That hook is NOT reusable here: it maps clientCompanyId ->
// companyName and staffId -> staffName, and a registry row has neither of those
// keys. What it does provide is the pattern — page a name-bearing list once,
// cache it, and degrade to a short id rather than inventing a name.
const LOOKUP_PAGE_SIZE = 100
const MAX_LOOKUP_PAGES = 10 // hard cap: 1000 employees

/**
 * Pages a { items, totalCount } list until exhausted or the cap is hit. Reads
 * totalCount defensively rather than assuming a page count, matching the
 * existing entity-map helper.
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

/**
 * Owner attribution for the Expiry & Renewal Registry.
 *
 * EMPLOYEE-OWNED ROWS ARE FULLY ATTRIBUTABLE. A row with an employeeId resolves
 * against the paged employee list, which carries both `fullName` and
 * `entityName`, so one cached lookup labels the row with a person and their legal
 * entity. The drawer's Owner tab goes one step further and fetches the employee
 * detail, which is the only DTO that carries `clientCompanyId`, and from there
 * the company name through the existing useAdminClient — see
 * AdminDocumentOwnerSection.
 *
 * ENTITY-OWNED ROWS CANNOT BE ATTRIBUTED, AND THIS IS A HARD BACKEND LIMITATION
 * RATHER THAN A MISSING LOOKUP. The obvious move would be to resolve
 * `clientEntityId` against an entity list, but:
 *
 *   - There is NO /api/v1/admin/entities/{entityId} route. The only entity-by-id
 *     route is /admin/clients/{clientId}/entities/{entityId}, which is scoped by
 *     BOTH ids — and the registry row has no company id to supply.
 *   - The company-scoped repository method behind it is wired only to the Client
 *     module (GetMyExpiringDocuments), not to any Admin route.
 *   - Building an entity map the way the employee map is built would mean paging
 *     every company and then every company's entities: an unbounded N+1 walk with
 *     no cap that guarantees a correct result.
 *
 * So an entity-only document reports its owner as UNRESOLVED, showing a truncated
 * GUID that is labelled as unresolved. That is a deliberate, visible gap: the
 * alternative — rendering a GUID as though it were a company or entity name —
 * would put a fabrication in front of an admin user, which is worse than
 * admitting the backend cannot answer the question.
 *
 * Returns { employeeIndex, resolveOwner, isLoading, isError }.
 */
export function useAdminDocumentOwners() {
  const employeesQuery = useQuery({
    queryKey: ['admin', 'document-owner-map', 'employees'],
    queryFn: () => fetchAllPages((params) => getAdminEmployees(params)),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  // employeeId -> { name, entityName }
  const employeeIndex = useMemo(() => {
    const index = new Map()
    for (const employee of employeesQuery.data ?? []) {
      if (!employee?.id) continue
      index.set(employee.id, {
        name: presentText(employee.fullName),
        entityName: presentText(employee.entityName),
      })
    }
    return index
  }, [employeesQuery.data])

  /**
   * Resolves the owner of one registry row.
   *
   * Always returns a descriptor so the caller never has to infer ownership from
   * the absence of fields:
   *
   *   { kind: 'employee', id, name, entityName, resolved }
   *   { kind: 'entity',   id, name: null,        resolved: false }
   *   { kind: 'none',     id: null, name: null,   resolved: false }
   *
   * `resolved: false` on an employee row means the employee exists but sits beyond
   * the lookup cap, or the lookup itself failed — the caller renders the same
   * explicitly-unresolved treatment either way rather than showing a raw GUID as
   * if it were a name.
   */
  function resolveOwner(document) {
    const employeeId = document?.employeeId
    if (employeeId) {
      const match = employeeIndex.get(employeeId)
      const name = match?.name ?? null
      return {
        kind: 'employee',
        id: employeeId,
        name,
        entityName: match?.entityName ?? null,
        resolved: Boolean(name),
      }
    }

    const entityId = document?.clientEntityId
    if (entityId) {
      return {
        kind: 'entity',
        id: entityId,
        name: null,
        entityName: null,
        // Always false, and not because the lookup is missing: an entity-only
        // document's owner is unreachable through the Admin API at all.
        resolved: false,
      }
    }

    return { kind: 'none', id: null, name: null, entityName: null, resolved: false }
  }

  return {
    employeeIndex,
    resolveOwner,
    isLoading: employeesQuery.isLoading,
    isError: employeesQuery.isError,
  }
}

/**
 * Display string for an unresolved owner reference. Kept next to the resolver so
 * the "this is an id, not a name" rule cannot be forgotten at a call site.
 */
export function unresolvedOwnerLabel(owner) {
  const short = shortGuid(owner?.id)
  return short ? `Unresolved · ${short}` : 'Unresolved'
}

export default useAdminDocumentOwners
