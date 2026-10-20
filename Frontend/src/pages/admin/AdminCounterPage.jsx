import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Building2, Plus, Store, User, UserPlus, X } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import StatusPill from '../../components/client/StatusPill'
import CustomerPicker from '../../components/serviceJobs/CustomerPicker'
import WalkInCustomerForm from '../../components/serviceJobs/WalkInCustomerForm'
import NewJobDialog from '../../components/serviceJobs/NewJobDialog'
import JobDetailDrawer from '../../components/serviceJobs/JobDetailDrawer'
import { Button, Chip } from '../../components/serviceJobs/ui'
import { CUSTOMER_TYPE_LABELS, JOB_STATUS, formatDate } from '../../components/serviceJobs/labels'
import { listJobs } from '../../api/serviceJobs'

/**
 * Walk-in counter: find or register the customer, then open jobs for them
 * (typing, visa, Emirates ID...). Subscription customers can be served here too.
 */
function AdminCounterPage() {
  const [mode, setMode] = useState('find')
  const [customer, setCustomer] = useState(null)
  const [person, setPerson] = useState(null)
  const [newJob, setNewJob] = useState(null)
  const [openJobId, setOpenJobId] = useState(null)

  const jobsQuery = useQuery({
    queryKey: ['service-jobs', 'Admin', 'customer', customer?.clientCompanyId],
    queryFn: () => listJobs('Admin', { clientCompanyId: customer.clientCompanyId, pageSize: 10 }),
    enabled: Boolean(customer),
  })

  const pick = ({ customer: picked, person: pickedPerson }) => {
    setCustomer(picked)
    setPerson(pickedPerson)
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader title="Counter" subtitle="Serve walk-in customers: find or register them, then open the jobs they need." />

      {!customer ? (
        <SectionCard
          title={mode === 'find' ? 'Find the customer' : 'Register a walk-in customer'}
          icon={mode === 'find' ? Store : UserPlus}
          action={mode === 'find'
            ? <Button variant="primary" icon={UserPlus} onClick={() => setMode('register')}>New walk-in</Button>
            : <Button onClick={() => setMode('find')}>Back to search</Button>}
        >
          {mode === 'find' ? (
            <CustomerPicker autoFocus onPick={pick} />
          ) : (
            <WalkInCustomerForm
              onCancel={() => setMode('find')}
              onCreated={(created) => {
                setCustomer({
                  clientCompanyId: created.clientCompanyId,
                  customerCode: created.customerCode,
                  name: created.name,
                  customerType: created.customerType,
                })
                setPerson(created.personId ? { id: created.personId, fullName: created.name } : null)
                setMode('find')
              }}
            />
          )}
        </SectionCard>
      ) : (
        <>
          <SectionCard
            title={customer.name}
            icon={customer.customerType === 'WalkInIndividual' ? User : Building2}
            subtitle={[customer.customerCode, customer.phone].filter(Boolean).join(' · ')}
            badge={<Chip tone={customer.customerType === 'Subscription' ? 'accent' : 'neutral'}>{CUSTOMER_TYPE_LABELS[customer.customerType]}</Chip>}
            action={<Button icon={X} onClick={() => { setCustomer(null); setPerson(null) }}>Next customer</Button>}
          >
            {person && <p className="mb-4 text-sm text-[#6B7280]">Serving: <span className="font-medium text-[#16181D]">{person.fullName}</span></p>}
            <div className="flex flex-wrap gap-2">
              <Button variant="primary" icon={Plus} onClick={() => setNewJob({ counterOnly: true })}>Typing / counter service</Button>
              <Button icon={Plus} onClick={() => setNewJob({ counterOnly: false })}>Visa, Emirates ID or other service</Button>
            </div>
          </SectionCard>

          <SectionCard title="This customer's jobs" subtitle="Most recent first.">
            {jobsQuery.isLoading ? (
              <p className="text-sm text-[#6B7280]">Loading…</p>
            ) : (jobsQuery.data?.items ?? []).length === 0 ? (
              <p className="text-sm text-[#6B7280]">No jobs yet.</p>
            ) : (
              <ul className="divide-y divide-[#E2E4E9]">
                {jobsQuery.data.items.map((job) => {
                  const status = JOB_STATUS[job.status]
                  return (
                    <li key={job.id}>
                      <button type="button" onClick={() => setOpenJobId(job.id)}
                        className="flex w-full cursor-pointer items-center justify-between gap-3 py-2.5 text-left hover:bg-[#F7F8FA]">
                        <span>
                          <span className="block text-sm font-medium text-[#16181D]">{job.jobNumber} · {job.serviceName}</span>
                          <span className="block text-xs text-[#6B7280]">{job.personName ?? job.entityName ?? ''} · created {formatDate(job.createdAt)}</span>
                        </span>
                        <StatusPill label={status?.label ?? job.status} tone={status?.tone} />
                      </button>
                    </li>
                  )
                })}
              </ul>
            )}
          </SectionCard>
        </>
      )}

      {newJob && customer && (
        <NewJobDialog
          initialCustomer={customer}
          initialPerson={person}
          source="Counter"
          counterOnly={newJob.counterOnly}
          onClose={() => setNewJob(null)}
          onCreated={(job) => setOpenJobId(job.id)}
        />
      )}
      {openJobId && <JobDetailDrawer role="Admin" jobId={openJobId} onClose={() => setOpenJobId(null)} />}
    </div>
  )
}

export default AdminCounterPage
