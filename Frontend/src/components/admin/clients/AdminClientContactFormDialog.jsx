import ClientContactInviteDialog from '../../shared/ClientContactInviteDialog'
import { useCreateAdminClientContact } from '../../../hooks/admin/useAdminClientMutations'

/** Admin invite dialog: POST /api/v1/admin/clients/{clientId}/contacts. */
function AdminClientContactFormDialog({ clientId, onClose, onCreated }) {
  const createContact = useCreateAdminClientContact()

  return (
    <ClientContactInviteDialog
      clientId={clientId}
      onClose={onClose}
      onCreated={onCreated}
      createContact={createContact}
    />
  )
}

export default AdminClientContactFormDialog
