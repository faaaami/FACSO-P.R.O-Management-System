import { ArrowRightCircle, Ban, Hourglass } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import AdminDetailRow from '../clients/AdminDetailRow'
import {
  formatDateTime,
  requestDescriptionText,
  serviceRequestStatusText,
  serviceRequestStatusTone,
  serviceRequestTypeText,
} from './serviceRequestDisplay'

/**
 * What became of the request: submitted, and then either still awaiting a
 * decision, converted into a renewal task, or rejected.
 *
 * THE TIMESTAMPS ARE NOT RENDERED AS A THREE-STEP TRACKER. The DTO carries no
 * decision actor, no decided-by name and no per-step history, so a three-node
 * progress bar with half its states hard-coded would be drawing a workflow the
 * response cannot actually support — it would show "Converted" for a request that
 * is still awaiting a decision. What is shown instead is the one real fact the
 * record holds: which terminal state it reached, when, and why, plus the honest
 * statement that it has not reached one.
 *
 * THE CONVERTED RENEWAL TASK IS REPORTED, NEVER LINKED.
 * convertedRenewalTaskId is a bare Guid in the service request response. There is
 * no Admin route that addresses a renewal task by id — the renewal task pages are
 * list and detail, and the detail addressable route is keyed by a value this
 * response does not provide. Building a link anyway would produce a URL that
 * silently opens the wrong thing or a blank detail pane, so the id is rendered as
 * mono text that is complete and copyable, and the copy says plainly that it
 * identifies a task in the Renewal Tasks module without pretending to point at
 * it. Renewal Tasks itself is untouched by this phase.
 *
 * CONVERSION IS NOT REVERSIBLE. ConvertServiceRequest sets status = Converted and
 * records convertedAt and convertedRenewalTaskId together; RejectServiceRequest
 * sets status = Rejected with rejectedAt and rejectionReason. Neither handler
 * reads a status other than Submitted, so a converted or rejected request cannot
 * be decided again by any endpoint. The copy says so, because "why can I not undo
 * this" is a fair question immediately after taking either decision.
 */
function TimelineEntry({ icon, tone, title, occurredAt, children }) {
  // Bound to a local first, exactly as AdminTaskDetailDrawer does for its tab icons:
  // a destructured-and-renamed prop is not seen as used by this repo's no-unused-vars
  // configuration even when it is rendered.
  const Icon = icon

  return (
    <li className="flex gap-3">
      <span
        className={`mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full border ${
          tone === 'success'
            ? 'border-[#0F9D74]/25 bg-[rgba(15,157,116,0.10)] text-[#0F9D74]'
            : tone === 'danger'
              ? 'border-[#DC2626]/25 bg-[rgba(220,38,38,0.10)] text-[#DC2626]'
              : 'border-[#D97706]/25 bg-[rgba(217,119,6,0.10)] text-[#D97706]'
        }`}
        aria-hidden="true"
      >
        <Icon size={13} strokeWidth={1.75} />
      </span>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
          <p className="text-sm font-semibold text-[#16181D]">{title}</p>
          <p className="text-xs text-[#6B7280]">
            {occurredAt ? formatDateTime(occurredAt) : <span className="text-[#9CA3AF]">&mdash;</span>}
          </p>
        </div>
        {children}
      </div>
    </li>
  )
}

function AdminServiceRequestLifecycleSection({ request }) {
  const isConverted = request.status === 'Converted'
  const isRejected = request.status === 'Rejected'
  const isSubmitted = request.status === 'Submitted'
  const rejectionReason = requestDescriptionText(request.rejectionReason)

  return (
    <div className="flex flex-col gap-5">
      <section
        aria-labelledby="admin-service-request-lifecycle-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3
            id="admin-service-request-lifecycle-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Outcome
          </h3>
          <StatusPill
            label={serviceRequestStatusText(request.status) ?? 'Unknown'}
            tone={serviceRequestStatusTone(request.status)}
          />
        </div>

        <ol className="mt-4 flex flex-col gap-4">
          <TimelineEntry icon={ArrowRightCircle} tone="success" title="Submitted" occurredAt={request.createdAt}>
            <p className="mt-0.5 text-xs text-[#6B7280]">
              The client raised this {serviceRequestTypeText(request.type) ?? 'service'} request
              {request.employeeId || request.entityId ? ' about the subject above' : ''}.
            </p>
          </TimelineEntry>

          {isConverted ? (
            <TimelineEntry icon={ArrowRightCircle} tone="success" title="Converted to a renewal task" occurredAt={request.convertedAt}>
              <p className="mt-0.5 text-xs text-[#6B7280]">
                Converting a request creates a renewal task in the Renewal Tasks module and
                closes this request. The task is a separate record with its own lifecycle.
              </p>
            </TimelineEntry>
          ) : null}

          {isRejected ? (
            <TimelineEntry icon={Ban} tone="danger" title="Rejected" occurredAt={request.rejectedAt}>
              {rejectionReason ? (
                <>
                  <p className="mt-1.5 text-xs font-medium text-[#6B7280]">Recorded reason</p>
                  <p className="mt-0.5 whitespace-pre-wrap break-words text-sm leading-relaxed text-[#16181D]">
                    {rejectionReason}
                  </p>
                </>
              ) : (
                <p className="mt-0.5 text-xs text-[#9CA3AF]">
                  The request was rejected without a recorded reason.
                </p>
              )}
            </TimelineEntry>
          ) : null}

          {isSubmitted ? (
            <TimelineEntry icon={Hourglass} tone="warning" title="Awaiting a decision" occurredAt={null}>
              <p className="mt-0.5 text-xs leading-relaxed text-[#6B7280]">
                No decision has been recorded. A request stays in this state until it is
                converted or rejected. The controls for doing that are at the foot of this
                drawer, and the note below explains what each one does.
              </p>
            </TimelineEntry>
          ) : null}
        </ol>
      </section>

      {isConverted && (
        <section
          aria-labelledby="admin-service-request-converted-task-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-service-request-converted-task-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Resulting renewal task
          </h3>

          <dl className="mt-3">
            <AdminDetailRow
              label="Renewal task ID"
              value={request.convertedRenewalTaskId ?? null}
              mono
            />
          </dl>

          <p className="mt-2.5 text-xs leading-relaxed text-[#6B7280]">
            This id identifies the renewal task created by the conversion. It is shown
            as text rather than a link: the service request response carries the id
            only, and there is no renewal task address in this module that can be built
            from it, so a link here could not reliably open the right task.
          </p>

          <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
            To find the task, open the Renewal Tasks list and look for the one created
            on the conversion date above.
          </p>
        </section>
      )}

      <section
        aria-labelledby="admin-service-request-irreversible-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA] p-4"
      >
        <h3
          id="admin-service-request-irreversible-heading"
          className="text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Deciding this request
        </h3>

        {isSubmitted ? (
          <>
            <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
              A submitted request can be converted into a renewal task or rejected with
              a reason, using the controls at the foot of this drawer. Both are one-way:
              each decision is recorded once and only a request that is still submitted
              can be decided, so a converted or rejected request cannot be reopened,
              converted again, or un-rejected by any endpoint in this application.
            </p>

            <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
              Conversion is more than a status change: it creates a renewal task in the
              Renewal Tasks module, unassigned and in the submitted state, with no
              service fee set. Assigning that task and setting its fee are separate
              later steps. If the conversion cannot go ahead — the document has been
              deactivated, or a task already exists for it — nothing is written at all,
              and the request stays submitted and can be decided again.
            </p>

            <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
              Either decision notifies the client company, and a rejection reason is
              shown to the client in their portal as well, so it should read as an
              explanation rather than an internal note.
            </p>
          </>
        ) : (
          <>
            <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
              This request has been decided, so the controls at the foot of the drawer
              are gone. Each decision is recorded once and only a request that is still
              submitted can be decided, so a converted or rejected request cannot be
              reopened, converted again, or un-rejected by any endpoint in this
              application. The record below is final.
            </p>

            <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
              A conversion writes the renewal task and the request's own status change
              together, so a request showing as converted always has its task, and a
              conversion that could not be completed left no task behind. Renewal Tasks
              is a separate module and is not affected by anything on this page.
            </p>
          </>
        )}
      </section>
    </div>
  )
}

export default AdminServiceRequestLifecycleSection
