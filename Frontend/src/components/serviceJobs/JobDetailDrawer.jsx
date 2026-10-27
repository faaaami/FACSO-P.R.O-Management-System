import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  AlertTriangle,
  Ban,
  Briefcase,
  CheckCircle2,
  CircleDot,
  FileCheck2,
  Landmark,
  MessageSquare,
  Pause,
  Play,
  RotateCcw,
  SkipForward,
  UserRound,
} from 'lucide-react'
import Drawer from '../client/documents/Drawer'
import StatusPill from '../client/StatusPill'
import LoadingState from '../client/LoadingState'
import ErrorState from '../client/ErrorState'
import { extractApiErrorMessage } from '../../utils/apiError'
import StaffSelect from './StaffSelect'
import {
  addJobGovFee,
  addJobNote,
  assignJob,
  cancelJob,
  completeJob,
  getJob,
  holdJob,
  resumeJob,
  setJobDocument,
  startJob,
  updateJobDetails,
  updateJobStep,
} from '../../api/serviceJobs'
import {
  CATEGORY_LABELS,
  CUSTOMER_TYPE_LABELS,
  DOCUMENT_TYPE_LABELS,
  JOB_STATUS,
  SOURCE_LABELS,
  STEP_STATUS,
  formatAed,
  formatDate,
  formatDateTime,
  isOpenStatus,
} from './labels'
import { Button, Chip, Field, Input, Modal, TextArea } from './ui'

function Section({ title, icon: Icon, action, children }) {
  return (
    <section className="border-t border-[#E2E4E9] pt-5 first:border-t-0 first:pt-0">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3 className="flex items-center gap-2 text-sm font-semibold text-[#16181D]">
          {Icon && <Icon size={15} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />}
          {title}
        </h3>
        {action}
      </div>
      {children}
    </section>
  )
}

function Row({ label, children }) {
  return (
    <div className="grid grid-cols-[130px_1fr] gap-3 py-1.5 text-sm">
      <dt className="text-[#6B7280]">{label}</dt>
      <dd className="min-w-0 break-words text-[#16181D]">{children ?? '—'}</dd>
    </div>
  )
}

/** Prompt for a reason (hold / cancel). */
function ReasonDialog({ title, confirmLabel, tone, onSubmit, onClose, busy }) {
  const [reason, setReason] = useState('')
  return (
    <Modal
      title={title}
      size="sm"
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Back</Button>
          <Button variant={tone === 'danger' ? 'danger' : 'primary'} loading={busy}
            disabled={!reason.trim()} onClick={() => onSubmit(reason.trim())}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <Field label="Reason" required hint="The client sees this reason on their portal.">
        {(id) => <TextArea id={id} value={reason} maxLength={1000} onChange={(e) => setReason(e.target.value)} autoFocus />}
      </Field>
    </Modal>
  )
}

function CompleteDialog({ job, onSubmit, onClose, busy }) {
  const isRenewal = Boolean(job.renewsDocumentType && job.documentId)
  const [note, setNote] = useState('')
  const [expiry, setExpiry] = useState('')
  const [number, setNumber] = useState('')
  const [issued, setIssued] = useState('')
  const canSubmit = !isRenewal || Boolean(expiry)

  return (
    <Modal
      title={`Complete ${job.jobNumber}`}
      subtitle={job.serviceName}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Back</Button>
          <Button variant="accent" icon={CheckCircle2} loading={busy} disabled={!canSubmit}
            onClick={() => onSubmit({
              note: note.trim() || null,
              renewedExpiryDate: expiry || null,
              renewedDocumentNumber: number.trim() || null,
              renewedIssueDate: issued || null,
            })}>
            Mark completed
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {isRenewal && (
          <div className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-4">
            <p className="mb-3 text-xs text-[#6B7280]">
              This job renews {DOCUMENT_TYPE_LABELS[job.documentType] ?? job.documentType} {job.documentNumber}
              {' '}(expires {formatDate(job.documentExpiryDate)}). Enter the renewed document&apos;s details.
            </p>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label="New expiry date" required>
                {(id) => <Input id={id} type="date" value={expiry} onChange={(e) => setExpiry(e.target.value)} />}
              </Field>
              <Field label="New number">
                {(id) => <Input id={id} value={number} onChange={(e) => setNumber(e.target.value)} placeholder="If changed" />}
              </Field>
              <Field label="Issue date">
                {(id) => <Input id={id} type="date" value={issued} onChange={(e) => setIssued(e.target.value)} />}
              </Field>
            </div>
          </div>
        )}
        <Field label="Completion note" hint="Optional. Shared with the client.">
          {(id) => <TextArea id={id} value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />}
        </Field>
      </div>
    </Modal>
  )
}

function GovFeeForm({ onSubmit, busy }) {
  const [description, setDescription] = useState('')
  const [amount, setAmount] = useState('')
  const [reference, setReference] = useState('')
  const valid = description.trim() && Number(amount) > 0

  return (
    <form
      className="mt-3 grid gap-2 sm:grid-cols-[1fr_110px_1fr_auto]"
      onSubmit={(e) => {
        e.preventDefault()
        if (!valid) return
        onSubmit({ feeDescription: description.trim(), amount: Number(amount), governmentReference: reference.trim() || null },
          () => { setDescription(''); setAmount(''); setReference('') })
      }}
    >
      <Input placeholder="Fee (e.g. Entry permit)" value={description} onChange={(e) => setDescription(e.target.value)} aria-label="Fee description" />
      <Input placeholder="AED" type="number" min="0" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} aria-label="Amount in AED" />
      <Input placeholder="Receipt / transaction no." value={reference} onChange={(e) => setReference(e.target.value)} aria-label="Government reference" />
      <Button type="submit" variant="secondary" loading={busy} disabled={!valid}>Add</Button>
    </form>
  )
}

/**
 * Job detail for every role. Admin and Agent can work the job (subject to API
 * rules); Admin additionally assigns, cancels and edits fee / due date /
 * priority. Clients get a read-only progress view.
 */
function JobDetailDrawer({ role, jobId, onClose }) {
  const queryClient = useQueryClient()
  const isStaff = role === 'Admin' || role === 'Agent'
  const isAdmin = role === 'Admin'
  const [dialog, setDialog] = useState(null)
  const [stepRefs, setStepRefs] = useState({})
  const [note, setNote] = useState('')
  const [noteInternal, setNoteInternal] = useState(true)
  const [appNumber, setAppNumber] = useState(null)

  const query = useQuery({
    queryKey: ['service-job', role, jobId],
    queryFn: () => getJob(role, jobId),
    enabled: Boolean(jobId),
  })

  const mutation = useMutation({
    mutationFn: ({ action }) => action(),
    onSuccess: (job, { success, after }) => {
      queryClient.setQueryData(['service-job', role, jobId], job)
      queryClient.invalidateQueries({ queryKey: ['service-jobs'] })
      if (success) toast.success(success)
      after?.()
      setDialog(null)
    },
    onError: (error) => toast.error(extractApiErrorMessage(error, 'The change could not be saved.')),
  })

  const run = (action, success, after) => mutation.mutate({ action, success, after })
  const busy = mutation.isPending

  const job = query.data
  const title = job ? job.jobNumber : 'Job'

  let body
  if (query.isLoading) {
    body = <LoadingState label="Loading job…" />
  } else if (query.isError) {
    body = <ErrorState message={extractApiErrorMessage(query.error, 'Could not load this job.')} onRetry={() => query.refetch()} />
  } else if (job) {
    const open = isOpenStatus(job.status)
    const status = JOB_STATUS[job.status] ?? { label: job.status, tone: 'neutral' }
    const stepsDone = job.steps.filter((s) => s.status !== 'Pending').length
    const govTotal = job.governmentFees.reduce((sum, fee) => sum + Number(fee.amount), 0)

    body = (
      <div className="space-y-6">
        {/* Status + actions */}
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill label={status.label} tone={status.tone} />
            {job.priority === 'Urgent' && <Chip tone="danger">Urgent</Chip>}
            {job.isOverdue && <Chip tone="warning"><AlertTriangle size={12} aria-hidden="true" />Overdue</Chip>}
            <Chip>{CATEGORY_LABELS[job.category] ?? job.category}</Chip>
          </div>
          {job.status === 'OnHold' && job.onHoldReason && (
            <p className="rounded-[10px] border border-[#D97706]/20 bg-amber-50 px-3 py-2 text-sm text-[#92400E]">
              On hold: {job.onHoldReason}
            </p>
          )}
          {job.status === 'Cancelled' && job.cancelReason && (
            <p className="rounded-[10px] border border-[#DC2626]/20 bg-red-50 px-3 py-2 text-sm text-[#991B1B]">
              Cancelled: {job.cancelReason}
            </p>
          )}
          {isStaff && open && (
            <div className="flex flex-wrap gap-2">
              {job.status === 'New' && (
                <Button variant="primary" icon={Play} loading={busy} onClick={() => run(() => startJob(role, job.id), 'Job started.')}>
                  Start
                </Button>
              )}
              {job.status === 'InProgress' && (
                <Button variant="accent" icon={CheckCircle2} disabled={stepsDone < job.steps.length}
                  title={stepsDone < job.steps.length ? 'Mark every step done or skipped first' : undefined}
                  onClick={() => setDialog('complete')}>
                  {stepsDone < job.steps.length ? `Complete (${job.steps.length - stepsDone} steps left)` : 'Complete'}
                </Button>
              )}
              {job.status === 'OnHold' ? (
                <Button icon={RotateCcw} loading={busy} onClick={() => run(() => resumeJob(role, job.id), 'Job resumed.')}>
                  Resume
                </Button>
              ) : (
                <Button icon={Pause} onClick={() => setDialog('hold')}>Put on hold</Button>
              )}
              {isAdmin && <Button variant="danger" icon={Ban} onClick={() => setDialog('cancel')}>Cancel job</Button>}
            </div>
          )}
        </div>

        {/* Summary */}
        <Section title="Details" icon={Briefcase}>
          <dl>
            <Row label="Service">
              {job.serviceName}
              {job.authority && <span className="text-[#6B7280]"> · {job.authority}</span>}
            </Row>
            <Row label="Customer">
              {job.clientName}
              <span className="text-[#6B7280]">
                {job.customerCode ? ` · ${job.customerCode}` : ''} · {CUSTOMER_TYPE_LABELS[job.customerType] ?? job.customerType}
              </span>
            </Row>
            {job.personName && (
              <Row label="Person">
                {job.personName}
                {job.personPassportNumber && <span className="text-[#6B7280]"> · {job.personPassportNumber}</span>}
              </Row>
            )}
            {job.entityName && <Row label="Licence / entity">{job.entityName}</Row>}
            {job.documentNumber && (
              <Row label="Document">
                {DOCUMENT_TYPE_LABELS[job.documentType] ?? job.documentType} {job.documentNumber}
                <span className="text-[#6B7280]"> · expires {formatDate(job.documentExpiryDate)}</span>
              </Row>
            )}
            <Row label="Assigned to">
              {isAdmin && open ? (
                <StaffSelect
                  value={job.assignedStaffId}
                  currentName={job.assignedStaffName}
                  disabled={busy}
                  onChange={(staffId) => run(() => assignJob(job.id, staffId), 'Assignment updated.')}
                />
              ) : (
                job.assignedStaffName ?? 'Unassigned'
              )}
            </Row>
            <Row label="Application no.">
              {isStaff && open ? (
                <form
                  className="flex gap-2"
                  onSubmit={(e) => {
                    e.preventDefault()
                    run(() => updateJobDetails(role, job.id, isAdmin
                      ? { applicationNumber: appNumber ?? job.applicationNumber, notes: job.notes, dueDate: job.dueDate, priority: job.priority, serviceFee: job.serviceFee }
                      : { applicationNumber: appNumber ?? job.applicationNumber, notes: job.notes }),
                    'Application number saved.', () => setAppNumber(null))
                  }}
                >
                  <Input aria-label="Government application number" value={appNumber ?? job.applicationNumber ?? ''}
                    onChange={(e) => setAppNumber(e.target.value)} placeholder="e.g. transaction no." />
                  {appNumber !== null && appNumber !== (job.applicationNumber ?? '') && (
                    <Button type="submit" size="sm" variant="primary" loading={busy}>Save</Button>
                  )}
                </form>
              ) : (
                job.applicationNumber
              )}
            </Row>
            <Row label="Due">{formatDate(job.dueDate)}</Row>
            <Row label="Service fee">
              {formatAed(job.serviceFee)}
              {job.serviceFeeVatable && <span className="text-[#6B7280]"> + VAT</span>}
            </Row>
            {isStaff && <Row label="Est. gov. fees">{formatAed(job.estimatedGovernmentFee)}</Row>}
            <Row label="Source">{SOURCE_LABELS[job.source] ?? job.source}</Row>
            <Row label="Created">{formatDateTime(job.createdAt)}</Row>
            {job.completedAt && <Row label="Completed">{formatDateTime(job.completedAt)}</Row>}
            {isStaff && job.notes && <Row label="Internal notes"><span className="whitespace-pre-line">{job.notes}</span></Row>}
          </dl>
        </Section>

        {/* Steps */}
        <Section title={`Steps (${stepsDone}/${job.steps.length})`} icon={CircleDot}>
          {job.steps.length === 0 ? (
            <p className="text-sm text-[#6B7280]">This service has no steps.</p>
          ) : (
            <ol className="space-y-2">
              {job.steps.map((step) => {
                const stepStatus = STEP_STATUS[step.status] ?? STEP_STATUS.Pending
                const ref = stepRefs[step.id] ?? step.referenceNumber ?? ''
                const editable = isStaff && open && job.status !== 'OnHold'
                return (
                  <li key={step.id} className="rounded-[10px] border border-[#E2E4E9] p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-sm font-medium text-[#16181D]">
                        <span className="mr-2 tabular-nums text-[#9CA3AF]">{step.order}.</span>
                        {step.name}
                      </p>
                      <StatusPill label={stepStatus.label} tone={stepStatus.tone} />
                    </div>
                    {(step.referenceNumber || step.completedAt) && (
                      <p className="mt-1 text-xs text-[#6B7280]">
                        {step.referenceNumber && <>Ref {step.referenceNumber} · </>}
                        {step.completedAt && <>{formatDateTime(step.completedAt)}{step.completedByName ? ` by ${step.completedByName}` : ''}</>}
                      </p>
                    )}
                    {editable && (
                      <div className="mt-2 flex flex-wrap items-center gap-2">
                        {step.status === 'Pending' && (
                          <Input className="max-w-[180px] !py-1.5 text-xs" aria-label={`Reference for ${step.name}`}
                            placeholder="Ref / receipt no." value={ref}
                            onChange={(e) => setStepRefs((prev) => ({ ...prev, [step.id]: e.target.value }))} />
                        )}
                        {step.status === 'Pending' ? (
                          <>
                            <Button size="sm" variant="accent" icon={CheckCircle2} loading={busy}
                              onClick={() => run(() => updateJobStep(role, job.id, step.id, { status: 'Done', referenceNumber: ref || null }), null)}>
                              Done
                            </Button>
                            <Button size="sm" icon={SkipForward} loading={busy}
                              onClick={() => run(() => updateJobStep(role, job.id, step.id, { status: 'Skipped', referenceNumber: ref || null }), null)}>
                              Skip
                            </Button>
                          </>
                        ) : (
                          <Button size="sm" variant="ghost" icon={RotateCcw} loading={busy}
                            onClick={() => run(() => updateJobStep(role, job.id, step.id, { status: 'Pending' }), null)}>
                            Reopen
                          </Button>
                        )}
                      </div>
                    )}
                  </li>
                )
              })}
            </ol>
          )}
        </Section>

        {/* Document checklist */}
        <Section title="Documents to collect" icon={FileCheck2}>
          {job.documents.length === 0 ? (
            <p className="text-sm text-[#6B7280]">No documents listed for this service.</p>
          ) : (
            <ul className="space-y-1.5">
              {job.documents.map((doc) => (
                <li key={doc.id} className="flex items-start gap-2.5 text-sm">
                  <input
                    type="checkbox"
                    id={`doc-${doc.id}`}
                    className="mt-0.5 h-4 w-4 accent-[#0F9D74]"
                    checked={doc.isReceived}
                    disabled={!isStaff || !open || busy}
                    onChange={(e) => run(() => setJobDocument(role, job.id, doc.id, e.target.checked), null)}
                  />
                  <label htmlFor={`doc-${doc.id}`} className="text-[#16181D]">
                    {doc.name}
                    {!doc.isMandatory && <span className="text-[#9CA3AF]"> (if applicable)</span>}
                    {doc.receivedAt && <span className="block text-xs text-[#6B7280]">Received {formatDateTime(doc.receivedAt)}</span>}
                  </label>
                </li>
              ))}
            </ul>
          )}
        </Section>

        {/* Government fees */}
        <Section title="Government fees paid" icon={Landmark}
          action={job.governmentFees.length > 0 && <span className="text-sm font-semibold tabular-nums text-[#16181D]">{formatAed(govTotal)}</span>}>
          {job.governmentFees.length === 0 ? (
            <p className="text-sm text-[#6B7280]">None recorded yet.</p>
          ) : (
            <ul className="divide-y divide-[#E2E4E9] rounded-[10px] border border-[#E2E4E9]">
              {job.governmentFees.map((fee) => (
                <li key={fee.id} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                  <span className="min-w-0">
                    {fee.feeDescription}
                    <span className="block text-xs text-[#6B7280]">
                      {formatDate(fee.paidByFirmAt)}{fee.governmentReference ? ` · ${fee.governmentReference}` : ''}
                    </span>
                  </span>
                  <span className="shrink-0 tabular-nums">{formatAed(fee.amount)}</span>
                </li>
              ))}
            </ul>
          )}
          {isStaff && job.status !== 'Cancelled' && (
            <GovFeeForm busy={busy} onSubmit={(body, reset) => run(() => addJobGovFee(role, job.id, body), 'Government fee recorded.', reset)} />
          )}
        </Section>

        {/* Activity */}
        <Section title="Activity" icon={MessageSquare}>
          {isStaff && (
            <form
              className="mb-4 space-y-2"
              onSubmit={(e) => {
                e.preventDefault()
                if (!note.trim()) return
                run(() => addJobNote(role, job.id, note.trim(), noteInternal), 'Note added.', () => setNote(''))
              }}
            >
              <TextArea aria-label="Add a note" placeholder="Add a note…" value={note} maxLength={2000} onChange={(e) => setNote(e.target.value)} rows={2} />
              <div className="flex items-center justify-between gap-2">
                <label className="flex items-center gap-2 text-xs text-[#6B7280]">
                  <input type="checkbox" className="h-4 w-4 accent-[#0F9D74]" checked={!noteInternal}
                    onChange={(e) => setNoteInternal(!e.target.checked)} />
                  Share with the client (they get a notification)
                </label>
                <Button type="submit" size="sm" variant="primary" loading={busy} disabled={!note.trim()}>Add note</Button>
              </div>
            </form>
          )}
          <ol className="space-y-3">
            {job.activities.map((activity) => (
              <li key={activity.id} className="flex gap-3 text-sm">
                <UserRound size={15} className="mt-0.5 shrink-0 text-[#9CA3AF]" aria-hidden="true" />
                <div className="min-w-0">
                  <p className="text-[#16181D]">
                    <span className="font-medium">{activity.action}</span>
                    {activity.toStatus && activity.fromStatus && (
                      <span className="text-[#6B7280]"> · {JOB_STATUS[activity.fromStatus]?.label} → {JOB_STATUS[activity.toStatus]?.label}</span>
                    )}
                    {activity.isInternal && isStaff && <span className="ml-1.5 text-xs text-[#9CA3AF]">internal</span>}
                  </p>
                  {activity.note && <p className="whitespace-pre-line text-[#374151]">{activity.note}</p>}
                  <p className="text-xs text-[#9CA3AF]">
                    {formatDateTime(activity.createdAt)}{activity.userName ? ` · ${activity.userName}` : ''}
                  </p>
                </div>
              </li>
            ))}
          </ol>
        </Section>

        {dialog === 'hold' && (
          <ReasonDialog title="Put job on hold" confirmLabel="Put on hold" busy={busy} onClose={() => setDialog(null)}
            onSubmit={(reason) => run(() => holdJob(role, job.id, reason), 'Job put on hold.')} />
        )}
        {dialog === 'cancel' && (
          <ReasonDialog title="Cancel job" confirmLabel="Cancel job" tone="danger" busy={busy} onClose={() => setDialog(null)}
            onSubmit={(reason) => run(() => cancelJob(job.id, reason), 'Job cancelled.')} />
        )}
        {dialog === 'complete' && (
          <CompleteDialog job={job} busy={busy} onClose={() => setDialog(null)}
            onSubmit={(body) => run(() => completeJob(role, job.id, body), 'Job completed.')} />
        )}
      </div>
    )
  }

  return (
    <Drawer title={title} subtitle={job?.serviceName} icon={Briefcase} size="lg" onClose={onClose}>
      {body}
    </Drawer>
  )
}

export default JobDetailDrawer
