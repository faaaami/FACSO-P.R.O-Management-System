import { useMemo } from 'react'
import { useAgentEntityMaps } from '../../../hooks/agent/useAgentEntityMaps'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { OWNER_KIND } from '../../documents/documentExtractionForm'

/**
 * Owner selection for the Agent's document review drawer.
 *
 * SCOPE, AND WHY THAT IS THE WHOLE POINT
 * --------------------------------------
 * A confirmation must name a real owner, and the Agent's reachable owners are
 * the ones already visible in their task scope. So the options come from
 * `useAgentEntityMaps`, which pages GET /agent/employees and the entities of
 * GET /agent/clients — the same lists the rest of the Agent portal already
 * caches, and the same lists the document tables use to label owner ids.
 *
 * There is deliberately no free-text id box and no global owner search. A typed
 * GUID would let an Agent attach a document to an owner they cannot see, and the
 * server has no way to distinguish that from a real choice. Offering only real
 * rows makes an out-of-scope owner unreachable by construction.
 *
 * An id is also NEVER prefilled. The extractor reads a file and has no way to
 * know which employee or entity it belongs to, so any owner value it produced
 * would be a guess — and a guessed id looks exactly like a chosen one.
 */
function AgentDocumentOwnerFields({ ownerKind, register, errors, setValue, disabled }) {
  const { employeeNames, entityNames, isLoading, isError } = useAgentEntityMaps()

  const employees = useMemo(
    () =>
      Array.from(employeeNames.entries())
        .map(([id, name]) => ({ id, name }))
        .sort((a, b) => a.name.localeCompare(b.name)),
    [employeeNames],
  )

  const entities = useMemo(
    () =>
      Array.from(entityNames.entries())
        .map(([id, name]) => ({ id, name }))
        .sort((a, b) => a.name.localeCompare(b.name)),
    [entityNames],
  )

  function chooseKind(kind) {
    if (kind === ownerKind) return
    setValue('ownerKind', kind, { shouldValidate: false })
    // Clear the other id: the schema sends exactly one, and a leftover value
    // would be re-sent if the reviewer switched back and forth.
    setValue(kind === OWNER_KIND.employee ? 'clientEntityId' : 'employeeId', '', {
      shouldValidate: false,
    })
  }

  const options = ownerKind === OWNER_KIND.employee ? employees : entities
  const listError = ownerKind === OWNER_KIND.employee ? errors.employeeId : errors.clientEntityId
  const emptyMessage =
    ownerKind === OWNER_KIND.employee
      ? 'No employees are available in your scope, so this document cannot be assigned to one.'
      : 'No client entities are available in your scope, so this document cannot be assigned to one.'

  return (
    <div className="space-y-3">
      <div role="radiogroup" aria-label="Document owner type" className="inline-flex rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-0.5">
        {[
          { kind: OWNER_KIND.employee, label: 'Employee' },
          { kind: OWNER_KIND.entity, label: 'Client entity' },
        ].map((option) => {
          const active = ownerKind === option.kind
          return (
            <button
              key={option.kind}
              type="button"
              role="radio"
              aria-checked={active}
              disabled={disabled}
              onClick={() => chooseKind(option.kind)}
              className={`cursor-pointer rounded-[8px] px-3.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed ${
                active
                  ? 'bg-white text-[#16181D] shadow-[0_1px_3px_rgba(28,31,38,0.06)]'
                  : 'text-[#6B7280] hover:text-[#16181D]'
              }`}
            >
              {option.label}
            </button>
          )
        })}
      </div>

      {isLoading ? (
        <p className="text-xs text-[#9CA3AF]">Loading owners in your scope…</p>
      ) : isError ? (
        <p role="alert" className="text-xs leading-relaxed text-[#DC2626]">
          Your owner lists could not be loaded, so an owner cannot be chosen. Reload the page to try
          again — the document itself is unaffected.
        </p>
      ) : options.length === 0 ? (
        <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-3 text-xs leading-relaxed text-[#92400E]">
          {emptyMessage}
        </p>
      ) : (
        <FormField
          label={ownerKind === OWNER_KIND.employee ? 'Employee' : 'Client entity'}
          htmlFor="extractionOwner"
          error={listError?.message}
        >
          <select
            id="extractionOwner"
            className={listError ? inputErrorClass : inputClass}
            disabled={disabled}
            {...register(ownerKind === OWNER_KIND.employee ? 'employeeId' : 'clientEntityId', {
              setValueAs: (value) => (value === '' ? undefined : value),
            })}
          >
            <option value="">Choose…</option>
            {options.map((option) => (
              <option key={option.id} value={option.id}>
                {option.name}
              </option>
            ))}
          </select>
        </FormField>
      )}

      <p className="text-xs leading-relaxed text-[#6B7280]">
        Owners are limited to the clients and employees in your own scope. Exactly one owner is stored
        with the document.
      </p>
    </div>
  )
}

export default AgentDocumentOwnerFields
