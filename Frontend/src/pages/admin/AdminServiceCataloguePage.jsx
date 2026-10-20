import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { BookOpen, Pencil, Plus, Search } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import ServiceEditorDialog from '../../components/serviceCatalogue/ServiceEditorDialog'
import { Button, Chip, Input, Select } from '../../components/serviceJobs/ui'
import { CATEGORIES, CATEGORY_LABELS, DOCUMENT_TYPE_LABELS, formatAed } from '../../components/serviceJobs/labels'
import { listServices, setServiceActive } from '../../api/serviceCatalogue'
import { extractApiErrorMessage } from '../../utils/apiError'

/** Admin: the services the agency sells, with fees, steps and document checklists. */
function AdminServiceCataloguePage() {
  const queryClient = useQueryClient()
  const [category, setCategory] = useState('')
  const [search, setSearch] = useState('')
  const [showInactive, setShowInactive] = useState(false)
  const [editing, setEditing] = useState(null)

  const query = useQuery({
    queryKey: ['admin', 'services', 'catalogue', showInactive],
    queryFn: () => listServices({ includeInactive: showInactive }),
  })

  const toggle = useMutation({
    mutationFn: ({ id, isActive }) => setServiceActive(id, isActive),
    onSuccess: (s) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'services'] })
      toast.success(`${s.name} ${s.isActive ? 'activated' : 'deactivated'}.`)
    },
    onError: (error) => toast.error(extractApiErrorMessage(error, 'Could not update the service.')),
  })

  const groups = useMemo(() => {
    const term = search.trim().toLowerCase()
    const filtered = (query.data ?? []).filter((s) =>
      (!category || s.category === category) &&
      (!term || [s.name, s.code, s.authority, s.nameArabic].some((v) => v?.toLowerCase().includes(term))))
    return CATEGORIES.map((c) => [c, filtered.filter((s) => s.category === c)]).filter(([, items]) => items.length > 0)
  }, [query.data, category, search])

  const unpriced = (query.data ?? []).filter((s) => s.isActive && Number(s.defaultServiceFee) === 0).length

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Services"
        subtitle="Your price list and the standard steps and documents for every service."
        action={<Button variant="primary" icon={Plus} onClick={() => setEditing('new')}>New service</Button>}
      />

      {unpriced > 0 && (
        <p className="rounded-[10px] border border-[#D97706]/20 bg-amber-50 px-4 py-3 text-sm text-[#92400E]">
          {unpriced} active service{unpriced === 1 ? ' has' : 's have'} no service fee yet. Set your prices before quoting customers.
        </p>
      )}

      <SectionCard title="Catalogue" icon={BookOpen}>
        <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-center">
          <div className="relative sm:w-72">
            <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]" aria-hidden="true" />
            <Input className="pl-8" aria-label="Search services" placeholder="Search name, code, authority…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          <Select aria-label="Category" className="sm:w-56" value={category} onChange={(e) => setCategory(e.target.value)}>
            <option value="">All categories</option>
            {CATEGORIES.map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
          </Select>
          <label className="flex items-center gap-2 text-sm text-[#6B7280]">
            <input type="checkbox" className="h-4 w-4 accent-[#0F9D74]" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
            Show inactive
          </label>
        </div>

        {query.isLoading ? (
          <LoadingState label="Loading services…" />
        ) : query.isError ? (
          <ErrorState message={extractApiErrorMessage(query.error, 'Could not load services.')} onRetry={() => query.refetch()} />
        ) : groups.length === 0 ? (
          <EmptyState icon={BookOpen} message="No services found." />
        ) : (
          <div className="space-y-6">
            {groups.map(([cat, items]) => (
              <div key={cat}>
                <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-[#6B7280]">{CATEGORY_LABELS[cat]}</h3>
                <ul className="divide-y divide-[#E2E4E9] rounded-[10px] border border-[#E2E4E9]">
                  {items.map((s) => (
                    <li key={s.id} className={`flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between ${s.isActive ? '' : 'opacity-60'}`}>
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-[#16181D]">
                          {s.name}
                          {s.nameArabic && <span className="ml-2 text-[#6B7280]" dir="rtl">{s.nameArabic}</span>}
                        </p>
                        <div className="mt-1 flex flex-wrap gap-1.5">
                          <Chip>{s.code}</Chip>
                          {s.authority && <Chip>{s.authority}</Chip>}
                          {s.renewsDocumentType && <Chip tone="accent">Renews {DOCUMENT_TYPE_LABELS[s.renewsDocumentType]}</Chip>}
                          {s.isCounterService && <Chip tone="accent">Counter</Chip>}
                          <Chip>{s.stepCount} steps · {s.requiredDocumentCount} docs</Chip>
                          {!s.isActive && <Chip tone="warning">Inactive</Chip>}
                        </div>
                      </div>
                      <div className="flex shrink-0 items-center gap-3">
                        <div className="text-right text-sm">
                          <p className={`tabular-nums ${Number(s.defaultServiceFee) === 0 ? 'text-[#D97706]' : 'text-[#16181D]'}`}>{formatAed(s.defaultServiceFee)}</p>
                          <p className="text-xs tabular-nums text-[#6B7280]">gov. ~{formatAed(s.estimatedGovernmentFee)}</p>
                        </div>
                        <Button size="sm" icon={Pencil} onClick={() => setEditing(s.id)}>Edit</Button>
                        <Button size="sm" variant="ghost" loading={toggle.isPending && toggle.variables?.id === s.id}
                          onClick={() => toggle.mutate({ id: s.id, isActive: !s.isActive })}>
                          {s.isActive ? 'Deactivate' : 'Activate'}
                        </Button>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}
      </SectionCard>

      {editing && <ServiceEditorDialog serviceId={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

export default AdminServiceCataloguePage
