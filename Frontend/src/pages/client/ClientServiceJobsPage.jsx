import JobsBoard from '../../components/serviceJobs/JobsBoard'

/** Client: progress of the services the agency handles for the company. */
function ClientServiceJobsPage() {
  return (
    <div className="space-y-6">
      <div className="border-b border-[#E2E4E9] pb-5">
        <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Jobs</h1>
        <p className="mt-1.5 text-sm text-[#6B7280]">
          Every visa, Emirates ID, labour and company service we are handling for you, step by step.
        </p>
      </div>
      <JobsBoard role="Client" title="Your jobs" />
    </div>
  )
}

export default ClientServiceJobsPage
