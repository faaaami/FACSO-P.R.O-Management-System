import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Plus, UserPlus, X } from 'lucide-react'
import { listServices } from '../../api/serviceCatalogue'
import { createPerson, listCustomerPersons } from '../../api/customers'
import { createJob } from '../../api/serviceJobs'
import { getAdminClientEntities } from '../../api/admin/clients'
import { getAdminEmployeeDocuments } from '../../api/admin/employees'
import { getAdminEntityDocuments } from '../../api/admin/documents'
import { extractApiErrorMessage } from '../../utils/apiError'
import CustomerPicker from './CustomerPicker'
import StaffSelect from './StaffSelect'
import {
  CATEGORIES,
  CATEGORY_LABELS,
  CUSTOMER_TYPE_LABELS,
  DOCUMENT_TYPE_LABELS,
  PERSON_TYPE_LABELS,
  formatAed,
  formatDate,
} from './labels'
import { Button, Chip, Field, Input, Modal, Select, TextArea } from './ui'

function QuickPersonForm({ entityId, customerId, onCreated, onCancel }) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState({ personType: 'Employee', fullName: '', passportNumber: '', nationality: '', mobile: '' })
  const set = (key) => (e) => setForm((prev) => ({ ...prev, [key]: e.target.value }))

  const mutation = useMutation({
    mutationFn: () => createPerson(entityId, {
      personType: form.personType,
      fullName: form.fullName.trim(),
      passportNumber: form.passportNumber.trim() || null,
      nationality: form.nationality.trim() || null,
      mobile: form.mobile.trim() || null,
    }),
    onSuccess: (person) => {
      queryClient.invalidateQueries({ queryKey: ['customers', customerId, 'persons'] })
      toast.success(`${person.fullName} added.`)
      onCreated(person)
    },
    onError: (error) => toast.error(extractApiErrorMessage(error, 'Could not add the person.')),
  })

  return (
    <div className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-3">
      <div className="grid gap-2 sm:grid-cols-2">
        <Select aria-label="Person type" value={form.personType} onChange={set('personType')}>
          {['Employee', 'PartnerInvestor', 'DomesticWorker'].map((t) => <option key={t} value={t}>{PERSON_TYPE_LABELS[t]}</option>)}
        </Select>
        <Input aria-label="Full name" placeholder="Full name (as in passport)" value={form.fullName} onChange={set('fullName')} />
        <Input aria-label="Passport number" placeholder="Passport number" value={form.passportNumber} onChange={set('passportNumber')} />
        <Input aria-label="Nationality" placeholder="Nationality" value={form.nationality} onChange={set('nationality')} />
        <Input aria-label="Mobile" placeholder="Mobile" value={form.mobile} onChange={set('mobile')} />
      </div>
      <p className="mt-2 text-xs text-[#6B7280]">Dependents are added from the customer&apos;s file (they need a sponsor).</p>
      <div className="mt-2 flex justify-end gap-2">
        <Button size="sm" onClick={onCancel}>Cancel</Button>
        <Button size="sm" variant="primary" loading={mutation.isPending} disabled={!form.fullName.trim()} onClick={() => mutation.mutate()}>
          Add person
        </Button>
      </div>
    </div>
  )
}

/**
 * Creates a job. Used from the Jobs board and the walk-in counter.
 * `initialCustomer` / `initialPerson` preselect the customer (and person).
 */
function NewJobDialog({ onClose, onCreated, initialCustomer = null, initialPerson = null, source = 'Staff', counterOnly = false }) {
  const queryClient = useQueryClient()
  const [customer, setCustomer] = useState(initialCustomer)
  const [personId, setPersonId] = useState(initialPerson?.id ?? '')
  const [entityId, setEntityId] = useState('')
  const [serviceId, setServiceId] = useState('')
  const [documentId, setDocumentId] = useState('')
  const [category, setCategory] = useState(counterOnly ? 'Typing' : '')
  const [fee, setFee] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [priority, setPriority] = useState('Normal')
  const [assignee, setAssignee] = useState(null)
  const [notes, setNotes] = useState('')
  const [addingPerson, setAddingPerson] = useState(false)

  const servicesQuery = useQuery({
    queryKey: ['admin', 'services', 'active'],
    queryFn: () => listServices({}),
    staleTime: 60_000,
  })
  const customerId = customer?.clientCompanyId
  const personsQuery = useQuery({
    queryKey: ['customers', customerId, 'persons'],
    queryFn: () => listCustomerPersons(customerId),
    enabled: Boolean(customerId),
  })
  const entitiesQuery = useQuery({
    queryKey: ['admin', 'client-entities', customerId],
    queryFn: () => getAdminClientEntities(customerId, { pageSize: 100 }),
    enabled: Boolean(customerId),
  })

  const services = servicesQuery.data ?? []
  const service = services.find((s) => s.id === serviceId)
  const visibleServices = category ? services.filter((s) => s.category === category) : services
  const persons = personsQuery.data ?? []
  const entities = entitiesQuery.data?.items ?? []
  const effectiveEntityId = entityId || (entities.length === 1 ? entities[0].id : '')

  const documentsQuery = useQuery({
    queryKey: ['job-renewal-docs', service?.renewsDocumentType, personId, effectiveEntityId],
    queryFn: async () => {
      const result = personId
        ? await getAdminEmployeeDocuments(personId)
        : await getAdminEntityDocuments(effectiveEntityId)
      return (result?.items ?? []).filter((d) => d.type === service.renewsDocumentType && !d.isDeleted)
    },
    enabled: Boolean(service?.renewsDocumentType && (personId || effectiveEntityId)),
  })
  const renewalDocs = documentsQuery.data ?? []

  const grouped = useMemo(() => {
    const map = new Map()
    for (const s of visibleServices) {
      if (!map.has(s.category)) map.set(s.category, [])
      map.get(s.category).push(s)
    }
    return [...map.entries()]
  }, [visibleServices])

  const mutation = useMutation({
    mutationFn: () => createJob({
      clientCompanyId: customerId,
      serviceDefinitionId: serviceId,
      employeeId: personId || null,
      clientEntityId: service?.subjectType === 'Company' ? effectiveEntityId || null : null,
      documentId: documentId || null,
      assignedStaffId: assignee,
      priority,
      source,
      serviceFee: fee === '' ? null : Number(fee),
      dueDate: dueDate || null,
      notes: notes.trim() || null,
    }),
    onSuccess: (job) => {
      queryClient.invalidateQueries({ queryKey: ['service-jobs'] })
      toast.success(`${job.jobNumber} created.`)
      onCreated?.(job)
      onClose()
    },
    onError: (error) => toast.error(extractApiErrorMessage(error, 'The job could not be created.')),
  })

  const needsPerson = service?.subjectType === 'Person'
  const needsEntity = service?.subjectType === 'Company'
  const needsDocument = Boolean(service?.renewsDocumentType)
  const valid = customerId && serviceId &&
    (!needsPerson || personId) &&
    (!needsEntity || effectiveEntityId)

  return (
    <Modal
      title="New job"
      subtitle="Pick the customer, the service and who it is for."
      size="lg"
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Cancel</Button>
          <Button variant="primary" icon={Plus} loading={mutation.isPending} disabled={!valid} onClick={() => mutation.mutate()}>
            Create job
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {/* Customer */}
        {customer ? (
          <div className="flex items-start justify-between gap-3 rounded-[10px] border border-[#E2E4E9] p-3">
            <div>
              <p className="text-sm font-semibold text-[#16181D]">{customer.name}</p>
              <p className="text-xs text-[#6B7280]">
                {[customer.customerCode, CUSTOMER_TYPE_LABELS[customer.customerType]].filter(Boolean).join(' · ')}
              </p>
            </div>
            {!initialCustomer && (
              <Button size="sm" variant="ghost" icon={X} onClick={() => { setCustomer(null); setPersonId(''); setEntityId(''); setDocumentId('') }}>
                Change
              </Button>
            )}
          </div>
        ) : (
          <Field label="Customer" required>
            <CustomerPicker autoFocus onPick={({ customer: picked, person }) => { setCustomer(picked); setPersonId(person?.id ?? '') }} />
          </Field>
        )}

        {/* Service */}
        <div className="grid gap-3 sm:grid-cols-[200px_1fr]">
          <Field label="Category">
            {(id) => (
              <Select id={id} value={category} onChange={(e) => { setCategory(e.target.value); setServiceId('') }}>
                <option value="">All categories</option>
                {CATEGORIES.map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
              </Select>
            )}
          </Field>
          <Field label="Service" required
            hint={service ? `${service.authority ?? ''}${service.estimatedDays ? ` · about ${service.estimatedDays} day(s)` : ''} · default fee ${formatAed(service.defaultServiceFee)}` : undefined}>
            {(id) => (
              <Select id={id} value={serviceId} onChange={(e) => { setServiceId(e.target.value); setDocumentId('') }}>
                <option value="">{servicesQuery.isLoading ? 'Loading services…' : 'Choose a service'}</option>
                {grouped.map(([cat, items]) => (
                  <optgroup key={cat} label={CATEGORY_LABELS[cat] ?? cat}>
                    {items.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                  </optgroup>
                ))}
              </Select>
            )}
          </Field>
        </div>

        {/* Subject */}
        {customerId && service && service.subjectType !== 'None' && (
          <div className="grid gap-3 sm:grid-cols-2">
            {needsPerson && (
              <Field label="Person" required>
                {(id) => (
                  <div className="space-y-2">
                    <div className="flex gap-2">
                      <Select id={id} value={personId} onChange={(e) => { setPersonId(e.target.value); setDocumentId('') }}>
                        <option value="">{personsQuery.isLoading ? 'Loading…' : 'Choose a person'}</option>
                        {persons.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.fullName} · {PERSON_TYPE_LABELS[p.personType]}{p.passportNumber ? ` · ${p.passportNumber}` : ''}
                          </option>
                        ))}
                      </Select>
                      {entities.length > 0 && !addingPerson && (
                        <Button icon={UserPlus} aria-label="Add a new person" onClick={() => setAddingPerson(true)} />
                      )}
                    </div>
                    {addingPerson && !effectiveEntityId && (
                      <p className="text-xs text-[#6B7280]">Choose the licence / entity the person belongs to.</p>
                    )}
                    {addingPerson && effectiveEntityId && (
                      <QuickPersonForm entityId={effectiveEntityId} customerId={customerId}
                        onCancel={() => setAddingPerson(false)}
                        onCreated={(p) => { setPersonId(p.id); setAddingPerson(false) }} />
                    )}
                  </div>
                )}
              </Field>
            )}
            {(needsEntity || (needsPerson && entities.length > 1 && addingPerson)) && (
              <Field label="Licence / entity" required={needsEntity}>
                {(id) => (
                  <Select id={id} value={effectiveEntityId} onChange={(e) => setEntityId(e.target.value)}>
                    <option value="">Choose…</option>
                    {entities.map((e) => <option key={e.id} value={e.id}>{e.entityName}{e.tradeLicenseNumber ? ` · ${e.tradeLicenseNumber}` : ''}</option>)}
                  </Select>
                )}
              </Field>
            )}
            {needsDocument && (personId || effectiveEntityId) && (
              <Field label={`${DOCUMENT_TYPE_LABELS[service.renewsDocumentType] ?? 'Document'} to renew`}
                hint={renewalDocs.length === 0 && !documentsQuery.isLoading
                  ? 'None on file. You can still create the job; register the document later to track its expiry.'
                  : 'Linking it updates its expiry when the job is completed.'}>
                {(id) => (
                  <Select id={id} value={documentId} onChange={(e) => setDocumentId(e.target.value)}>
                    <option value="">{documentsQuery.isLoading ? 'Loading…' : 'Choose the document'}</option>
                    {renewalDocs.map((d) => <option key={d.id} value={d.id}>{d.documentNumber} · expires {formatDate(d.expiryDate)}</option>)}
                  </Select>
                )}
              </Field>
            )}
          </div>
        )}

        {/* Terms */}
        <div className="grid gap-3 sm:grid-cols-4">
          <Field label="Service fee (AED)" hint={customer?.customerType === 'Subscription' ? 'Blank = contract price' : 'Blank = default fee'}>
            {(id) => <Input id={id} type="number" min="0" step="0.01" value={fee} placeholder={service ? String(service.defaultServiceFee) : ''} onChange={(e) => setFee(e.target.value)} />}
          </Field>
          <Field label="Due date" hint="Blank = estimated days">
            {(id) => <Input id={id} type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />}
          </Field>
          <Field label="Priority">
            {(id) => (
              <Select id={id} value={priority} onChange={(e) => setPriority(e.target.value)}>
                <option value="Normal">Normal</option>
                <option value="Urgent">Urgent</option>
              </Select>
            )}
          </Field>
          <Field label="Assign to">
            {(id) => <StaffSelect id={id} value={assignee} onChange={setAssignee} />}
          </Field>
        </div>

        <Field label="Internal notes">
          {(id) => <TextArea id={id} rows={2} value={notes} maxLength={4000} onChange={(e) => setNotes(e.target.value)} />}
        </Field>

        {service && (
          <div className="flex flex-wrap gap-1.5">
            <Chip tone="accent">{CATEGORY_LABELS[service.category]}</Chip>
            <Chip>{service.stepCount} step(s)</Chip>
            <Chip>{service.requiredDocumentCount} document(s) to collect</Chip>
            {service.serviceFeeVatable && <Chip>Fee + 5% VAT</Chip>}
          </div>
        )}
      </div>
    </Modal>
  )
}

export default NewJobDialog
