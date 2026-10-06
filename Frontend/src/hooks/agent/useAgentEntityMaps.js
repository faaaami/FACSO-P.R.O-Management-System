import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getAgentClients, getAgentClientEntities } from '../../api/agent/clients'
import { getAgentEmployees } from '../../api/agent/employees'

// The Agent document/task DTOs expose only owner *ids* (clientEntityId,
// employeeId) — there is no join that returns owner names. To render readable
// owner labels we build client-side lookup maps from the Agent-visible client,
// entity and employee lists, which are themselves task-scoped. We page through
// them once and cache the result; anything beyond the cap falls back to a short
// id.
//
// NOTE: `clientEntityId` refers to `client_entities.id`, NOT
// `client_companies.id`. Resolving it against the company list alone can never
// match, which previously made every company-owned record render as
// "Company <short-guid>". We therefore build a dedicated entity-name map.
const LOOKUP_PAGE_SIZE = 100
const MAX_LOOKUP_PAGES = 10 // hard cap: 1000 entities per list

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

async function fetchAllEntities(clientIds) {
  const all = []
  for (const clientId of clientIds) {
    const entities = await fetchAllPages((params) =>
      getAgentClientEntities(clientId, params)
    )
    all.push(...entities)
  }
  return all
}

function shortId(id) {
  return String(id).slice(0, 8)
}

/**
 * Builds id → display-name maps for documents' owners (company / entity /
 * employee) and exposes a resolver that degrades to an owner-type + short-id
 * label when a name is not available (e.g. beyond the lookup cap or a failed
 * lookup).
 *
 * Backing queries: GET /api/v1/agent/clients, GET /api/v1/agent/employees, and
 * GET /api/v1/agent/clients/{id}/entities for each visible client.
 * Returns { resolveOwner, clientNames, entityNames, employeeNames, isLoading,
 *   isError }.
 */
export function useAgentEntityMaps() {
  const clientsQuery = useQuery({
    queryKey: ['agent', 'entity-map', 'clients'],
    queryFn: () => fetchAllPages((params) => getAgentClients(params)),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  const employeesQuery = useQuery({
    queryKey: ['agent', 'entity-map', 'employees'],
    queryFn: () => fetchAllPages((params) => getAgentEmployees(params)),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  const visibleClientIds = useMemo(
    () => (clientsQuery.data ?? []).map((client) => client?.id).filter(Boolean),
    [clientsQuery.data]
  )

  const entitiesQuery = useQuery({
    queryKey: ['agent', 'entity-map', 'entities'],
    queryFn: () => fetchAllEntities(visibleClientIds),
    enabled: visibleClientIds.length > 0,
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

  const entityNames = useMemo(() => {
    const map = new Map()
    for (const entity of entitiesQuery.data ?? []) {
      if (entity?.id && entity.entityName) {
        map.set(entity.id, entity.entityName)
      }
    }
    return map
  }, [entitiesQuery.data])

  const employeeNames = useMemo(() => {
    const map = new Map()
    for (const employee of employeesQuery.data ?? []) {
      if (employee?.id && employee.fullName) {
        map.set(employee.id, employee.fullName)
      }
    }
    return map
  }, [employeesQuery.data])

  /**
   * Resolves the owner of a document/task-like object that carries
   * employeeId and/or clientEntityId. Returns null when the object has no
   * owner, or { kind, id, name, fallback, resolved } otherwise.
   */
  function resolveOwner(entity) {
    if (entity?.employeeId) {
      const name = employeeNames.get(entity.employeeId) ?? null
      return {
        kind: 'employee',
        id: entity.employeeId,
        name,
        resolved: Boolean(name),
        fallback: `Employee ${shortId(entity.employeeId)}`,
      }
    }
    if (entity?.clientEntityId) {
      // `clientEntityId` is an entity id. Prefer the dedicated entity map;
      // fall back to the company map only for legacy/ambiguous ids.
      const name =
        entityNames.get(entity.clientEntityId) ??
        clientNames.get(entity.clientEntityId) ??
        null
      return {
        kind: 'entity',
        id: entity.clientEntityId,
        name,
        resolved: Boolean(name),
        fallback: `Entity ${shortId(entity.clientEntityId)}`,
      }
    }
    return null
  }

  return {
    resolveOwner,
    clientNames,
    entityNames,
    employeeNames,
    isLoading:
      clientsQuery.isLoading || employeesQuery.isLoading || entitiesQuery.isLoading,
    isError: clientsQuery.isError || employeesQuery.isError || entitiesQuery.isError,
  }
}

export default useAgentEntityMaps
