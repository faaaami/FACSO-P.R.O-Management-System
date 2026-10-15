import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, ListPlus, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useAgentTaskActions } from '../../../hooks/agent/useAgentTaskActions'
import { extractApiErrorMessage } from '../../../utils/apiError'

const stepSchema = z.object({
  stepName: z.string().trim().min(1, 'Step name is required.'),
  referenceNumber: z.string().optional(),
  proofFileUrl: z.string().optional(),
})

function AgentAddStepForm({ taskId }) {
  const { addStep } = useAgentTaskActions()
  const [errorMessage, setErrorMessage] = useState(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(stepSchema),
    defaultValues: { stepName: '', referenceNumber: '', proofFileUrl: '' },
  })

  function onSubmit(values) {
    setErrorMessage(null)

    addStep.mutate(
      {
        taskId,
        stepName: values.stepName.trim(),
        referenceNumber: values.referenceNumber?.trim() || undefined,
        proofFileUrl: values.proofFileUrl?.trim() || undefined,
      },
      {
        onSuccess: () => {
          reset({ stepName: '', referenceNumber: '', proofFileUrl: '' })
          toast.success('Renewal step added.')
        },
        onError: (error) => {
          const message = extractApiErrorMessage(error, 'Could not add the renewal step.')
          setErrorMessage(message)
          toast.error(message)
        },
      }
    )
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
      <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
        Add renewal step
      </span>

      <div className="mt-3 space-y-3">
        <FormField label="Step name" htmlFor="agentStepName" error={errors.stepName?.message}>
          <input
            id="agentStepName"
            type="text"
            disabled={addStep.isPending}
            placeholder="e.g. Emirates ID renewal submitted"
            className={errors.stepName ? inputErrorClass : inputClass}
            {...register('stepName')}
          />
        </FormField>

        <FormField
          label="Reference number (optional)"
          htmlFor="agentStepReference"
          error={errors.referenceNumber?.message}
        >
          <input
            id="agentStepReference"
            type="text"
            disabled={addStep.isPending}
            placeholder="Application or receipt number"
            className={inputClass}
            {...register('referenceNumber')}
          />
        </FormField>

        <FormField
          label="Proof file URL (optional)"
          htmlFor="agentStepProof"
          error={errors.proofFileUrl?.message}
        >
          <input
            id="agentStepProof"
            type="text"
            disabled={addStep.isPending}
            placeholder="Link to the supporting document"
            className={inputClass}
            {...register('proofFileUrl')}
          />
        </FormField>

        {errorMessage && (
          <div className="flex items-start gap-2 rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-2.5">
            <AlertCircle size={14} strokeWidth={2} className="mt-0.5 shrink-0 text-[#DC2626]" aria-hidden="true" />
            <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
          </div>
        )}

        <button
          type="submit"
          disabled={addStep.isPending}
          className="inline-flex items-center gap-2 rounded-[10px] bg-[#0F9D74] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#0B7D5D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]/30 disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 cursor-pointer"
        >
          {addStep.isPending ? (
            <Loader2 size={16} strokeWidth={2} className="animate-spin" aria-hidden="true" />
          ) : (
            <ListPlus size={16} strokeWidth={1.75} aria-hidden="true" />
          )}
          <span>{addStep.isPending ? 'Adding...' : 'Add step'}</span>
        </button>
      </div>
    </form>
  )
}

export default AgentAddStepForm
