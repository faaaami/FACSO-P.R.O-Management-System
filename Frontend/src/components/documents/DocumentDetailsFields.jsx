import FormField, { inputClass, inputErrorClass } from '../client/settings/FormField'

/**
 * Schema-driven editor for a document's free-form `details` object.
 *
 * SHARED BY AGENT AND ADMIN. No field is named here. The parent renders one
 * control per field of the selected document type's schema
 * (GET .../document-types/{type}/schema), each
 * `{ key, label, valueKind, required, maxLength }` where valueKind is the string
 * enum 'String' | 'Date' | 'Number' | 'Boolean'. A schema change on the server
 * therefore changes this form with no edit to this file.
 *
 * THE PARENT OWNS THE FORM. `register` and `errors` come from the drawer's
 * useForm instance, so these fields are validated by the same dynamically built
 * Zod schema (buildConfirmSchema) as the rest of the confirmation — a detail
 * field cannot fail here and pass on submit, or the reverse.
 *
 * THE PARENT MUST NOT RENDER THIS UNTIL THE SCHEMA HAS LOADED. An empty field
 * list is indistinguishable from "this type has no extra fields", so rendering
 * against a failed schema would let a reviewer confirm with details silently
 * dropped. The review drawers block on schema failure and offer a retry instead.
 *
 * SCHEMA KEYS ARE REACT-HOOK-FORM PATHS. The schema contract states keys are
 * camelCase and unique within a type, and RHF treats `.` as a path separator —
 * a key containing one would nest instead of landing in `details`. That cannot
 * happen for the shipped schemas, and the alternative (managing a separate
 * state object) would split validation in two.
 */

/** Turns an empty control into an absent value; a required field keeps ''. */
function emptyToUndefined(value) {
  return value === '' ? undefined : value
}

function FieldControl({ field, register, error, id, disabled }) {
  const name = `details.${field.key}`
  const className = error ? inputErrorClass : inputClass
  const common = {
    id,
    disabled,
    'aria-invalid': error ? true : undefined,
  }

  if (field.valueKind === 'Date') {
    // The value is already yyyy-MM-dd: it was normalised when the form was
    // seeded, so no per-render conversion and no timezone drift.
    return (
      <input
        {...common}
        type="date"
        className={className}
        {...register(name, {
          setValueAs: (value) => (field.required ? value : emptyToUndefined(value)),
        })}
      />
    )
  }

  if (field.valueKind === 'Number') {
    return (
      <input
        {...common}
        type="number"
        inputMode="decimal"
        className={className}
        {...register(name, {
          setValueAs: (value) => {
            if (field.required) return value === '' ? value : Number(value)
            return value === '' ? undefined : Number(value)
          },
        })}
      />
    )
  }

  if (field.valueKind === 'Boolean') {
    // A select rather than a checkbox: these fields are optional, and a checkbox
    // cannot express "not recorded" without recording a false that nobody chose.
    return (
      <select
        {...common}
        className={className}
        {...register(name, {
          setValueAs: (value) => {
            if (value === '') return undefined
            return value === 'true'
          },
        })}
      >
        <option value="">Not recorded</option>
        <option value="true">Yes</option>
        <option value="false">No</option>
      </select>
    )
  }

  return (
    <input
      {...common}
      type="text"
      className={className}
      placeholder={field.required ? 'Required' : 'Optional'}
      {...register(name, {
        // A required field keeps its '' so the schema reports "required" rather
        // than a type error; an optional one drops it so the key is not sent.
        setValueAs: (value) => (field.required ? value : emptyToUndefined(value)),
      })}
    />
  )
}

/**
 * Props:
 *   fields    the document type's schema fields (required; loaded successfully)
 *   register  the parent form's register
 *   errors    the parent form's errors, read at `errors.details`
 *   disabled  passed to every control, so a confirmation in flight cannot be
 *             edited underneath itself
 */
function DocumentDetailsFields({ fields, register, errors, disabled = false }) {
  if (!Array.isArray(fields) || fields.length === 0) {
    return (
      <p className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] px-3.5 py-3 text-xs leading-relaxed text-[#6B7280]">
        This document type has no additional fields to record.
      </p>
    )
  }

  const detailErrors = errors?.details ?? {}

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {fields.map((field) => {
        const id = `documentDetail_${field.key}`
        return (
          <FormField
            key={field.key}
            label={field.label || field.key}
            htmlFor={id}
            error={detailErrors[field.key]?.message}
          >
            <FieldControl
              field={field}
              register={register}
              error={detailErrors[field.key]}
              id={id}
              disabled={disabled}
            />
          </FormField>
        )
      })}
    </div>
  )
}

export default DocumentDetailsFields
