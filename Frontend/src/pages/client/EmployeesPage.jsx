import { useState } from 'react'
import { Calendar, Users } from 'lucide-react'
import { format } from 'date-fns'
import EmployeeFilters from '../../components/client/employees/EmployeeFilters'
import EmployeeTable from '../../components/client/employees/EmployeeTable'
import EmployeeDetailDrawer from '../../components/client/employees/EmployeeDetailDrawer'
import EmployeeDocumentsDrawer from '../../components/client/documents/EmployeeDocumentsDrawer'
import DocumentDetailDrawer from '../../components/client/documents/DocumentDetailDrawer'
import { useClientEmployees } from '../../hooks/client/useClientEmployees'

const PAGE_SIZE = 10

function buildParams(entityId, page) {
  const params = { page, pageSize: PAGE_SIZE }
  if (entityId) params.entityId = entityId
  return params
}

function EmployeesPage() {
  const [entityId, setEntityId] = useState('')
  const [page, setPage] = useState(1)
  const [selectedEmployeeId, setSelectedEmployeeId] = useState(null)
  const [documentsEmployeeId, setDocumentsEmployeeId] = useState(null)
  const [selectedDocumentId, setSelectedDocumentId] = useState(null)

  const {
    data: employees,
    isLoading: employeesLoading,
    isError: employeesError,
    refetch: employeesRefetch,
  } = useClientEmployees(buildParams(entityId, page))

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  const updateEntity = (next) => {
    setEntityId(next || '')
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
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Employees</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <Users size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Review registered personnel, their profiles, and supporting compliance documents.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

      {/* Filters */}
      <EmployeeFilters
        entityId={entityId}
        onEntityChange={updateEntity}
        isDirty={Boolean(entityId)}
      />

      {/* Directory */}
      <EmployeeTable
        data={employees}
        isLoading={employeesLoading}
        isError={employeesError}
        onRetry={() => employeesRefetch()}
        page={page}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
        onDetails={setSelectedEmployeeId}
        onDocuments={setDocumentsEmployeeId}
      />

      {/* Employee detail drawer */}
      {selectedEmployeeId && (
        <EmployeeDetailDrawer
          employeeId={selectedEmployeeId}
          onClose={() => setSelectedEmployeeId(null)}
        />
      )}

      {/* Employee documents drawer */}
      {documentsEmployeeId && (
        <EmployeeDocumentsDrawer
          employeeId={documentsEmployeeId}
          onClose={() => setDocumentsEmployeeId(null)}
          onSelectDocument={(documentId) => {
            setDocumentsEmployeeId(null)
            openDocument(documentId)
          }}
        />
      )}

      {/* Document detail drawer */}
      {selectedDocumentId && (
        <DocumentDetailDrawer
          documentId={selectedDocumentId}
          onClose={closeDocument}
          onOpenEmployeeDocuments={setDocumentsEmployeeId}

        />
      )}
    </div>
  )
}

export default EmployeesPage