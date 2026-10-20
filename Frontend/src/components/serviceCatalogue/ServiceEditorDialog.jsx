import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ArrowDown, ArrowUp, Plus, Save, Trash2 } from 'lucide-react'
import { createService, getService, updateService } from '../../api/serviceCatalogue'
import { extractApiErrorMessage } from '../../utils/apiError'
import { CATEGORIES, CATEGORY_LABELS, DOCUMENT_TYPE_LABELS, SUBJECT_LABELS } from '../serviceJobs/labels'
import { Button, Field, Input, Modal, Select, TextArea } from '../serviceJobs/ui'
import LoadingState from '../client/LoadingState'

const EMPTY = {
  code: '', name: '', nameArabic: '', category: 'Visa', authority: '', description: '', subjectType: 'Person',
  defaultServiceFee: 0, estimatedGovernmentFee: 0, serviceFeeVatable: true, estimatedDays: '',
  isCounterService: false, renewsDocumentType: '', sortOrder: 0, steps: [], requiredDocuments: [],
}

function ListEditor({ label, items, onChange, render, empty, newItem }) {
  const move = (index, delta) => {
    const next = [...items]
    const [item] = next.splice(index, 1)
    next.splice(index + delta, 0, item)
    onChange(next)
  }
  return (
    <div>
      <div className="mb-2 flex items-center justify-between">
        <p className="text-xs font-semibold text-[#16181D]">{label}</p>
        <Button size="sm" icon={Plus} onClick={() => onChange([...items, newItem()])}>Add</Button>
      </div>
      {items.length === 0 ? (
        <p className="rounded-[10px] border border-dashed border-[#E2E4E9] px-3 py-3 text-xs text-[#6B7280]">{empty}</p>
      ) : (
        <ol className="space-y-2">
          {items.map((item, index) => (
            <li key={item.key} className="flex items-start gap-2">
              <span className="mt-2 w-5 text-right text-xs tabular-nums text-[#9CA3AF]">{index + 1}.</span>
              <div className="min-w-0 flex-1">{render(item, (patch) => onChange(items.map((x, i) => (i === index ? { ...x, ...patch } : x))))}</div>
              <div className="flex shrink-0 gap-0.5">
                <Button size="sm" variant="ghost" icon={ArrowUp} aria-label="Move up" disabled={index === 0} onClick={() => move(index, -1)} />
                <Button size="sm" variant="ghost" icon={ArrowDown} aria-label="Move down" disabled={index === items.length - 1} onClick={() => move(index, 1)} />
                <Button size="sm" variant="ghost" icon={Trash2} aria-label="Remove" onClick={() => onChange(items.filter((_, i) => i !== index))} />
              </div>
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}

let keySeed = 0
const withKeys = (list) => list.map((x) => ({ ...x, key: `k${++keySeed}` }))

function Editor({ initial, serviceId, onClose }) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState(initial)
  const set = (key) => (e) => setForm((prev) => ({ ...prev, [key]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }))

  const mutation = useMutation({
    mutationFn: () => {
      const body = {
        ...form,
        code: form.code.trim(),
        name: form.name.trim(),
        nameArabic: form.nameArabic?.trim() || null,
        authority: form.authority?.trim() || null,
        description: form.description?.trim() || null,
        defaultServiceFee: Number(form.defaultServiceFee) || 0,
        estimatedGovernmentFee: Number(form.estimatedGovernmentFee) || 0,
        estimatedDays: form.estimatedDays === '' || form.estimatedDays === null ? null : Number(form.estimatedDays),
        renewsDocumentType: form.renewsDocumentType || null,
        sortOrder: Number(form.sortOrder) || 0,
        steps: form.steps.filter((s) => s.name.trim()).map((s, i) => ({ order: i + 1, name: s.name.trim(), description: s.description?.trim() || null })),
        requiredDocuments: form.requiredDocuments.filter((d) => d.name.trim()).map((d, i) => ({ order: i + 1, name: d.name.trim(), isMandatory: d.isMandatory })),
      }
      return serviceId ? updateService(serviceId, body) : createService(body)
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'services'] })
      toast.success(`${saved.name} saved.`)
      onClose()
    },
    onError: (error) => toast.error(extractApiErrorMessage(error, 'The service could not be saved.')),
  })

  const valid = form.code.trim() && form.name.trim()

  return (
    <Modal
      title={serviceId ? `Edit ${initial.name}` : 'New service'}
      subtitle="Changes apply to new jobs. Existing jobs keep their steps and checklist."
      size="lg"
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Cancel</Button>
          <Button variant="primary" icon={Save} loading={mutation.isPending} disabled={!valid} onClick={() => mutation.mutate()}>Save service</Button>
        </>
      }
    >
      <div className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Code" required hint="Letters, digits and dashes">
            {(id) => <Input id={id} value={form.code} onChange={set('code')} placeholder="VISA-EMP-NEW" />}
          </Field>
          <Field label="Name" required className="sm:col-span-2">
            {(id) => <Input id={id} value={form.name} onChange={set('name')} />}
          </Field>
          <Field label="Arabic name">
            {(id) => <Input id={id} dir="rtl" value={form.nameArabic ?? ''} onChange={set('nameArabic')} />}
          </Field>
          <Field label="Category">
            {(id) => (
              <Select id={id} value={form.category} onChange={set('category')}>
                {CATEGORIES.map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
              </Select>
            )}
          </Field>
          <Field label="Authority">
            {(id) => <Input id={id} value={form.authority ?? ''} onChange={set('authority')} placeholder="ICP / GDRFA / MOHRE / DED" />}
          </Field>
          <Field label="Done for">
            {(id) => (
              <Select id={id} value={form.subjectType} onChange={set('subjectType')}>
                {Object.entries(SUBJECT_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </Select>
            )}
          </Field>
          <Field label="Renews document" hint="Completing the job updates its expiry">
            {(id) => (
              <Select id={id} value={form.renewsDocumentType ?? ''} onChange={set('renewsDocumentType')}>
                <option value="">— (not a renewal)</option>
                {Object.entries(DOCUMENT_TYPE_LABELS).filter(([v]) => v !== 'Other').map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </Select>
            )}
          </Field>
          <Field label="Typical duration (days)">
            {(id) => <Input id={id} type="number" min="0" value={form.estimatedDays ?? ''} onChange={set('estimatedDays')} />}
          </Field>
        </div>

        <div className="grid gap-3 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-4 sm:grid-cols-3">
          <Field label="Service fee (AED, before VAT)">
            {(id) => <Input id={id} type="number" min="0" step="0.01" value={form.defaultServiceFee} onChange={set('defaultServiceFee')} />}
          </Field>
          <Field label="Typical government fees (AED)" hint="For quotes only; actual fees are recorded per job">
            {(id) => <Input id={id} type="number" min="0" step="0.01" value={form.estimatedGovernmentFee} onChange={set('estimatedGovernmentFee')} />}
          </Field>
          <div className="space-y-2 pt-5">
            <label className="flex items-center gap-2 text-sm text-[#16181D]">
              <input type="checkbox" className="h-4 w-4 accent-[#0F9D74]" checked={form.serviceFeeVatable} onChange={set('serviceFeeVatable')} />
              Service fee carries 5% VAT
            </label>
            <label className="flex items-center gap-2 text-sm text-[#16181D]">
              <input type="checkbox" className="h-4 w-4 accent-[#0F9D74]" checked={form.isCounterService} onChange={set('isCounterService')} />
              Quick counter service
            </label>
          </div>
        </div>

        <Field label="Description">
          {(id) => <TextArea id={id} rows={2} value={form.description ?? ''} onChange={set('description')} />}
        </Field>

        <div className="grid gap-5 lg:grid-cols-2">
          <ListEditor
            label="Steps"
            items={form.steps}
            onChange={(steps) => setForm((prev) => ({ ...prev, steps }))}
            empty="No steps. Jobs can be completed straight away."
            newItem={() => ({ key: `k${++keySeed}`, name: '', description: '' })}
            render={(item, update) => (
              <Input aria-label="Step name" value={item.name} placeholder="e.g. Entry permit issued" onChange={(e) => update({ name: e.target.value })} />
            )}
          />
          <ListEditor
            label="Documents to collect"
            items={form.requiredDocuments}
            onChange={(requiredDocuments) => setForm((prev) => ({ ...prev, requiredDocuments }))}
            empty="No documents listed."
            newItem={() => ({ key: `k${++keySeed}`, name: '', isMandatory: true })}
            render={(item, update) => (
              <div className="flex items-center gap-2">
                <Input aria-label="Document name" value={item.name} placeholder="e.g. Passport copy" onChange={(e) => update({ name: e.target.value })} />
                <label className="flex shrink-0 items-center gap-1 text-xs text-[#6B7280]">
                  <input type="checkbox" className="h-3.5 w-3.5 accent-[#0F9D74]" checked={item.isMandatory} onChange={(e) => update({ isMandatory: e.target.checked })} />
                  Required
                </label>
              </div>
            )}
          />
        </div>
      </div>
    </Modal>
  )
}

/** Create (no serviceId) or edit (serviceId) a catalogue service. */
function ServiceEditorDialog({ serviceId, onClose }) {
  const query = useQuery({
    queryKey: ['admin', 'services', 'detail', serviceId],
    queryFn: () => getService(serviceId),
    enabled: Boolean(serviceId),
  })

  if (!serviceId) {
    return <Editor initial={EMPTY} onClose={onClose} />
  }

  if (!query.data) {
    return (
      <Modal title="Edit service" onClose={onClose}>
        {query.isError ? <p className="text-sm text-[#DC2626]">{extractApiErrorMessage(query.error, 'Could not load the service.')}</p> : <LoadingState />}
      </Modal>
    )
  }

  const s = query.data
  return (
    <Editor
      serviceId={serviceId}
      onClose={onClose}
      initial={{
        ...s,
        nameArabic: s.nameArabic ?? '',
        authority: s.authority ?? '',
        description: s.description ?? '',
        estimatedDays: s.estimatedDays ?? '',
        renewsDocumentType: s.renewsDocumentType ?? '',
        steps: withKeys(s.steps),
        requiredDocuments: withKeys(s.requiredDocuments),
      }}
    />
  )
}

export default ServiceEditorDialog
