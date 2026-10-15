import { useEffect, useId, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Info, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import {
  useCreateAdminClient,
  useUpdateAdminClient,
} from '../../../hooks/admin/useAdminClientMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * Create and edit dialog for a client company — one component, two modes,
 * because the fields are identical and only the semantics of the save differ.
 *
 * THE TWO MODES ARE NOT THE SAME OPERATION, and the difference is the reason
 * this file needs an explanation rather than a shared payload.
 *
 *   create -> POST  CreateClientCompanyCommand  — companyName is REQUIRED
 *   edit   -> PATCH  UpdateClientCompanyCommand  — every field is OPTIONAL
 *
 * THE EDIT ROUTE IS A TRUE PARTIAL UPDATE AND ITS FIELDS CANNOT BE CLEARED.
 * UpdateClientCompanyCommandHandler assigns each field only
 * `if (request.X is not null)`, so null means "leave the stored value alone" and
 * there is no payload that empties a field. The wrapper therefore omits blanks
 * rather than forwarding them, which is why the edit form below is honest about
 * being "type a new value to change this" rather than a record editor:
 *
 *   - Blanking a box here does NOT remove the stored value. It leaves it alone.
 *   - A value that has been typed IS sent and replaces what was there.
 *
 * The form says so in edit mode rather than letting an Admin discover it by
 * saving a blank field and watching the old value survive.
 *
 * WHY companyName IS REQUIRED ON EDIT ANYWAY, even though the server allows
 * null. The update validator applies only MaximumLength, guarded by
 * `When(x => x is not null)` — there is no NotEmpty — so an empty name passes the
 * server and a company can end up nameless. Create forbids that, so the edit
 * form enforces the same rule: the frontend is stopping a nameless record, not
 * restating a server rule that does not exist.
 *
 * NO RULES ARE INVENTED BEYOND THE VALIDATOR. Maximum lengths come from
 * FluentValidation: companyName 200, trade licence 100, phone 30, email 320
 * valid, address 500, emirate 100. There is no uniqueness rule on this route at
 * all — two companies may share a name and two may share a trade licence — so
 * the form does not claim the address or licence is checked, and cannot pre-empt
 * a conflict that the server will not report.
 *
 * `isActive` is NOT offered. It exists on the update DTO and the wrapper honours
 * it, but nothing in this flow sets or clears it: activation is not an onboarding
 * step and no control here would have a second source of truth. A checkbox that
 * merely round-trips the current value is a control that cannot do anything.
 */
const baseFields = {
  companyName: z.string().trim().min(1, 'Company name is required.').max(200, 'Company name must be 200 characters or fewer.'),
  tradeLicenseNumber: z.string().max(100, 'Trade licence must be 100 characters or fewer.'),
  phone: z.string().max(30, 'Phone must be 30 characters or fewer.'),
  email: z
    .string()
    .max(320, 'Email must be 320 characters or fewer.')
    .refine((value) => value === '' || z.string().email().safeParse(value).success, {
      message: 'Enter a valid email address.',
    }),
  address: z.string().max(500, 'Address must be 500 characters or fewer.'),
  emirate: z.string().max(100, 'Emirate must be 100 characters or fewer.'),
}

const schema = z.object(baseFields)

function AdminClientCompanyFormDialog({ mode, client, onClose, onSaved }) {
  const isEdit = mode === 'edit'
  const titleId = useId()
  const noteId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  const createClient = useCreateAdminClient()
  const updateClient = useUpdateAdminClient()
  const mutation = isEdit ? updateClient : createClient

  const [errorMessage, setErrorMessage] = useState(null)

  const buildDefaults = (record) => ({
    companyName: record?.companyName ?? '',
    tradeLicenseNumber: record?.tradeLicenseNumber ?? '',
    phone: record?.phone ?? '',
    email: record?.email ?? '',
    address: record?.address ?? '',
    emirate: record?.emirate ?? '',
  })

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: buildDefaults(isEdit ? client : null),
  })

  // Re-seed when a different record is edited into the same mounted dialog, so a
  // value left over from the previous company cannot be submitted against this one.
  useEffect(() => {
    if (!isEdit) return
    reset(buildDefaults(client))
  }, [isEdit, client, reset])

  function onSubmit(values) {
    setErrorMessage(null)

    if (isEdit) {
      // The wrapper drops every blank, which is what makes this safe on a partial
      // route: a field the Admin did not fill in is omitted, and omitted means
      // "unchanged" server-side. `isActive` is deliberately not sent.
      updateClient.mutate(
        {
          clientId: client?.id,
          companyName: values.companyName.trim(),
          tradeLicenseNumber: values.tradeLicenseNumber.trim(),
          phone: values.phone.trim(),
          email: values.email.trim(),
          address: values.address.trim(),
          emirate: values.emirate.trim(),
        },
        {
          onSuccess: () => {
            toast.success(`${values.companyName.trim()} updated.`)
            onSaved?.()
            onClose()
          },
          onError: (error) =>
            handleSubmitError(
              error,
              setError,
              setErrorMessage,
              'Unable to update this company. Please try again.',
            ),
        },
      )
      return
    }

    // Optional strings are omitted when blank rather than sent as "": "" is a real
    // value to the handler, which would trim and store an empty string and render
    // it as a blank value instead of an absent one.
    createClient.mutate(
      {
        companyName: values.companyName.trim(),
        tradeLicenseNumber: values.tradeLicenseNumber.trim(),
        phone: values.phone.trim(),
        email: values.email.trim(),
        address: values.address.trim(),
        emirate: values.emirate.trim(),
      },
      {
        onSuccess: (created) => {
          toast.success(`${created?.companyName ?? values.companyName.trim()} created.`)
          onSaved?.(created)
          onClose()
        },
        onError: (error) =>
          handleSubmitError(
            error,
            setError,
            setErrorMessage,
            'Unable to create this company. Please try again.',
          ),
      },
    )
  }

  const dialogHeading = isEdit
    ? `Edit ${client?.companyName?.trim() || 'company'}`
    : 'New client company'

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={onClose}
        aria-hidden="true"
      />

      <div className="relative flex min-h-full items-end justify-center p-0 sm:items-center sm:p-6">
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          tabIndex={-1}
          className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none sm:rounded-[14px] sm:p-6"
        >
          <h2
            id={titleId}
            className="text-base font-semibold tracking-tight text-[#16181D]"
          >
            {dialogHeading}
          </h2>

          <p className="mt-1 text-xs text-[#6B7280]">
            {isEdit
              ? 'Only the fields you fill in are sent. Anything left blank keeps its stored value.'
              : 'The company is created active. Entities and client contacts are added afterwards.'}
          </p>

          {isEdit && (
            <div
              id={noteId}
              role="note"
              className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/70 px-3.5 py-3"
            >
              <Info
                size={15}
                strokeWidth={2}
                className="mt-0.5 shrink-0 text-[#6B7280]"
                aria-hidden="true"
              />
              <p className="min-w-0 text-xs text-[#6B7280]">
                A field cannot be emptied on this form. The server treats an omitted
                value as &ldquo;leave unchanged&rdquo;, so clearing a box here will not
                remove what is already stored.
              </p>
            </div>
          )}

          {errorMessage && (
            <div
              role="alert"
              className="mt-4 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
            >
              <AlertCircle
                size={14}
                strokeWidth={2}
                className="shrink-0 text-[#DC2626]"
                aria-hidden="true"
              />
              <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
            </div>
          )}

          <form
            onSubmit={handleSubmit(onSubmit)}
            noValidate
            aria-describedby={isEdit ? noteId : undefined}
            className="mt-5 flex flex-col gap-4"
          >
            <FormField label="Company name" htmlFor="adminClientCompanyName" error={errors.companyName?.message}>
              <input
                id="adminClientCompanyName"
                type="text"
                autoComplete="organization"
                disabled={mutation.isPending}
                className={errors.companyName ? inputErrorClass : inputClass}
                {...register('companyName')}
              />
            </FormField>

            <FormField
              label="Trade licence number"
              htmlFor="adminClientTradeLicense"
              error={errors.tradeLicenseNumber?.message}
            >
              <input
                id="adminClientTradeLicense"
                type="text"
                disabled={mutation.isPending}
                className={errors.tradeLicenseNumber ? inputErrorClass : inputClass}
                {...register('tradeLicenseNumber')}
              />
            </FormField>

            <FormField label="Phone" htmlFor="adminClientPhone" error={errors.phone?.message}>
              <input
                id="adminClientPhone"
                type="tel"
                autoComplete="tel"
                disabled={mutation.isPending}
                className={errors.phone ? inputErrorClass : inputClass}
                {...register('phone')}
              />
            </FormField>

            <FormField label="Email" htmlFor="adminClientEmail" error={errors.email?.message}>
              <input
                id="adminClientEmail"
                type="email"
                autoComplete="email"
                disabled={mutation.isPending}
                className={errors.email ? inputErrorClass : inputClass}
                {...register('email')}
              />
            </FormField>

            <FormField label="Address" htmlFor="adminClientAddress" error={errors.address?.message}>
              <input
                id="adminClientAddress"
                type="text"
                autoComplete="street-address"
                disabled={mutation.isPending}
                className={errors.address ? inputErrorClass : inputClass}
                {...register('address')}
              />
            </FormField>

            <FormField label="Emirate" htmlFor="adminClientEmirate" error={errors.emirate?.message}>
              <input
                id="adminClientEmirate"
                type="text"
                disabled={mutation.isPending}
                className={errors.emirate ? inputErrorClass : inputClass}
                {...register('emirate')}
              />
            </FormField>

            <p className="text-xs text-[#6B7280]">
              {isEdit
                ? 'This company record is a registration reference. It is not a sign-in account.'
                : 'The backend applies no uniqueness check here, so two companies may share a name or a trade licence number.'}
            </p>

            <div className="flex flex-col-reverse gap-2 pt-1 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={onClose}
                disabled={mutation.isPending}
                className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                // Disabled while pending because the create route has no
                // duplicate-name check: a double submit would create two real
                // companies, not one company and a rejected duplicate.
                disabled={mutation.isPending || (isEdit && !isDirty)}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {mutation.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Saving…</span>
                  </>
                ) : (
                  <span>{isEdit ? 'Save changes' : 'Create company'}</span>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}

/**
 * Shared failure handling for both modes.
 *
 * This route has no 409 — there is no uniqueness rule to conflict with — so
 * there is no field to attach a duplicate message to the way the staff form
 * attaches one to email. Everything carrying field-level detail is mapped onto
 * the matching input by name, and only a failure with no field to attach it to
 * reaches the banner.
 *
 * A 404 is called out separately because it means something specific here: the
 * company was deleted, or its id is wrong, which is not a transport problem the
 * Admin can retry their way out of.
 */
function handleSubmitError(error, setError, setErrorMessage, fallback) {
  if (error?.response?.status === 404) {
    const message = 'This company no longer exists.'
    setErrorMessage(message)
    toast.error(message)
    return
  }

  const fieldErrors = apiFieldErrors(error)
  const failedFields = Object.entries(fieldErrors)

  if (failedFields.length > 0) {
    failedFields.forEach(([field, message]) => {
      setError(field, { type: 'server', message })
    })
    return
  }

  const message = extractApiErrorMessage(error, fallback)
  setErrorMessage(message)
  toast.error(message)
}

export default AdminClientCompanyFormDialog
