import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, CheckCircle2, Eye, EyeOff, KeyRound, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import SectionCard from '../../client/SectionCard'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useChangeAgentPassword } from '../../../hooks/agent/useChangeAgentPassword'
import { extractApiErrorMessage } from '../../../utils/apiError'

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

function AgentPasswordField({ id, label, autoComplete, error, disabled, register }) {
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
          className="absolute inset-y-0 right-0 flex cursor-pointer items-center pr-3.5 text-[#9CA3AF] transition-colors hover:text-[#16181D] focus:outline-none focus:text-[#0F9D74] disabled:cursor-not-allowed"
        >
          {show ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        </button>
      </div>
    </FormField>
  )
}

function AgentChangePasswordForm() {
  const changePassword = useChangeAgentPassword()
  const [successMessage, setSuccessMessage] = useState(null)
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
    setSuccessMessage(null)
    setErrorMessage(null)

    changePassword.mutate(values, {
      onSuccess: () => {
        reset({ currentPassword: '', newPassword: '', confirmPassword: '' })
        setSuccessMessage('Password changed successfully.')
        toast.success('Password changed successfully.')
      },
      onError: (error) => {
        const message = extractApiErrorMessage(error, 'Unable to change password. Please try again.')
        if (error?.response?.status === 409) {
          setError('currentPassword', { type: 'server', message })
        } else {
          setErrorMessage(message)
          toast.error(message)
        }
      },
    })
  }

  return (
    <SectionCard
      title="Security"
      icon={KeyRound}
      subtitle="Change the password used to sign in to your account"
    >
      {successMessage && (
        <div className="mb-5 flex items-center gap-2.5 rounded-[10px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-4 py-2.5">
          <CheckCircle2 size={14} strokeWidth={2} className="text-[#0F9D74]" aria-hidden="true" />
          <p className="text-xs font-medium text-[#0F9D74]">{successMessage}</p>
        </div>
      )}

      {errorMessage && (
        <div className="mb-5 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5">
          <AlertCircle size={14} strokeWidth={2} className="text-[#DC2626]" aria-hidden="true" />
          <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="grid grid-cols-1 gap-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <AgentPasswordField
            id="agentCurrentPassword"
            label="Current password"
            autoComplete="current-password"
            error={errors.currentPassword?.message}
            disabled={changePassword.isPending}
            register={register('currentPassword')}
          />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <AgentPasswordField
            id="agentNewPassword"
            label="New password"
            autoComplete="new-password"
            error={errors.newPassword?.message}
            disabled={changePassword.isPending}
            register={register('newPassword')}
          />
          <AgentPasswordField
            id="agentConfirmPassword"
            label="Confirm new password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            disabled={changePassword.isPending}
            register={register('confirmPassword')}
          />
        </div>

        <div>
          <button
            type="submit"
            disabled={changePassword.isPending || !isDirty}
            className="inline-flex cursor-pointer items-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {changePassword.isPending ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin text-[#0F9D74]" />
                <span>Updating...</span>
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

export default AgentChangePasswordForm
