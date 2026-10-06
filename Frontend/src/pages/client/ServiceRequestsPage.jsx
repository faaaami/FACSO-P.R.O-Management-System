import { useState } from 'react'
import { Calendar, ClipboardList } from 'lucide-react'
import { format } from 'date-fns'
import ServiceRequestSummaryCards from '../../components/client/serviceRequests/ServiceRequestSummaryCards'
import ServiceRequestFilters from '../../components/client/serviceRequests/ServiceRequestFilters'
import ServiceRequestTable from '../../components/client/serviceRequests/ServiceRequestTable'
import ServiceRequestCreatePanel from '../../components/client/serviceRequests/ServiceRequestCreatePanel'
import ServiceRequestDetailDrawer from '../../components/client/serviceRequests/ServiceRequestDetailDrawer'
import DocumentDetailDrawer from '../../components/client/documents/DocumentDetailDrawer'
import EmployeeDocumentsDrawer from '../../components/client/documents/EmployeeDocumentsDrawer'
import { useClientServiceRequests } from '../../hooks/client/useClientServiceRequests'

const PAGE_SIZE = 10

function buildParams(filters, page) {
  const params = { page, pageSize: PAGE_SIZE }
  if (filters.status) params.status = filters.status
  return params
}

function hasActiveFilters(filters) {
  return Boolean(filters.status)
}

function ServiceRequestsPage() {
  const [filters, setFilters] = useState({})
  const [page, setPage] = useState(1)
  const [selectedRequestId, setSelectedRequestId] = useState(null)
  const [selectedDocumentId, setSelectedDocumentId] = useState(null)
  const [employeeId, setEmployeeId] = useState(null)

  const { data: totalData } = useClientServiceRequests({ page: 1, pageSize: 1 })
  const { data: submittedData } = useClientServiceRequests({ page: 1, pageSize: 1, status: 'Submitted' })
  const { data: convertedData } = useClientServiceRequests({ page: 1, pageSize: 1, status: 'Converted' })
  const { data: rejectedData } = useClientServiceRequests({ page: 1, pageSize: 1, status: 'Rejected' })
  const {
    data: requests,
    isLoading: requestsLoading,
    isError: requestsError,
    refetch: requestsRefetch,
  } = useClientServiceRequests(buildParams(filters, page))

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  const updateFilters = (next) => {
    setFilters(next)
    setPage(1)
  }

  const clearFilters = () => {
    setFilters({})
    setPage(1)
  }

  const openRequest = (requestId) => setSelectedRequestId(requestId)
  const closeRequest = () => setSelectedRequestId(null)

  const openDocument = (documentIdValue) => {
    setSelectedDocumentId(documentIdValue)
  }

  const closeDocument = () => {
    setSelectedDocumentId(null)
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <header className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Service Requests</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <ClipboardList size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Submit document and renewal service requests. Track each request until the PRO team converts or rejects it.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

      {/* Summary cards */}
      <ServiceRequestSummaryCards
        total={totalData?.totalCount ?? 0}
        submitted={submittedData?.totalCount ?? 0}
        converted={convertedData?.totalCount ?? 0}
        rejected={rejectedData?.totalCount ?? 0}
      />

      {/* Main content grid */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-8">
          <ServiceRequestFilters
            filters={filters}
            onChange={updateFilters}
            onClear={clearFilters}
            isDirty={hasActiveFilters(filters)}
          />
          <ServiceRequestTable
            data={requests}
            isLoading={requestsLoading}
            isError={requestsError}
            onRetry={() => requestsRefetch()}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onSelect={openRequest}
          />
        </div>

        <div className="lg:col-span-4">
          <ServiceRequestCreatePanel />
        </div>
      </div>

      {/* Detail drawer */}
      {selectedRequestId && (
        <ServiceRequestDetailDrawer
          requestId={selectedRequestId}
          onClose={closeRequest}
          onOpenEmployeeDocuments={setEmployeeId}
        />
      )}

      {/* Employee documents drawer */}
      {employeeId && (
        <EmployeeDocumentsDrawer
          employeeId={employeeId}
          onClose={() => setEmployeeId(null)}
          onSelectDocument={(documentIdValue) => {
            setEmployeeId(null)
            openDocument(documentIdValue)
          }}
        />
      )}

      {/* Document detail drawer */}
      {selectedDocumentId && (
        <DocumentDetailDrawer
          documentId={selectedDocumentId}
          onClose={closeDocument}
          onOpenEmployeeDocuments={setEmployeeId}

        />
      )}
    </div>
  )
}

export default ServiceRequestsPage