import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import JobsBoard from '../../components/serviceJobs/JobsBoard'

/** Agent: jobs assigned to me. */
function AgentServiceJobsPage() {
  return (
    <div className="space-y-6">
      <AgentPageHeader title="My jobs" description="Work through the steps, record application numbers and government fees." />
      <JobsBoard role="Agent" title="Assigned to me" />
    </div>
  )
}

export default AgentServiceJobsPage
