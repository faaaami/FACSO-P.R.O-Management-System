import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getAdminClientEntities } from '../../api/admin/clients'
import { useAdminEntityMaps } from './useAdminEntityMaps'
import { useAdminDocumentOwners } from './useAdminDocumentOwners'
import { useAdminDocument } from './useAdminDocuments'

/**
 * Subject and context resolution for the Admin Service Requests module.
 *
 * ServiceRequestListItemDto and GetServiceRequestResponseDto both carry FOUR bare
 * Guids and no names at all:
 *
 *   clientCompanyId  -> the client company
 *   employeeId?      -> the person the request is about
 *   entityId?        -> the legal entity the request is about
 *   documentId?      -> the visa document an EarlyRenewal was resolved against
 *
 * No Admin route joins any of them to a name. So resolution is assembled from
 * endpoints that already return names, and the important part of this file is
 * WHERE IT IS DELIBERATELY NOT DONE.
 *
 * REUSED, NOT REFETCHED. Company names come from useAdminEntityMaps and employee
 * names from useAdminDocumentOwners, both of which the completed Admin modules
 * already call. Calling them here adds ZERO requests: TanStack serves both from
 * the shared cache entries ['admin','entity-map','clients'] and
 * ['admin','document-owner-map','employees'], so this module never pages the
 * client list or the employee list a second time. useAdminEntityMaps maps
 * clientCompanyId -> companyName and staffId -> staffName and has no employee or
 * entity key, which is why the employee index is taken from
 * useAdminDocumentOwners: its `employeeIndex` is a generic
 * employeeId -> { name, entityName } map. Its resolveOwner() is NOT used, because
 * its entity-less fallback assumes an entity can never be resolved, and this
 * module does resolve one.
 *
 * THE RESOLVERS TAKE AN ID, NOT THE RECORD. The table renders twenty rows and
 * cannot call a hook per row, so — exactly as the Renewal Tasks table does with
 * resolveClient / resolveStaff — the page calls this hook once and passes the
 * resolvers down as props, and each row supplies its own id.
 *
 * THE LIST DOES NOT RESOLVE ENTITIES OR DOCUMENTS. Building an entity map would
 * mean paging every company and then each company's entities, and a document map
 * would mean one request per row: an unbounded N+1 walk whose cost grows with
 * totalCount, on a page that is explicitly a plain paged list. So a list row shows
 * a short id labelled as unresolved, and the DRAWER resolves both for the single
 * record it is showing, where it is one company-scoped request and one document
 * request, both lazy. `detailLookups: true` enables them; the page leaves it off.
 *
 * The document lookup reuses the existing useAdminDocument hook, so the drawer
 * shares the cache entry ['admin','document', documentId] with the Admin
 * Documents module rather than opening a second one for the same row. It is
 * handed a null id when detail lookups are off, which keeps the query disabled
 * rather than merely ignoring its result.
 *
 * A REQUEST REFERENCES A DOCUMENT ONLY WHEN IT IS AN EarlyRenewal. The client
 * create handler resolves documentId for EarlyRenewal alone, so NewVisa and Other
 * requests always carry documentId = null. A missing document is therefore a
 * common, real state here and not a data fault.
 *
 * Returns { resolveCompany, resolveEmployee, resolveEntity, resolveDocument,
 *   isLoading, isError }.
 */

const LOOKUP_PAGE_SIZE = 100
const MAX_LOOKUP_PAGES = 10 // hard cap: 1000 rows, matching useAdminDocumentOwners

/**
 * Pages a { items, totalCount } list until exhausted or the cap is hit. Reads
 * totalCount defensively rather than assuming a page count, because the Admin
 * endpoints use three different pagination envelopes.
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
 * @param request  a ServiceRequestListItemDto or GetServiceRequestResponseDto.
 *                 Optional: the list-capable resolvers need only the shared maps,
 *                 so the page may pass undefined and still resolve every row.
 * @param options  { detailLookups?: boolean } — true only inside the drawer.
 */
export function useAdminServiceRequestSubjects(request, options = {}) {
  const detailLookups = options?.detailLookups === true

  const companyId = request?.clientCompanyId ?? null
  const documentId = request?.documentId ?? null

  // Shared with the completed Admin modules — no extra request.
  const { resolveClient } = useAdminEntityMaps()
  const { employeeIndex } = useAdminDocumentOwners()

  /**
   * A company's entities. Company-scoped because there is no
   * /admin/entities/{id} route — the only entity-by-id Admin route is
   * /admin/clients/{clientId}/entities/{entityId}, which needs BOTH ids, and a
   * list row has no single company to pair with. The drawer does have its own
   * record's company, so one company-scoped page resolves that record's entity.
   */
  const entitiesQuery = useQuery({
    queryKey: ['admin', 'service-request-subject-entities', companyId],
    queryFn: () => fetchAllPages((params) => getAdminClientEntities(companyId, params)),
    enabled: detailLookups && Boolean(companyId),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  const entityNames = useMemo(() => {
    const map = new Map()
    for (const entity of entitiesQuery.data ?? []) {
      if (entity?.id && entity.entityName) {
        map.set(entity.id, entity.entityName)
      }
    }
    return map
  }, [entitiesQuery.data])

  // The linked document, reusing the Admin Documents module's hook and cache. The
  // id is nulled out when detail lookups are off, so the query stays disabled.
  const documentQuery = useAdminDocument(detailLookups ? documentId : null, {})
  const documentData = detailLookups ? documentQuery.data : null

  /**
   * The client company. Delegates to useAdminEntityMaps, so the company column
   * and the drawer's /clients?search= link can never disagree about a name.
   * Returns null for a null id, otherwise that hook's
   * { id, name, resolved, fallback } shape.
   */
  function resolveCompany(id) {
    return resolveClient(id)
  }

  /**
   * The employee a request is about.
   *
   * Always returns a descriptor, so a call site never infers presence from
   * missing fields:
   *   { kind:'employee', id, name, entityName, resolved } — an employeeId was given
   *   { kind:'none',     id: null, ... }                   — no employee subject
   *
   * `resolved: false` means the employee exists but sits beyond the shared
   * lookup cap, or that lookup failed. Both render the explicitly unresolved
   * treatment rather than a Guid presented as a name.
   */
  function resolveEmployee(id) {
    if (!id) {
      return { kind: 'none', id: null, name: null, entityName: null, resolved: false }
    }

    const match = employeeIndex.get(id)
    const name = match?.name ?? null

    return {
      kind: 'employee',
      id,
      name,
      // The employee list carries the employee's own entity name, so an
      // employee-scoped request gets entity context from the same lookup.
      entityName: match?.entityName ?? null,
      resolved: Boolean(name),
    }
  }

  /**
   * The legal entity a request is about.
   *
   * Resolved in the drawer from the company-scoped entity list. In the list it is
   * not resolved at all, so `resolved: false` there is a deliberate refusal to
   * walk every company — not a missing lookup — and the caller says so in its
   * copy rather than implying the name was looked up and missed.
   */
  function resolveEntity(id) {
    if (!id) {
      return { kind: 'none', id: null, name: null, resolved: false }
    }

    const name = entityNames.get(id) ?? null
    return { kind: 'entity', id, name, resolved: Boolean(name) }
  }

  /**
   * The visa document an EarlyRenewal was resolved against.
   *
   * Returns identity fields only — document number, type, expiry — and never the
   * document's fileUrl as a link: the linked document is a pre-existing Documents
   * row that the request merely points at, not an attachment the request owns, and
   * this module has no verified route that serves it as one.
   */
  function resolveDocument() {
    if (!documentId) {
      return {
        kind: 'none',
        id: null,
        number: null,
        type: null,
        expiryDate: null,
        resolved: false,
      }
    }

    return {
      kind: 'document',
      id: documentId,
      number: documentData?.documentNumber ?? null,
      type: documentData?.type ?? null,
      expiryDate: documentData?.expiryDate ?? null,
      resolved: Boolean(documentData?.documentNumber),
    }
  }

  return {
    resolveCompany,
    resolveEmployee,
    resolveEntity,
    resolveDocument,
    isLoading: entitiesQuery.isLoading || documentQuery.isLoading,
    isError: entitiesQuery.isError || documentQuery.isError,
  }
}

export default useAdminServiceRequestSubjects
