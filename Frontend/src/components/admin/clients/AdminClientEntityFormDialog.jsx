import { useEffect, useId, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Info, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import {
  useCreateAdminClientEntity,
  useUpdateAdminClientEntity,
} from '../../../hooks/admin/useAdminClientMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * Create and edit dialog for a legal entity — one component, two modes, because
 * the inputs are the same three fields and only the save differs.
 *
 * THE OPPOSITE OF THE COMPANY FORM, IN THE ONE PLACE IT MATTERS. Both routes are
 * PATCH-shaped and both read as "update", but they are not the same operation:
 *
 *   company PATCH  guards every field with `if (request.X is not null)`, so an
 *                  omitted field means "leave it alone" and NOTHING can be
 *                  cleared. Blanking a box in the company form does nothing.
 *
 *   entity PATCH   assigns every field unconditionally — EntityName.Trim(),
 *                  TradeLicenseNumber?.Trim(), Emirate?.Trim(), IsActive — so the
 *                  payload is a complete replacement. null is not "unchanged"
 *                  here, it is "clear this", and the wrapper sends it.
 *
 * So on THIS form, emptying the trade licence or emirate really does remove the
 * stored value, and the form says so. Getting this wrong in either direction is
 * user-visible data loss or a silently ignored edit, which is why the note below
 * is the opposite of the company form's.
 *
 * `isActive` is offered in edit mode and not in create mode, because the two
 * payloads genuinely differ: the create DTO has no isActive at all, while the
 * update DTO carries a non-nullable bool the validator requires. A create form
 * with the checkbox would be sending a field the endpoint does not have.
 *
 * It is SEEDED from the record, never defaulted. Since the update replaces every
 * field, a checkbox left at its default `false` would deactivate a live entity
 * during an edit that only touched the name. The value is read from the entity
 * being edited on every open, so an unrelated save cannot change it.
 *
 * NO RULES ARE INVENTED. Maximum lengths come from FluentValidation: entityName
 * 200 and required on both routes, trade licence 100, emirate 100. The entity
 * DTO carries no address, email, phone, establishment card or licence dates, so
 * there is nothing else to offer and nothing is shown for a field that does not
 * exist.
 */
const schema = z.object({
  entityName: z.string().trim().min(1, 'Entity name is required.').max(200, 'Entity name must be 200 characters or fewer.'),
  tradeLicenseNumber: z.string().max(100, 'Trade licence must be 100 characters or fewer.'),
  emirate: z.string().max(100, 'Emirate must be 100 characters or fewer.'),
  isActive: z.boolean(),
})

function AdminClientEntityFormDialog({ mode, clientId, entity, onClose, onSaved }) {
  const isEdit = mode === 'edit'
  const titleId = useId()
  const noteId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  const createEntity = useCreateAdminClientEntity()
  const updateEntity = useUpdateAdminClientEntity()
  const mutation = isEdit ? updateEntity : createEntity

  const [errorMessage, setErrorMessage] = useState(null)

  // isActive is read from the record on create too, so the value is consistent
  // between the two modes; on create the DTO carries no such field and the
  // wrapper simply does not send it.
  const buildDefaults = (record) => ({
    entityName: record?.entityName ?? '',
    tradeLicenseNumber: record?.tradeLicenseNumber ?? '',
    emirate: record?.emirate ?? '',
    isActive: record?.isActive ?? true,
  })

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: buildDefaults(entity),
  })

  // Re-seed when a different entity is edited into the same mounted dialog, so a
  // previous row's isActive cannot be submitted against this one.
  useEffect(() => {
    if (!isEdit) return
    reset(buildDefaults(entity))
  }, [isEdit, entity, reset])

  function onSubmit(values) {
    setErrorMessage(null)

    const entityName = values.entityName.trim()
    // The wrapper converts each blank to null, and null CLEARS these two fields
    // on this route. That is the honest reading of a full replacement, so the
    // payload is built complete rather than omitting the blanks.
    const payload = {
      entityName,
      tradeLicenseNumber: values.tradeLicenseNumber.trim() || null,
      emirate: values.emirate.trim() || null,
    }

    if (isEdit) {
      updateEntity.mutate(
        { clientId, entityId: entity?.id, ...payload, isActive: values.isActive },
        {
          onSuccess: () => {
            toast.success(`${entityName} updated.`)
            onSaved?.()
            onClose()
          },
          onError: (error) =>
            handleSubmitError(
              error,
              setError,
              setErrorMessage,
              'Unable to update this entity. Please try again.',
            ),
        },
      )
      return
    }

    createEntity.mutate(
      { clientId, ...payload },
      {
        onSuccess: (created) => {
          toast.success(`${created?.entityName ?? entityName} added.`)
          onSaved?.(created)
          onClose()
        },
        onError: (error) =>
          handleSubmitError(
            error,
            setError,
            setErrorMessage,
            'Unable to add this entity. Please try again.',
          ),
      },
    )
  }

  const dialogHeading = isEdit
    ? `Edit ${entity?.entityName?.trim() || 'entity'}`
    : 'Add legal entity'

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
              ? 'This save replaces the stored entity record. Every field below is sent.'
              : 'A trading entity registered under this company. Entities are optional — a contact can be created without one.'}
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
                This save replaces the record, so clearing the trade licence or emirate
                removes the stored value rather than leaving it alone.
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
            <FormField label="Entity name" htmlFor="adminEntityName" error={errors.entityName?.message}>
              <input
                id="adminEntityName"
                type="text"
                autoComplete="organization"
                disabled={mutation.isPending}
                className={errors.entityName ? inputErrorClass : inputClass}
                {...register('entityName')}
              />
            </FormField>

            <FormField
              label="Trade licence number"
              htmlFor="adminEntityTradeLicense"
              error={errors.tradeLicenseNumber?.message}
            >
              <input
                id="adminEntityTradeLicense"
                type="text"
                disabled={mutation.isPending}
                className={errors.tradeLicenseNumber ? inputErrorClass : inputClass}
                {...register('tradeLicenseNumber')}
              />
            </FormField>

            <FormField label="Emirate" htmlFor="adminEntityEmirate" error={errors.emirate?.message}>
              <input
                id="adminEntityEmirate"
                type="text"
                disabled={mutation.isPending}
                className={errors.emirate ? inputErrorClass : inputClass}
                {...register('emirate')}
              />
            </FormField>

            {isEdit && (
              <fieldset className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 px-3.5 py-3">
                <legend className="px-1 text-xs font-medium text-[#6B7280]">Status</legend>
                <div className="mt-1 flex items-start gap-2.5">
                  <input
                    id="adminEntityIsActive"
                    type="checkbox"
                    disabled={mutation.isPending}
                    className="mt-0.5 h-4 w-4 shrink-0 cursor-pointer rounded-[4px] border-[#E2E4E9] text-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                    {...register('isActive')}
                  />
                  <label
                    htmlFor="adminEntityIsActive"
                    className="cursor-pointer text-xs text-[#16181D]"
                  >
                    Active — this entity is in use
                  </label>
                </div>
              </fieldset>
            )}

            <p className="text-xs text-[#6B7280]">
              {isEdit
                ? 'An inactive entity keeps its record and its history. It is not deleted.'
                : 'The backend applies no uniqueness check here, so two entities may share a name or a trade licence number.'}
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
                disabled={mutation.isPending || (isEdit && !isDirty)}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {mutation.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Saving…</span>
                  </>
                ) : (
                  <span>{isEdit ? 'Save changes' : 'Add entity'}</span>
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
 * A 404 here has two possible meanings and both are worth naming: the entity is
 * gone, or the COMPANY is gone — the nested routes are all scoped to a parent
 * that is not deleted, so a deleted company makes every entity route 404 for
 * reasons that have nothing to do with the entity. The message covers both
 * rather than blaming the entity for a company-level condition.
 */
function handleSubmitError(error, setError, setErrorMessage, fallback) {
  if (error?.response?.status === 404) {
    const message = 'This entity or its parent company no longer exists.'
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

export default AdminClientEntityFormDialog
