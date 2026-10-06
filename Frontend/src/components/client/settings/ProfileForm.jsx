import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, CheckCircle2, Loader2, Mail, User } from 'lucide-react'
import SectionCard from '../SectionCard'
import StatusPill from '../StatusPill'
import FormField, { inputClass, inputErrorClass } from './FormField'
import { useUpdateClientProfile } from '../../../hooks/client/useUpdateClientProfile'
import { useAuth } from '../../../auth/AuthContext'

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

function extractErrorMessage(error, fallback) {
  if (error?.response?.data?.error?.message) {
    return error.response.data.error.message
  }
  if (error?.message) {
    return error.message
  }
  return fallback
}

function ProfileForm({ profile }) {
  const { updateUser } = useAuth()
  const updateProfile = useUpdateClientProfile()
  const [successMessage, setSuccessMessage] = useState(null)
  const [errorMessage, setErrorMessage] = useState(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      fullName: profile.fullName,
      phone: profile.phone ?? '',
    },
  })

  function onSubmit(values) {
    setSuccessMessage(null)
    setErrorMessage(null)

    updateProfile.mutate(
      { fullName: values.fullName, phone: values.phone },
      {
        onSuccess: () => {
          updateUser({ fullName: values.fullName })
          reset({ fullName: values.fullName, phone: values.phone ?? '' })
          setSuccessMessage('Profile updated successfully.')
        },
        onError: (err) => {
          setErrorMessage(
            extractErrorMessage(err, 'Unable to update profile. Please try again.')
          )
        },
      }
    )
  }

  const isActive = profile.isActive
  const isEmailVerified = profile.emailVerified

  return (
    <SectionCard
      title="Profile"
      icon={User}
      subtitle="Your account details and contact information"
    >
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <div className="flex items-center gap-2 text-xs font-medium text-[#6B7280] uppercase tracking-wider mb-2">
            <Mail size={12} strokeWidth={2} aria-hidden="true" />
            Email
          </div>
          <p className="text-sm font-medium text-[#16181D] break-all">{profile.email}</p>
        </div>

        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <div className="text-xs font-medium text-[#6B7280] uppercase tracking-wider mb-2">
            Role
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <StatusPill label={profile.role} tone="success" />
          </div>
        </div>

        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <div className="text-xs font-medium text-[#6B7280] uppercase tracking-wider mb-2">
            Account
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <StatusPill
              label={isActive ? 'Active' : 'Inactive'}
              tone={isActive ? 'success' : 'neutral'}
            />
            <StatusPill
              label={isEmailVerified ? 'Email Verified' : 'Email Not Verified'}
              tone={isEmailVerified ? 'success' : 'warning'}
            />
          </div>
        </div>
      </div>

      {successMessage && (
        <div className="mt-5 flex items-center gap-2.5 rounded-[10px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-4 py-2.5">
          <CheckCircle2 size={14} strokeWidth={2} className="text-[#0F9D74]" aria-hidden="true" />
          <p className="text-xs font-medium text-[#0F9D74]">{successMessage}</p>
        </div>
      )}

      {errorMessage && (
        <div className="mt-5 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5">
          <AlertCircle size={14} strokeWidth={2} className="text-[#DC2626]" aria-hidden="true" />
          <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2">
        <FormField label="Full name" htmlFor="fullName" error={errors.fullName?.message}>
          <input
            id="fullName"
            type="text"
            autoComplete="name"
            disabled={updateProfile.isPending}
            className={errors.fullName ? inputErrorClass : inputClass}
            placeholder="Your full name"
            {...register('fullName')}
          />
        </FormField>

        <FormField label="Phone (optional)" htmlFor="phone" error={errors.phone?.message}>
          <input
            id="phone"
            type="tel"
            autoComplete="tel"
            disabled={updateProfile.isPending}
            className={errors.phone ? inputErrorClass : inputClass}
            placeholder="+971 50 000 0000"
            {...register('phone')}
          />
        </FormField>

        <div className="sm:col-span-2">
          <button
            type="submit"
            disabled={updateProfile.isPending || !isDirty}
            className="inline-flex items-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 cursor-pointer"
          >
            {updateProfile.isPending ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin text-[#0F9D74]" />
                <span>Saving...</span>
              </>
            ) : (
              <span>Save changes</span>
            )}
          </button>
        </div>
      </form>
    </SectionCard>
  )
}

export default ProfileForm