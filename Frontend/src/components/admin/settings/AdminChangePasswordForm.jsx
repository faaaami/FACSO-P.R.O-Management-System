import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Eye, EyeOff, KeyRound, Loader2, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'
import SectionCard from '../../client/SectionCard'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useChangeAdminPassword } from '../../../hooks/admin/useAdminSettingsMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * Mirrors ChangePasswordCommandValidator. The backend's only password rule is a
 * length minimum of 8 — there is no complexity, casing, digit or symbol
 * requirement anywhere in the validator, so none is invented here and no
 * requirements checklist or strength meter is shown.
 */
const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Current password is required.'),
    newPassword: z.string().min(8, 'New password must be at least 8 characters.'),
    confirmPassword: z.string().min(1, 'Confirm password is required.'),
  })
  // The backend rejects whitespace-only passwords via NotEmpty. These checks trim
  // for the emptiness test only, so a valid password keeps its internal spaces.
  .refine((values) => values.currentPassword.trim().length > 0, {
    message: 'Current password cannot be only whitespace.',
    path: ['currentPassword'],
  })
  .refine((values) => values.newPassword.trim().length > 0, {
    message: 'New password cannot be only whitespace.',
    path: ['newPassword'],
  })
  .refine((values) => values.confirmPassword.trim().length > 0, {
    message: 'Confirm password is required.',
    path: ['confirmPassword'],
  })
  .refine((values) => values.newPassword !== values.currentPassword, {
    message: 'New password must be different from current password.',
    path: ['newPassword'],
  })
  .refine((values) => values.confirmPassword === values.newPassword, {
    message: 'Confirm password does not match new password.',
    path: ['confirmPassword'],
  })

function AdminPasswordField({ id, label, autoComplete, error, disabled, register }) {
  const [show, setShow] = useState(false)

  return (
    <FormField label={label} htmlFor={id} error={error}>
      <div className="relative">
        <input
          id={id}
          type={show ? 'text' : 'password'}
          autoComplete={autoComplete}
          disabled={disabled}
          className={`${error ? inputErrorClass : inputClass} pr-10`}
          placeholder="••••••••"
          {...register}
        />
        <button
          type="button"
          onClick={() => setShow(!show)}
          disabled={disabled}
          aria-label={show ? `Hide ${label}` : `Show ${label}`}
          aria-pressed={show}
          className="absolute inset-y-0 right-0 flex cursor-pointer items-center pr-3.5 text-[#9CA3AF] transition-colors hover:text-[#16181D] focus:outline-none focus:text-[#0F9D74] disabled:cursor-not-allowed"
        >
          {show ? (
            <EyeOff className="h-4 w-4" aria-hidden="true" />
          ) : (
            <Eye className="h-4 w-4" aria-hidden="true" />
          )}
        </button>
      </div>
    </FormField>
  )
}

/**
 * Changes the signed-in Admin's own password via POST /api/v1/users/change-password.
 *
 * Failure handling is split by what the backend can actually tell us:
 *   409 CONFLICT -> the current password is wrong; the message belongs on that field.
 *   400 VALIDATION_ERROR -> per-field messages arrive under PascalCase keys
 *     (CurrentPassword / NewPassword / ConfirmPassword) and are mapped onto the
 *     matching RHF fields, so the user sees which field is wrong.
 *   anything else -> a single banner, since there is no field to attach it to.
 */
function AdminChangePasswordForm() {
  const changePassword = useChangeAdminPassword()
  const [errorMessage, setErrorMessage] = useState(null)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: {
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    },
  })

  function onSubmit(values) {
    setErrorMessage(null)

    changePassword.mutate(values, {
      onSuccess: () => {
        // No session side effects to announce: the backend neither revokes the
        // refresh token nor signs the user out, so nothing about other sessions
        // is claimed here.
        reset({ currentPassword: '', newPassword: '', confirmPassword: '' })
        toast.success('Password changed successfully.')
      },
      onError: (error) => {
        if (error?.response?.status === 409) {
          setError('currentPassword', {
            type: 'server',
            message: extractApiErrorMessage(error, 'Current password is incorrect.'),
          })
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

        const message = extractApiErrorMessage(
          error,
          'Unable to change password. Please try again.'
        )
        setErrorMessage(message)
        toast.error(message)
      },
    })
  }

  return (
    <SectionCard
      title="Security"
      icon={KeyRound}
      subtitle="Change the password used to sign in to your account"
    >
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
        aria-label="Change password"
        className="grid grid-cols-1 gap-4"
      >
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <AdminPasswordField
            id="adminCurrentPassword"
            label="Current password"
            autoComplete="current-password"
            error={errors.currentPassword?.message}
            disabled={changePassword.isPending}
            register={register('currentPassword')}
          />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <AdminPasswordField
            id="adminNewPassword"
            label="New password"
            autoComplete="new-password"
            error={errors.newPassword?.message}
            disabled={changePassword.isPending}
            register={register('newPassword')}
          />
          <AdminPasswordField
            id="adminConfirmPassword"
            label="Confirm new password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            disabled={changePassword.isPending}
            register={register('confirmPassword')}
          />
        </div>

        <p className="flex items-center gap-1.5 text-xs text-[#6B7280]">
          <ShieldCheck size={13} strokeWidth={2} className="shrink-0" aria-hidden="true" />
          The new password must be at least 8 characters. No other rules are enforced.
        </p>

        <div>
          <button
            type="submit"
            disabled={changePassword.isPending || !isDirty}
            className="inline-flex cursor-pointer items-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {changePassword.isPending ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                <span>Updating…</span>
              </>
            ) : (
              <span>Update password</span>
            )}
          </button>
        </div>
      </form>
    </SectionCard>
  )
}

export default AdminChangePasswordForm
