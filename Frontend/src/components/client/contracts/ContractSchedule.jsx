import { CalendarClock, CalendarRange } from 'lucide-react'
import { differenceInCalendarDays, format } from 'date-fns'
import SectionCard from '../SectionCard'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function remainingDays(endDate) {
  if (!endDate) return null
  const date = new Date(endDate)
  if (Number.isNaN(date.getTime())) return null
  return differenceInCalendarDays(date, new Date())
}

function remainingTone(days) {
  if (days == null) return 'text-[#6B7280]'
  if (days < 0) return 'text-[#DC2626]'
  if (days <= 90) return 'text-[#D97706]'
  return 'text-[#0F9D74]'
}

function RemainingCaption({ days }) {
  if (days == null) {
    return <span className="text-[#6B7280]">No fixed end date</span>
  }
  if (days < 0) {
    return <span className={remainingTone(days)}>Ended {Math.abs(days)} days ago</span>
  }
  return <span className={remainingTone(days)}>{days} days remaining</span>
}

function ContractSchedule({ contract }) {
  const startDate = formatDate(contract.startDate)
  const endDate = formatDate(contract.endDate)
  const days = remainingDays(contract.endDate)

  return (
    <SectionCard
      title="Contract Schedule"
      icon={CalendarRange}
      subtitle="Commencement, validity and remaining tenure"
    >
      <div className="space-y-3 text-xs">
        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <div className="flex items-center gap-1.5 text-[#6B7280]">
            <CalendarRange size={13} strokeWidth={1.75} aria-hidden="true" />
            <span className="font-medium uppercase tracking-wider">Commencement</span>
          </div>
          <p className="mt-1.5 text-sm font-semibold text-[#16181D]">
            {startDate ?? 'N/A'}
          </p>
        </div>

        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <div className="flex items-center gap-1.5 text-[#6B7280]">
            <CalendarClock size={13} strokeWidth={1.75} aria-hidden="true" />
            <span className="font-medium uppercase tracking-wider">Expiry</span>
          </div>
          <p className="mt-1.5 text-sm font-semibold text-[#16181D]">
            {endDate ?? 'Open-ended'}
          </p>
          <p className="mt-0.5 font-medium">
            <RemainingCaption days={days} />
          </p>
        </div>
      </div>
    </SectionCard>
  )
}

export default ContractSchedule