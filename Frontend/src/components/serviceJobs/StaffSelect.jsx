import { useQuery } from '@tanstack/react-query'
import { getAdminStaff } from '../../api/admin/staff'
import { useAuth } from '../../auth/AuthContext'
import { Select } from './ui'

/** Agents plus the signed-in admin ("Me"), for assigning jobs. */
function StaffSelect({ value, onChange, disabled, currentName, id }) {
  const { user } = useAuth()
  const staffQuery = useQuery({
    queryKey: ['admin', 'staff', 'all-active'],
    queryFn: () => getAdminStaff({ pageSize: 100 }),
    staleTime: 60_000,
  })
  const staff = (staffQuery.data?.items ?? []).filter((s) => s.isActive !== false)
  const known = new Set([...staff.map((s) => s.id), user?.id])

  return (
    <Select id={id} value={value ?? ''} disabled={disabled} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">Unassigned</option>
      {user?.id && <option value={user.id}>Me ({user.fullName ?? 'admin'})</option>}
      {value && !known.has(value) && <option value={value}>{currentName ?? 'Current assignee'}</option>}
      {staff.map((s) => (
        <option key={s.id} value={s.id}>{s.fullName}</option>
      ))}
    </Select>
  )
}

export default StaffSelect
