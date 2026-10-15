import { useQuery } from '@tanstack/react-query'
import { getAdminClients, getAdminClientEntities } from '../../api/admin/clients'
import { getAdminEmployees } from '../../api/admin/employees'

/**
 * Owner choices for the D1 document review drawer.
 *
 * A confirmation names exactly one owner, and the UI must offer only real,
 * authorised choices — never a free-text GUID box. These are the only owner
 * lists the Admin API actually exposes, so they are what the drawer uses:
 *
 *   Employee -> GET /api/v1/admin/employees  (paged, global to Admin)
 *   Entity   -> GET /api/v1/admin/clients/{clientId}/entities (paged)
 *
 * THERE IS NO ADMIN ENTITY LIST ROUTE. An entity is only reachable through its
 * company, so the entity picker is necessarily two steps: pick the company, then
 * the entity within it (see useAdminClientEntities). This reuses the existing
 * client endpoints rather than inventing a global entity search — the same
 * documented limit useAdminDocumentOwners already works around for display.
 *
 * The employee query deliberately shares the key `['admin','document-owner-map',
 * 'employees']` with useAdminDocumentOwners, so opening the review drawer reuses
 * the employee list the registry has already paged instead of fetching it again.
 * Both hooks consume the same paged array.
 */
const LOOKUP_PAGE_SIZE = 100
const MAX_LOOKUP_PAGES = 10 // hard cap: 1000 rows per list

async function fetchAllPages(fetchPage) {
  const all = []
  for (let page = 1; page <= MAX_LOOKUP_PAGES; page += 1) {
    const result = await fetchPage({ page, pageSize: LOOKUP_PAGE_SIZE })
    const items = result?.items ?? []
    all.push(...items)
    const totalCount = result?.totalCount ?? 0
    if (items.length === 0 || all.length >= totalCount) {
      break
    }
  }
  return all
}

function nameOf(employee) {
  const fullName = typeof employee?.fullName === 'string' ? employee.fullName.trim() : ''
  if (fullName) return fullName
  return `Employee ${String(employee?.id ?? '').slice(0, 8)}`
}

/**
 * Returns the owner option lists the review drawer offers.
 *
 * Returns { employees, clients, nameOf, isLoading, isError, hasEmployees,
 *   hasClients }.
 */
export function useAdminDocumentOwnerOptions() {
  const employeesQuery = useQuery({
    queryKey: ['admin', 'document-owner-map', 'employees'],
    queryFn: () => fetchAllPages((params) => getAdminEmployees(params)),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  const clientsQuery = useQuery({
    queryKey: ['admin', 'document-owner-map', 'clients'],
    queryFn: () => fetchAllPages((params) => getAdminClients(params)),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  const employees = employeesQuery.data ?? []
  const clients = clientsQuery.data ?? []

  return {
    employees,
    clients,
    nameOf,
    isLoading: employeesQuery.isLoading || clientsQuery.isLoading,
    isError: employeesQuery.isError || clientsQuery.isError,
    hasEmployees: employees.length > 0,
    hasClients: clients.length > 0,
  }
}

/**
 * The entities of ONE company — the second step of the entity picker.
 *
 * Kept as its own hook so entities are fetched only for the company the
 * reviewer actually selected; no entity list is prefetched.
 */
export function useAdminClientEntities(clientId) {
  const query = useQuery({
    queryKey: ['admin', 'document-owner-map', 'entities', clientId ?? null],
    queryFn: () => fetchAllPages((params) => getAdminClientEntities(clientId, params)),
    enabled: Boolean(clientId),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  return {
    entities: query.data ?? [],
    isLoading: query.isLoading,
    isError: query.isError,
  }
}

export default useAdminDocumentOwnerOptions
