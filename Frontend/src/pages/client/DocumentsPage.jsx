import { useState } from 'react'
import { Calendar, FileText } from 'lucide-react'
import { format } from 'date-fns'
import SummaryCards from '../../components/client/documents/SummaryCards'
import DocumentFilters from '../../components/client/documents/DocumentFilters'
import DocumentTable from '../../components/client/documents/DocumentTable'
import DocumentDetailDrawer from '../../components/client/documents/DocumentDetailDrawer'
import EmployeeDocumentsDrawer from '../../components/client/documents/EmployeeDocumentsDrawer'
import ExpiringDocumentsPanel from '../../components/client/documents/ExpiringDocumentsPanel'
import { useClientDocuments } from '../../hooks/client/useClientDocuments'
import { useClientExpiringDocuments } from '../../hooks/client/useClientExpiringDocuments'

const PAGE_SIZE = 10

function buildParams(filters, page) {
  const params = { page, pageSize: PAGE_SIZE }
  if (filters.type) params.type = filters.type
  if (filters.status) params.status = filters.status
  if (filters.ownerType) params.ownerType = filters.ownerType
  if (filters.expiresFrom) params.expiresFrom = filters.expiresFrom
  if (filters.expiresTo) params.expiresTo = filters.expiresTo
  return params
}

function hasActiveFilters(filters) {
  return Boolean(
    filters.type ||
      filters.status ||
      filters.ownerType ||
      filters.expiresFrom ||
      filters.expiresTo
  )
}

function DocumentsPage() {
  const [filters, setFilters] = useState({})
  const [page, setPage] = useState(1)
  const [selectedDocumentId, setSelectedDocumentId] = useState(null)
  const [employeeId, setEmployeeId] = useState(null)

  const { data: totalData } = useClientDocuments({ page: 1, pageSize: 1 })
  const { data: expiringData } = useClientExpiringDocuments({ days: 30, page: 1, pageSize: 10 })
  const {
    data: documents,
    isLoading: documentsLoading,
    isError: documentsError,
    refetch: documentsRefetch,
  } = useClientDocuments(buildParams(filters, page))

  const totalDocuments = totalData?.totalCount ?? 0
  const expiringCount = expiringData?.totalCount ?? 0
  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  const updateFilters = (next) => {
    setFilters(next)
    setPage(1)
  }

  const clearFilters = () => {
    setFilters({})
    setPage(1)
  }

  const openDocument = (documentId) => {
    setSelectedDocumentId(documentId)
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
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Documents</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <FileText size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Review company and employee documents on file. Monitor expiry dates and open registered scans.
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
      <SummaryCards total={totalDocuments} expiring={expiringCount} />

      {/* Main content grid */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-8">
          <DocumentFilters
            filters={filters}
            onChange={updateFilters}
            onClear={clearFilters}
            isDirty={hasActiveFilters(filters)}
          />
          <DocumentTable
            data={documents}
            isLoading={documentsLoading}
            isError={documentsError}
            onRetry={() => documentsRefetch()}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onSelect={openDocument}
          />
        </div>

        <div className="lg:col-span-4">
          <ExpiringDocumentsPanel onSelectDocument={openDocument} />
        </div>
      </div>

      {/* Detail drawer */}
      {selectedDocumentId && (
        <DocumentDetailDrawer
          documentId={selectedDocumentId}
          onClose={closeDocument}
          onOpenEmployeeDocuments={(id) => setEmployeeId(id)}

        />
      )}

      {/* Employee documents drawer */}
      {employeeId && (
        <EmployeeDocumentsDrawer
          employeeId={employeeId}
          onClose={() => setEmployeeId(null)}
          onSelectDocument={(documentId) => {
            setEmployeeId(null)
            openDocument(documentId)
          }}
        />
      )}
    </div>
  )
}

export default DocumentsPage