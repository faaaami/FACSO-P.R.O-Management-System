import { useState } from 'react'
import { Plus } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import JobsBoard from '../../components/serviceJobs/JobsBoard'
import NewJobDialog from '../../components/serviceJobs/NewJobDialog'
import { Button } from '../../components/serviceJobs/ui'

/** Admin: every job across customers — assign, track and work them. */
function AdminServiceJobsPage() {
  const [creating, setCreating] = useState(false)
  const [openJobId, setOpenJobId] = useState(null)

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Jobs"
        subtitle="Visa, Emirates ID, MOHRE, company and typing work for subscription and walk-in customers."
        action={<Button variant="primary" icon={Plus} onClick={() => setCreating(true)}>New job</Button>}
      />
      <JobsBoard role="Admin" openJobId={openJobId} onOpenJobHandled={() => setOpenJobId(null)} />
      {creating && <NewJobDialog onClose={() => setCreating(false)} onCreated={(job) => setOpenJobId(job.id)} />}
    </div>
  )
}

export default AdminServiceJobsPage
