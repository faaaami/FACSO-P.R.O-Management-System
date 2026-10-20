import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Briefcase } from 'lucide-react'
import { listServices } from '../../api/serviceCatalogue'
import { convertRequestToJob } from '../../api/serviceJobs'
import { extractApiErrorMessage } from '../../utils/apiError'
import StaffSelect from './StaffSelect'
import { CATEGORY_LABELS, formatAed } from './labels'
import { Button, Field, Input, Modal, Select, TextArea } from './ui'

/** Admin: turn a client's service request into a job for any catalogue service. */
function ConvertRequestToJobDialog({ request, onClose, onConverted }) {
  const queryClient = useQueryClient()
  const [serviceId, setServiceId] = useState(request.serviceDefinitionId ?? '')
  const [priority, setPriority] = useState('Normal')
  const [assignee, setAssignee] = useState(null)
  const [fee, setFee] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [notes, setNotes] = useState('')

  const servicesQuery = useQuery({
    queryKey: ['admin', 'services', 'active'],
    queryFn: () => listServices({}),
    staleTime: 60_000,
  })
  const services = useMemo(() => servicesQuery.data ?? [], [servicesQuery.data])
  const service = services.find((s) => s.id === serviceId)
  const grouped = useMemo(() => {
    const map = new Map()
    for (const s of services) {
      if (!map.has(s.category)) map.set(s.category, [])
      map.get(s.category).push(s)
    }
    return [...map.entries()]
  }, [services])

  const mutation = useMutation({
    mutationFn: () => convertRequestToJob(request.id, {
      serviceDefinitionId: serviceId,
      assignedStaffId: assignee,
      priority,
      serviceFee: fee === '' ? null : Number(fee),
      dueDate: dueDate || null,
      notes: notes.trim() || null,
    }),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'service-requests'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'service-request', request.id] })
      queryClient.invalidateQueries({ queryKey: ['service-jobs'] })
      toast.success(`Request converted to ${result.jobNumber}.`)
      onConverted?.(result)
      onClose()
    },
    onError: (error) => toast.error(extractApiErrorMessage(error, 'The request could not be converted.')),
  })

  return (
    <Modal
      title="Convert to job"
      subtitle="The client is notified with the job number and can follow its progress."
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Cancel</Button>
          <Button variant="accent" icon={Briefcase} loading={mutation.isPending} disabled={!serviceId} onClick={() => mutation.mutate()}>
            Create job
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Service" required hint={service ? `Default fee ${formatAed(service.defaultServiceFee)} (contract price applies if set)` : undefined}>
          {(id) => (
            <Select id={id} value={serviceId} onChange={(e) => setServiceId(e.target.value)}>
              <option value="">{servicesQuery.isLoading ? 'Loading…' : 'Choose a service'}</option>
              {grouped.map(([cat, items]) => (
                <optgroup key={cat} label={CATEGORY_LABELS[cat] ?? cat}>
                  {items.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                </optgroup>
              ))}
            </Select>
          )}
        </Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Assign to">{(id) => <StaffSelect id={id} value={assignee} onChange={setAssignee} />}</Field>
          <Field label="Priority">
            {(id) => (
              <Select id={id} value={priority} onChange={(e) => setPriority(e.target.value)}>
                <option value="Normal">Normal</option>
                <option value="Urgent">Urgent</option>
              </Select>
            )}
          </Field>
          <Field label="Service fee override (AED)">
            {(id) => <Input id={id} type="number" min="0" step="0.01" value={fee} onChange={(e) => setFee(e.target.value)} />}
          </Field>
          <Field label="Due date">
            {(id) => <Input id={id} type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />}
          </Field>
        </div>
        <Field label="Internal notes" hint="The client's request notes are copied to the job automatically.">
          {(id) => <TextArea id={id} rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />}
        </Field>
      </div>
    </Modal>
  )
}

export default ConvertRequestToJobDialog
