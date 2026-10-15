import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useAdminEntityMaps } from './useAdminEntityMaps'
import { getAdminClientContracts } from '../../api/admin/contracts'
import { getAdminTasks } from '../../api/admin/tasks'

/**
 * Option lists for the Admin Invoices filter bar: client company, that company's
 * service contracts, and that company's renewal tasks.
 *
 * ------------------------------------------------------------------
 * EVERY RELATIONSHIP FILTER IS COMPANY-GATED, AND THE REASON IS THE API'S.
 * ------------------------------------------------------------------
 * The Retainer contract filter needs GET /api/v1/clients/{clientCompanyId}/contracts
 * and the Service Fee task filter needs GET /api/v1/admin/tasks?clientCompanyId=…
 * — both are COMPANY-SCOPED endpoints. There is no global contracts list, no Admin
 * /contracts route, and no task text-search parameter anywhere in the platform.
 * So neither option list can be built before a company is chosen, and inventing a
 * free-text id field to work around that would be a control that submits a value
 * the API cannot validate.
 *
 * `enabled: Boolean(clientCompanyId)` on both queries is what makes the gating real:
 * a component cannot skip a hook call, so the call is made unconditional and the
 * QUERY is disabled instead. Nothing is requested until a company is selected, and
 * the filter bar explains the requirement rather than rendering an empty dropdown
 * that would look broken.
 *
 * COMPANY OPTIONS COME FROM THE SHARED ENTITY MAP, not from a request of their own.
 * useAdminEntityMaps already pages through GET /admin/clients once and caches the
 * result under ['admin','entity-map','clients'] at a 5-minute staleTime, and every
 * other Admin module holds that same entry. Reusing it means selecting a company
 * here costs no additional request in a session that has visited any other Admin
 * page. Its 1,000-row cap is inherited: a company beyond the cap is absent from the
 * dropdown while the rows themselves still render with an explicit unresolved short
 * id, so nothing becomes unreachable — it simply cannot be SELECTED as a filter.
 *
 * THE CONTRACT LIST RESPONSE IS { items } ONLY. It carries no totalCount, so the
 * option list cannot report how many contracts exist beyond a hard page cap. The
 * page size is therefore set well above any realistic per-company contract count
 * rather than paging, and the copy below the filter says the list may be capped.
 *
 * THE TASK LIST IS CAPPED AT ONE PAGE too, for the same reason: GET /admin/tasks
 * has no search and no sort, so there is no way to enumerate tasks beyond the first
 * page for a company. The copy says so.
 *
 * THE `Updated` SUBSET IS A SEPARATE QUERY, NOT A FILTER OF THAT LIST. The Server Fee
 * creation dialog needs only `Updated` tasks, and GET /admin/tasks DOES accept
 * `?status=RenewalTaskStatus` — TasksController binds it, GetRenewalTasksQuery carries
 * it, and the repository applies it against the integer column from the typed enum, so
 * the filter is genuinely usable. See useAdminUpdatedTaskOptions. It could not be done
 * here as a client-side narrowing of `taskOptions` for two reasons: this list must
 * serve the filter bar's status filter and the Gov Fee picker's optional link, both of
 * which need every status, and one capped page of mixed statuses can hide a qualifying
 * task behind 100 rows that are not eligible. Ask the server instead of guessing which
 * of its rows to keep.
 *
 * Query keys are namespaced under ['admin','billing','filter-options', …] and
 * carry the clientCompanyId, so a company's options are cached per company and
 * switching company never shows the previous company's contracts or tasks.
 */
const OPTION_PAGE_SIZE = 100

/**
 * `RenewalTaskStatus` is an enum, so it arrives as its NAME under the global
 * JsonStringEnumConverter — `RenewalTaskStatus.Updated = 6` serialises as
 * "Updated", not 6. Every status comparison in this file and its callers therefore
 * compares against the name.
 */
const UPDATED_TASK_STATUS = 'Updated'

/**
 * The one task→option mapping, shared by the full list and the `Updated` subset so the
 * two dropdowns cannot drift apart in shape. See useAdminUpdatedTaskOptions.
 */
function toTaskOption(task) {
  return {
    value: task.id,
    label: `${String(task.id).slice(0, 8)}…`,
    status: task.status ?? null,
  }
}

export function useAdminBillingFilterOptions(clientCompanyId) {
  const selectedCompanyId = clientCompanyId ?? null

  // Shared with every other Admin module; adds no request of its own.
  const { clientNames, isLoading: isCompaniesLoading, isError: isCompaniesError } =
    useAdminEntityMaps()

  /**
   * The company's service contracts. `GET /clients/{id}/contracts` is the only
   * contract route the Admin API exposes, and it is always company-scoped — there
   * is deliberately no global contract list to fall back on.
   */
  const contractsQuery = useQuery({
    queryKey: ['admin', 'billing', 'filter-options', 'contracts', selectedCompanyId],
    queryFn: () => getAdminClientContracts(selectedCompanyId, { page: 1, pageSize: OPTION_PAGE_SIZE }),
    enabled: Boolean(selectedCompanyId),
    staleTime: 30_000,
    retry: 1,
  })

  /**
   * The company's renewal tasks. `GET /admin/tasks` binds `clientCompanyId`, so the
   * same company scoping is achieved with a query parameter rather than a route
   * segment. No `search` parameter exists on this endpoint, so no text box is
   * offered for it.
   */
  const tasksQuery = useQuery({
    queryKey: ['admin', 'billing', 'filter-options', 'tasks', selectedCompanyId],
    queryFn: () => getAdminTasks({ clientCompanyId: selectedCompanyId, page: 1, pageSize: OPTION_PAGE_SIZE }),
    enabled: Boolean(selectedCompanyId),
    staleTime: 30_000,
    retry: 1,
  })

  const companyOptions = useMemo(
    () =>
      [...clientNames.entries()]
        .map(([value, label]) => ({ value, label }))
        .sort((a, b) => a.label.localeCompare(b.label)),
    [clientNames],
  )

  /**
   * A contract option is labelled by its contract number, because that is the only
   * human-readable string on ServiceContractListItemDto. The dates are deliberately
   * not folded into the label: the backend's own "active" notion ignores dates, so
   * decorating the option with one would imply a currency of meaning it does not
   * have.
   */
  const contractOptions = useMemo(
    () =>
      (contractsQuery.data?.items ?? [])
        .filter((contract) => contract?.id && contract?.contractNumber)
        .map((contract) => ({
          value: contract.id,
          label: contract.contractNumber,
          status: contract.status ?? null,
        })),
    [contractsQuery.data],
  )

  /**
   * A task option is labelled by the task id's short form, because
   * RenewalTaskListItemDto carries no title, no number and no document name — only
   * `id`, `documentId`, `clientCompanyId`, `assignedStaffId`, `status` and
   * timestamps. A short id plus the status is the whole of what can honestly be
   * said about a task in a dropdown, and the status makes the list readable without
   * pretending a name exists.
   *
   * EVERY status is kept, because two consumers need them all: the filter bar's task
   * status filter, and the Gov Fee dialog's optional renewal-task link. The `Updated`
   * subset the Service Fee dialog needs is deliberately NOT narrowed out of this list
   * — it is its own query, see useAdminUpdatedTaskOptions, because this response is one
   * capped page of mixed statuses and a qualifying task beyond the cap would be
   * unreachable there with no way to tell that from having none.
   */
  const taskOptions = useMemo(
    () => (tasksQuery.data?.items ?? []).filter((task) => task?.id).map(toTaskOption),
    [tasksQuery.data],
  )

  return {
    companyOptions,
    contractOptions,
    taskOptions,
    /**
     * The AUTHORITATIVE number of tasks the company has, not the size of this page.
     * Used by the Service Fee dialog to say how many were checked and found
     * non-qualifying, which a capped page could not honestly state.
     */
    tasksTotalCount: tasksQuery.data?.totalCount ?? 0,

    // The relationship option lists are UNAVAILABLE, not EMPTY, until a company is
    // chosen. The filter bar says so in words; these flags let it tell "you must
    // pick a company" from "that company has none".
    contractsEnabled: Boolean(selectedCompanyId),
    tasksEnabled: Boolean(selectedCompanyId),
    isContractsLoading: Boolean(selectedCompanyId) && contractsQuery.isLoading,
    isContractsError: Boolean(selectedCompanyId) && contractsQuery.isError,
    contractsError: contractsQuery.error,
    refreshContracts: contractsQuery.refetch,

    isTasksLoading: Boolean(selectedCompanyId) && tasksQuery.isLoading,
    isTasksError: Boolean(selectedCompanyId) && tasksQuery.isError,
    tasksError: tasksQuery.error,
    refreshTasks: tasksQuery.refetch,

    isCompaniesLoading,
    isCompaniesError,
  }
}

export default useAdminBillingFilterOptions

/**
 * `Updated` renewal tasks for one company — the ONLY tasks a Service Fee invoice can be
 * raised against. ServiceFeeInvoiceCreationService reads the task and refuses anything
 * else with a 409, so the dialog needs this list and not a narrowing of the filter bar's.
 *
 * WHY THIS IS ITS OWN REQUEST. GET /api/v1/admin/tasks binds
 * `[FromQuery] RenewalTaskStatus? status`, GetRenewalTasksQuery carries it, and
 * RenewalTaskReadRepository applies it as `Status = @Status` with the TYPED enum, which
 * Dapper sends as its underlying integer against the integer column. So the filter works
 * — an earlier version of this file's comments claimed it did not exist, and was wrong.
 * (It is genuinely broken on the PAYMENT ORDER list, where
 * IPaymentOrderReadRepository takes `string? status` and binds that text against an
 * integer column; that is why the Payments tab has no status filter. Two endpoints, two
 * shapes, and the difference is the parameter type.)
 *
 * Two consumers must not be served by one request: the filter bar's task status filter
 * and the Gov Fee dialog's optional link both need EVERY status, so this is opt-in and
 * the callers that do not need it pay nothing. The query key is deliberately distinct
 * from the unfiltered `'tasks'` entry so the two cannot clobber each other's cache.
 *
 * It is a filter and NOT a validation, for one reason that matters: the DTO carries no
 * invoice reference, so this cannot know whether a task has already been invoiced. The
 * service applies "at most one non-deleted invoice per task" itself, so a task already
 * invoiced is still offered and is refused by the server. Pre-checking it would mean N
 * requests and a rule that could still race. The server stays the authority for what
 * this list cannot know.
 */
export function useAdminUpdatedTaskOptions(clientCompanyId) {
  const selectedCompanyId = clientCompanyId ?? null

  const query = useQuery({
    queryKey: ['admin', 'billing', 'filter-options', 'tasks-updated', selectedCompanyId],
    queryFn: () =>
      getAdminTasks({
        clientCompanyId: selectedCompanyId,
        status: UPDATED_TASK_STATUS,
        page: 1,
        pageSize: OPTION_PAGE_SIZE,
      }),
    enabled: Boolean(selectedCompanyId),
    staleTime: 30_000,
    retry: 1,
  })

  const updatedTaskOptions = useMemo(
    () =>
      (query.data?.items ?? [])
        .filter((task) => task?.id)
        .map(toTaskOption)
        // The server already filtered, so this re-check should be a no-op. It is here
        // because the response is cached for 30s: a task set to Updated elsewhere
        // shortly after this request can still be present, and offering it would only
        // invite the 409 the comment above explains.
        .filter((task) => task.status === UPDATED_TASK_STATUS),
    [query.data],
  )

  return {
    updatedTaskOptions,
    /** UNAVAILABLE, not EMPTY, until a company is chosen — same rule as the siblings. */
    updatedTasksEnabled: Boolean(selectedCompanyId),
    isUpdatedTasksLoading: Boolean(selectedCompanyId) && query.isLoading,
    isUpdatedTasksError: Boolean(selectedCompanyId) && query.isError,
    updatedTasksError: query.error,
    updatedTasksTotalCount: query.data?.totalCount ?? 0,
    refreshUpdatedTasks: query.refetch,
  }
}
