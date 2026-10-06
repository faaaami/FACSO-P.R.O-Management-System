import { useCallback, useState } from 'react'
import { getAdminDocumentExtractionFile } from '../../../api/admin/documents'
import { useAdminDocumentTypeSchema } from '../../../hooks/admin/useAdminDocuments'
import {
  useAdminDocumentExtraction,
  useConfirmAdminDocumentExtraction,
} from '../../../hooks/admin/useAdminDocumentExtraction'
import {
  useAdminClientEntities,
  useAdminDocumentOwnerOptions,
} from '../../../hooks/admin/useAdminDocumentOwnerOptions'
import DocumentExtractionReviewDrawer from '../../documents/DocumentExtractionReviewDrawer'
import DocumentFilePanel from '../../documents/DocumentFilePanel'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { OWNER_KIND } from '../../documents/documentExtractionForm'

/**
 * Admin owner selection, which is necessarily TWO STEPS.
 *
 * There is no Admin entity list route: an entity is reachable only through its
 * company (GET /admin/clients/{clientId}/entities). So the entity picker asks for
 * the company first and then the entity inside it. Inventing a global entity
 * search endpoint is out of scope for this work, and the limit is the same one
 * useAdminDocumentOwners already documents.
 *
 * The company step is not decoration — it is also what makes the list bounded.
 * Without it the drawer would have to page every company in the system to build
 * one select.
 *
 * The employee list is global to Admin and already paged for the expiry
 * registry, so it is shared rather than refetched.
 */
function AdminDocumentOwnerFields({ ownerKind, register, errors, setValue, disabled }) {
  const { employees, clients, nameOf, isLoading, isError } = useAdminDocumentOwnerOptions()
  const [clientId, setClientId] = useState('')
  const { entities, isLoading: isLoadingEntities } = useAdminClientEntities(clientId)

  function chooseKind(kind) {
    if (kind === ownerKind) return
    setValue('ownerKind', kind, { shouldValidate: false })
    setValue(kind === OWNER_KIND.employee ? 'clientEntityId' : 'employeeId', '', {
      shouldValidate: false,
    })
  }

  function chooseClient(nextClientId) {
    setClientId(nextClientId)
    // The entity belongs to the previous company, so it cannot stay selected.
    setValue('clientEntityId', '', { shouldValidate: false })
  }

  const listError = ownerKind === OWNER_KIND.employee ? errors.employeeId : errors.clientEntityId

  // The entity error is held back until a company is chosen. The entity select is
  // disabled without one, so surfacing "select the client entity" there would
  // point the reviewer at a control they cannot click and hide the step that is
  // actually blocking them. Its own placeholder says "Choose a company first…",
  // which names the real blocker.

  return (
    <div className="space-y-3">
      <div
        role="radiogroup"
        aria-label="Document owner type"
        className="inline-flex rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-0.5"
      >
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
        <p className="text-xs text-[#9CA3AF]">Loading owners…</p>
      ) : isError ? (
        <p role="alert" className="text-xs leading-relaxed text-[#DC2626]">
          The owner lists could not be loaded, so an owner cannot be chosen. The document is
          unaffected — reload the page to try again.
        </p>
      ) : ownerKind === OWNER_KIND.employee ? (
        employees.length === 0 ? (
          <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-3 text-xs leading-relaxed text-[#92400E]">
            No employees are on record, so this document cannot be assigned to one.
          </p>
        ) : (
          <FormField label="Employee" htmlFor="adminExtractionEmployee" error={listError?.message}>
            <select
              id="adminExtractionEmployee"
              className={listError ? inputErrorClass : inputClass}
              disabled={disabled}
              {...register('employeeId', { setValueAs: (v) => (v === '' ? undefined : v) })}
            >
              <option value="">Choose an employee…</option>
              {employees.map((employee) => (
                <option key={employee.id} value={employee.id}>
                  {nameOf(employee)}
                </option>
              ))}
            </select>
          </FormField>
        )
      ) : clients.length === 0 ? (
        <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-3 text-xs leading-relaxed text-[#92400E]">
          No client companies are on record, so this document cannot be assigned to an entity.
        </p>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Client company" htmlFor="adminExtractionClient">
            <select
              id="adminExtractionClient"
              className={inputClass}
              value={clientId}
              disabled={disabled}
              onChange={(event) => chooseClient(event.target.value)}
            >
              <option value="">Choose a company…</option>
              {clients.map((client) => (
                <option key={client.id} value={client.id}>
                  {client.companyName}
                </option>
              ))}
            </select>
          </FormField>

          <FormField
            label="Legal entity"
            htmlFor="adminExtractionEntity"
            error={clientId ? listError?.message : undefined}
          >
            <select
              id="adminExtractionEntity"
              className={clientId && listError ? inputErrorClass : inputClass}
              disabled={disabled || !clientId || isLoadingEntities}
              {...register('clientEntityId', { setValueAs: (v) => (v === '' ? undefined : v) })}
            >
              <option value="">
                {!clientId
                  ? 'Choose a company first…'
                  : isLoadingEntities
                    ? 'Loading entities…'
                    : 'Choose an entity…'}
              </option>
              {entities.map((entity) => (
                <option key={entity.id} value={entity.id}>
                  {entity.entityName}
                </option>
              ))}
            </select>
          </FormField>
        </div>
      )}

      <p className="text-xs leading-relaxed text-[#6B7280]">
        Exactly one owner is stored with the document. An owner is never suggested — extraction reads
        the file and cannot know who it belongs to.
      </p>
    </div>
  )
}

/**
 * Admin binding for the shared document review drawer. Same structure as the
 * Agent binding, with the Admin's own endpoints and its own owner picker.
 */
function AdminExtractionReviewDrawer({ extractionId, onClose, onConfirmed }) {
  const { data, isLoading, isError, refresh } = useAdminDocumentExtraction(extractionId)
  const confirm = useConfirmAdminDocumentExtraction()

  const loadFile = useCallback(
    () => getAdminDocumentExtractionFile(extractionId),
    [extractionId],
  )

  const draftState = isLoading ? 'loading' : isError || !data ? 'error' : 'ready'

  return (
    <DocumentExtractionReviewDrawer
      extractionId={extractionId}
      draft={data}
      draftState={draftState}
      refreshDraft={refresh}
      schemaHook={useAdminDocumentTypeSchema}
      ownerPicker={(props) => <AdminDocumentOwnerFields {...props} />}
      confirm={confirm}
      onConfirmed={onConfirmed}
      onClose={onClose}
      filePanel={
        <DocumentFilePanel
          load={loadFile}
          identity={['admin-extraction', extractionId]}
          fileName={data?.fileName}
          contentType={data?.contentType}
          label="Preview the uploaded file"
        />
      }
    />
  )
}

export default AdminExtractionReviewDrawer
