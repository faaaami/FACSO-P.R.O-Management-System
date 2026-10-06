import { ListChecks, Paperclip } from 'lucide-react'
import AdminClientSection, {
  AdminClientRecordCard,
  AdminClientRecordList,
} from '../clients/AdminClientSection'
import { useAdminEntityMaps } from '../../../hooks/admin/useAdminEntityMaps'
import { useAdminTaskSteps } from '../../../hooks/admin/useAdminTasks'
import {
  displayText,
  formatDateTime,
  presentText,
  proofReferenceText,
  shortGuid,
} from './taskDisplay'

/**
 * The renewal-step log for one task.
 *
 * BACKING QUERY: GET /api/v1/admin/tasks/{taskId}/steps, fetched only when this
 * tab is first activated.
 * Returns { taskId, items, totalCount }
 *   item: RenewalStepLogDto { id, stepName, completedBy, completedAt,
 *     referenceNumber?, proofFileUrl? }
 *
 * READ-ONLY. There is no Add, Edit or Delete control here, not even a disabled
 * one. The backend offers only POST .../steps — steps are append-only, with no
 * update or delete route anywhere — so "edit step" and "delete step" do not exist
 * as operations to expose, and offering them would be inventing a capability.
 *
 * ------------------------------------------------------------------
 * THE COUNT MISMATCH IS REAL, VERIFIED IN SQL, AND NOT RECONCILED HERE.
 * ------------------------------------------------------------------
 * Two different queries produce the two numbers and they genuinely disagree:
 *
 *   - The task DETAIL computes `COUNT(sl.id) AS StepLogCount` from
 *     `LEFT JOIN renewal_step_logs sl ON sl.renewal_task_id = rt.id`. That join
 *     carries NO `is_deleted` predicate, so it counts soft-deleted step rows.
 *   - The STEPS endpoint selects `WHERE ... AND is_deleted = false`, so its rows
 *     exclude them — and its own `totalCount` is literally `stepItems.Count`,
 *     i.e. always exactly the number of rows returned. It is not a second
 *     independent measurement and must not be presented as one.
 *
 * So `stepLogCount >= items.length`, and they differ precisely when a step has
 * been soft-deleted. This module shows both, each labelled with where it came
 * from. It does NOT subtract one from the other, and does not present the
 * difference as "deleted steps": the API exposes no way to know that the
 * shortfall is deletions rather than anything else, so the wording says only
 * what is verifiable — that the list excludes soft-deleted rows the count
 * includes.
 *
 * ORDERING IS THE BACKEND'S: `ORDER BY completed_at ASC`, with NO tie-breaker.
 * Two steps completed in the same instant therefore have no defined relative
 * order, and the browser neither re-sorts them nor invents a secondary key.
 *
 * EVERY LISTED STEP IS COMPLETED. `completedAt` is a non-nullable DateTime on the
 * DTO and the log is append-only, so there is no per-step "status" field to
 * render and none is invented: a row's presence in this list is the completion
 * record. Completion state belongs to the task, and is shown on the Overview tab.
 *
 * `completedBy` is the acting USER, and adding a step is an Admin action, so this
 * id is usually an Admin account that the Agent-only staff map cannot resolve.
 * It is rendered as an explicit unresolved short id rather than a guessed name.
 *
 * `proofFileUrl` is free text typed in at add-step time. Nothing verifies it
 * resolves, and there is no Admin route that downloads a task-step proof, so it
 * is shown as inert reference text and never as a link or a download.
 */
function AdminTaskStepsSection({ taskId, stepLogCount }) {
  const { items, isLoading, isError, error, refresh } = useAdminTaskSteps(taskId)
  const { resolveStaff } = useAdminEntityMaps()

  const listedCount = items.length
  const detailCount = Number.isFinite(stepLogCount) ? stepLogCount : null
  const countsDisagree = detailCount != null && detailCount !== listedCount

  function renderCompletedBy(step) {
    const actor = resolveStaff?.(step?.completedBy)

    if (!step?.completedBy) {
      return <span className="text-[#9CA3AF]">Not recorded</span>
    }

    if (actor?.resolved) {
      return <span className="text-[#16181D]">{actor.name}</span>
    }

    return (
      <span className="text-[#9CA3AF]" title={String(step.completedBy)}>
        {shortGuid(step.completedBy)}
        <span className="sr-only"> — actor not resolvable to a name</span>
      </span>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <AdminClientSection
        title="Renewal steps"
        description="Completed steps recorded against this task, oldest first."
        loading={isLoading}
        loadingLabel="Loading steps…"
        error={isError ? error : null}
        onRetry={() => refresh()}
        errorMessage="Could not load this task's steps."
        isEmpty={!isLoading && !isError && listedCount === 0}
        emptyMessage="No steps recorded yet."
        emptyDescription="Steps are added by an administrator as the renewal progresses. Nothing has been logged against this task."
        emptyIcon={ListChecks}
      >
        <AdminClientRecordList items={items}>
          {(step) => {
            const reference = presentText(step?.referenceNumber)
            const proof = proofReferenceText(step?.proofFileUrl)

            return (
              <AdminClientRecordCard
                key={step?.id}
                title={displayText(step?.stepName)}
                subtitle={`Completed ${formatDateTime(step?.completedAt)}`}
                meta={[
                  { label: 'Completed by', value: renderCompletedBy(step) },
                  { label: 'Reference', value: reference ?? <span className="text-[#9CA3AF]">None</span> },
                  { label: 'Step record', value: shortGuid(step?.id) ?? '—' },
                ]}
                trailing={
                  proof ? (
                    <span
                      className="inline-flex max-w-[9rem] items-center gap-1.5 rounded-[6px] border border-[#E2E4E9] bg-white px-2 py-1 text-[11px] text-[#6B7280]"
                      title={proof}
                    >
                      <Paperclip size={11} strokeWidth={1.75} aria-hidden="true" />
                      <span className="truncate">Proof reference</span>
                    </span>
                  ) : null
                }
              />
            )
          }}
        </AdminClientRecordList>
      </AdminClientSection>

      {/*
        The two figures are reported side by side with their provenance, and only
        when they actually disagree. A note that appears unconditionally would be
        noise on the common case where every step is present.
      */}
      {countsDisagree && (
        <p
          role="note"
          className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]"
        >
          <span className="font-semibold">Counts differ.</span> The task record
          counts {detailCount.toLocaleString('en-US')} step row
          {detailCount === 1 ? '' : 's'} in total, while {listedCount.toLocaleString('en-US')}{' '}
          {listedCount === 1 ? 'is' : 'are'} listed above. The list endpoint
          excludes soft-deleted steps and the task total does not, which is the
          only difference the API lets us account for. Both figures are shown as
          the backend reported them; nothing is subtracted or reconciled here.
        </p>
      )}

      {!countsDisagree && detailCount != null && !isLoading && !isError && (
        <p className="text-xs text-[#6B7280]">
          {listedCount === 0
            ? 'The task record counts 0 steps, matching the empty list above.'
            : `All ${detailCount.toLocaleString('en-US')} step rows counted on the task are listed above.`}
        </p>
      )}
    </div>
  )
}

export default AdminTaskStepsSection
