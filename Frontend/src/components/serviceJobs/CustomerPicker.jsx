import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Building2, Search, User } from 'lucide-react'
import { lookupCustomers } from '../../api/customers'
import { CUSTOMER_TYPE_LABELS, PERSON_TYPE_LABELS } from './labels'
import { Chip, Input } from './ui'

function useDebounced(value, delay = 300) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])
  return debounced
}

/**
 * Counter search: name, customer code, phone, email, TRN, trade licence, or a
 * person's passport / Emirates ID / mobile. Picking a matched person also
 * returns that person, so the job can be created for them directly.
 */
function CustomerPicker({ onPick, autoFocus }) {
  const [term, setTerm] = useState('')
  const debounced = useDebounced(term.trim())

  const query = useQuery({
    queryKey: ['customers', 'lookup', debounced],
    queryFn: () => lookupCustomers(debounced),
    enabled: debounced.length >= 2,
    staleTime: 15_000,
  })

  const results = query.data ?? []

  return (
    <div>
      <div className="relative">
        <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]" aria-hidden="true" />
        <Input
          autoFocus={autoFocus}
          className="pl-9"
          aria-label="Search customers"
          placeholder="Name, customer code, mobile, passport or Emirates ID…"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
        />
      </div>

      {debounced.length >= 2 && (
        <div className="mt-2 max-h-72 overflow-y-auto rounded-[10px] border border-[#E2E4E9]">
          {query.isLoading ? (
            <p className="px-3 py-3 text-sm text-[#6B7280]">Searching…</p>
          ) : results.length === 0 ? (
            <p className="px-3 py-3 text-sm text-[#6B7280]">No customer found. Register them as a walk-in.</p>
          ) : (
            <ul className="divide-y divide-[#E2E4E9]">
              {results.map((customer) => (
                <li key={customer.clientCompanyId}>
                  <button
                    type="button"
                    onClick={() => onPick({ customer, person: null })}
                    className="flex w-full cursor-pointer items-start gap-3 px-3 py-2.5 text-left hover:bg-[#F7F8FA] focus:bg-[#F7F8FA] focus:outline-none"
                  >
                    {customer.customerType === 'WalkInIndividual'
                      ? <User size={16} className="mt-0.5 shrink-0 text-[#6B7280]" aria-hidden="true" />
                      : <Building2 size={16} className="mt-0.5 shrink-0 text-[#6B7280]" aria-hidden="true" />}
                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-medium text-[#16181D]">{customer.name}</span>
                      <span className="block text-xs text-[#6B7280]">
                        {[customer.customerCode, customer.phone, customer.tradeLicenseNumber].filter(Boolean).join(' · ')}
                      </span>
                    </span>
                    <Chip tone={customer.customerType === 'Subscription' ? 'accent' : 'neutral'}>
                      {CUSTOMER_TYPE_LABELS[customer.customerType]}
                    </Chip>
                  </button>
                  {customer.matchedPersons.map((person) => (
                    <button
                      key={person.id}
                      type="button"
                      onClick={() => onPick({ customer, person })}
                      className="flex w-full cursor-pointer items-center gap-2 border-t border-dashed border-[#E2E4E9] py-2 pl-10 pr-3 text-left text-xs hover:bg-[#F7F8FA] focus:bg-[#F7F8FA] focus:outline-none"
                    >
                      <span className="font-medium text-[#16181D]">{person.fullName}</span>
                      <span className="text-[#6B7280]">
                        {PERSON_TYPE_LABELS[person.personType]}
                        {person.passportNumber ? ` · ${person.passportNumber}` : ''}
                        {person.emiratesIdNumber ? ` · ${person.emiratesIdNumber}` : ''}
                      </span>
                    </button>
                  ))}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}

export default CustomerPicker
