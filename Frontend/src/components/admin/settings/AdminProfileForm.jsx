import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, CheckCircle2, Loader2, Save, User } from 'lucide-react'
import SectionCard from '../../client/SectionCard'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useUpdateAdminProfile } from '../../../hooks/admin/useAdminSettingsMutations'
import { apiFieldErrorEntries } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * Mirrors UpdateMyProfileCommandValidator exactly, including the message text, so
 * a value the user can see as invalid is never sent. `.trim()` also means the
 * submitted values are already trimmed, matching the server-side trim.
 *
 * There is no phone-format rule on the backend — only a 20-character cap — so
 * none is invented here and the field must not claim to validate a number.
 */
const profileSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(1, 'Full name is required.')
    .max(150, 'Full name cannot exceed 150 characters.'),
  phone: z
    .string()
    .trim()
    .max(20, 'Phone number cannot exceed 20 characters.')
    .optional(),
})

/**
 * Edits the signed-in Admin's own full name and phone.
 *
 * email, role, isActive, emailVerified and clientCompanyId are immutable on
 * PATCH /api/v1/users/me — they are never bound by the command and are silently
 * ignored — so this form exposes no control for them.
 */
function AdminProfileForm({ profile }) {
  const updateProfile = useUpdateAdminProfile()
  const [successMessage, setSuccessMessage] = useState(null)
  const [errorMessage, setErrorMessage] = useState(null)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      fullName: profile?.fullName ?? '',
      phone: profile?.phone ?? '',
    },
  })

  function onSubmit(values) {
    setSuccessMessage(null)
    setErrorMessage(null)

    // A blank phone is submitted as an empty string on purpose: the backend
    // normalises blank to null, which is how the field is cleared.
    const submitted = { fullName: values.fullName, phone: values.phone ?? '' }

    updateProfile.mutate(submitted, {
      onSuccess: (updated) => {
        // Re-seed from the server response so the form shows the stored value
        // (trimmed, with a cleared phone coming back as ''), not the raw input.
        reset({
          fullName: updated?.fullName ?? submitted.fullName,
          phone: updated?.phone ?? '',
        })
        setSuccessMessage('Profile updated successfully.')
      },
      onError: (error) => {
        // A 400 carries per-field messages under PascalCase keys (FullName,
        // Phone). Attach each to its field instead of collapsing them.
        const fieldErrors = apiFieldErrorEntries(error)
        if (fieldErrors.length > 0) {
          fieldErrors.forEach(({ field, message }) => {
            setError(field, { type: 'server', message })
          })
          return
        }
        setErrorMessage(
          extractApiErrorMessage(error, 'Unable to update profile. Please try again.')
        )
      },
    })
  }

  return (
    <SectionCard
      title="Profile"
      icon={User}
      subtitle="Your own account details. Email and role are managed elsewhere and cannot be changed here."
    >
      {successMessage && (
        <div
          role="status"
          className="mb-5 flex items-center gap-2.5 rounded-[10px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-4 py-2.5"
        >
          <CheckCircle2 size={14} strokeWidth={2} className="text-[#0F9D74]" aria-hidden="true" />
          <p className="text-xs font-medium text-[#0F9D74]">{successMessage}</p>
        </div>
      )}

      {errorMessage && (
        <div
          role="alert"
          className="mb-5 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
        >
          <AlertCircle size={14} strokeWidth={2} className="text-[#DC2626]" aria-hidden="true" />
          <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
        </div>
      )}

      <form
        onSubmit={handleSubmit(onSubmit)}
        noValidate
        aria-label="Update profile"
        className="grid grid-cols-1 gap-4 sm:grid-cols-2"
      >
        <FormField label="Full name" htmlFor="adminFullName" error={errors.fullName?.message}>
          <input
            id="adminFullName"
            type="text"
            autoComplete="name"
            disabled={updateProfile.isPending}
            className={errors.fullName ? inputErrorClass : inputClass}
            placeholder="Your full name"
            {...register('fullName')}
          />
        </FormField>

        <FormField
          label="Phone (optional)"
          htmlFor="adminPhone"
          error={errors.phone?.message}
        >
          <input
            id="adminPhone"
            type="tel"
            autoComplete="tel"
            disabled={updateProfile.isPending}
            className={errors.phone ? inputErrorClass : inputClass}
            placeholder="+971 50 000 0000"
            {...register('phone')}
          />
        </FormField>

        <p className="text-xs text-[#6B7280] sm:col-span-2">
          Leave the phone field empty to remove it. Up to 20 characters; no format is
          enforced.
        </p>

        <div className="sm:col-span-2">
          <button
            type="submit"
            disabled={updateProfile.isPending || !isDirty}
            className="inline-flex cursor-pointer items-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {updateProfile.isPending ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                <span>Saving…</span>
              </>
            ) : (
              <>
                <Save size={14} strokeWidth={2} aria-hidden="true" />
                <span>Save changes</span>
              </>
            )}
          </button>
        </div>
      </form>
    </SectionCard>
  )
}

export default AdminProfileForm
