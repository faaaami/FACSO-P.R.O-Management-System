import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { UserPlus } from 'lucide-react'
import { createWalkInCustomer } from '../../api/customers'
import { extractApiErrorMessage } from '../../utils/apiError'
import { Button, Field, Input, Select, TextArea } from './ui'

const EMIRATES = ['Dubai', 'Abu Dhabi', 'Sharjah', 'Ajman', 'Umm Al Quwain', 'Ras Al Khaimah', 'Fujairah']

/** Registers a walk-in individual or company at the counter. */
function WalkInCustomerForm({ onCreated, onCancel }) {
  const [type, setType] = useState('WalkInIndividual')
  const [form, setForm] = useState({
    name: '', mobile: '', email: '', emirate: 'Dubai', nationality: '', passportNumber: '',
    emiratesIdNumber: '', dateOfBirth: '', gender: '', tradeLicenseNumber: '', contactPersonName: '',
    taxRegistrationNumber: '', notes: '',
  })
  const set = (key) => (e) => setForm((prev) => ({ ...prev, [key]: e.target.value }))
  const individual = type === 'WalkInIndividual'

  const mutation = useMutation({
    mutationFn: () => createWalkInCustomer({
      customerType: type,
      name: form.name.trim(),
      mobile: form.mobile.trim() || null,
      email: form.email.trim() || null,
      emirate: form.emirate || null,
      nationality: individual ? form.nationality.trim() || null : null,
      passportNumber: individual ? form.passportNumber.trim() || null : null,
      emiratesIdNumber: individual ? form.emiratesIdNumber.trim() || null : null,
      dateOfBirth: individual && form.dateOfBirth ? form.dateOfBirth : null,
      gender: individual ? form.gender || null : null,
      tradeLicenseNumber: individual ? null : form.tradeLicenseNumber.trim() || null,
      contactPersonName: individual ? null : form.contactPersonName.trim() || null,
      taxRegistrationNumber: form.taxRegistrationNumber.trim() || null,
      notes: form.notes.trim() || null,
    }),
    onSuccess: (created) => {
      toast.success(`${created.name} registered as ${created.customerCode}.`)
      onCreated(created)
    },
    onError: (error) => toast.error(extractApiErrorMessage(error, 'The customer could not be registered.')),
  })

  const valid = form.name.trim() && (form.mobile.trim() || form.email.trim())

  return (
    <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (valid) mutation.mutate() }}>
      <div className="flex gap-1.5" role="radiogroup" aria-label="Customer type">
        {[['WalkInIndividual', 'Individual'], ['WalkInCompany', 'Company']].map(([value, label]) => (
          <button key={value} type="button" role="radio" aria-checked={type === value} onClick={() => setType(value)}
            className={`cursor-pointer rounded-[8px] px-3 py-1.5 text-sm font-semibold transition duration-150 ${
              type === value ? 'bg-[#1C1F26] text-white' : 'bg-[#F7F8FA] text-[#6B7280] hover:text-[#16181D]'}`}>
            {label}
          </button>
        ))}
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={individual ? 'Full name (as in passport)' : 'Company name'} required className="sm:col-span-2">
          {(id) => <Input id={id} value={form.name} onChange={set('name')} autoFocus />}
        </Field>
        <Field label="Mobile" hint="Mobile or email is required">
          {(id) => <Input id={id} value={form.mobile} onChange={set('mobile')} placeholder="+971 5x xxx xxxx" />}
        </Field>
        <Field label="Email">
          {(id) => <Input id={id} type="email" value={form.email} onChange={set('email')} />}
        </Field>

        {individual ? (
          <>
            <Field label="Nationality">
              {(id) => <Input id={id} value={form.nationality} onChange={set('nationality')} />}
            </Field>
            <Field label="Passport number">
              {(id) => <Input id={id} value={form.passportNumber} onChange={set('passportNumber')} />}
            </Field>
            <Field label="Emirates ID" hint="784-YYYY-NNNNNNN-N">
              {(id) => <Input id={id} value={form.emiratesIdNumber} onChange={set('emiratesIdNumber')} />}
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Date of birth">
                {(id) => <Input id={id} type="date" value={form.dateOfBirth} onChange={set('dateOfBirth')} />}
              </Field>
              <Field label="Gender">
                {(id) => (
                  <Select id={id} value={form.gender} onChange={set('gender')}>
                    <option value="">—</option>
                    <option value="M">Male</option>
                    <option value="F">Female</option>
                  </Select>
                )}
              </Field>
            </div>
          </>
        ) : (
          <>
            <Field label="Trade licence no.">
              {(id) => <Input id={id} value={form.tradeLicenseNumber} onChange={set('tradeLicenseNumber')} />}
            </Field>
            <Field label="Contact person">
              {(id) => <Input id={id} value={form.contactPersonName} onChange={set('contactPersonName')} />}
            </Field>
            <Field label="TRN (VAT)" hint="15 digits, if VAT registered">
              {(id) => <Input id={id} value={form.taxRegistrationNumber} onChange={set('taxRegistrationNumber')} />}
            </Field>
          </>
        )}

        <Field label="Emirate">
          {(id) => (
            <Select id={id} value={form.emirate} onChange={set('emirate')}>
              {EMIRATES.map((e) => <option key={e} value={e}>{e}</option>)}
            </Select>
          )}
        </Field>
        <Field label="Notes" className="sm:col-span-2">
          {(id) => <TextArea id={id} rows={2} value={form.notes} onChange={set('notes')} />}
        </Field>
      </div>

      <div className="flex justify-end gap-2">
        {onCancel && <Button onClick={onCancel}>Cancel</Button>}
        <Button type="submit" variant="primary" icon={UserPlus} loading={mutation.isPending} disabled={!valid}>
          Register customer
        </Button>
      </div>
    </form>
  )
}

export default WalkInCustomerForm
