import { Bell } from 'lucide-react'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import Pagination from '../billing/Pagination'
import NotificationItem from './NotificationItem'

/**
 * Shared notification list used by the Client, Agent and Admin portals.
 *
 * OPTIONAL EMPTY-STATE COPY. `emptyMessage` and `emptyDescription` exist because
 * the three portals have genuinely different honest answers for "why is this
 * empty". The defaults below are the ORIGINAL Client/Agent strings, so a caller
 * that omits both props renders byte-identical markup to before this change.
 * The Admin portal passes its own copy because no backend producer targets an
 * Admin account, and telling an Admin that "compliance alerts will appear here
 * automatically" would be a claim the backend does not support.
 *
 * Everything else about the component is unchanged: same loading, error and
 * empty branching, same NotificationItem, same Pagination.
 */
function NotificationsList({
  data,
  isLoading,
  isError,
  onRetry,
  page,
  pageSize,
  onPageChange,
  onMarkRead,
  isMutating,
  emptyMessage = 'No notifications.',
  emptyDescription = 'New compliance alerts and updates will appear here automatically.',
}) {
  let content

  if (isLoading) {
    content = <LoadingState label="Loading notifications…" />
  } else if (isError) {
    content = <ErrorState message="Could not load notifications." onRetry={onRetry} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={Bell}
        message={emptyMessage}
        description={emptyDescription}
      />
    )
  } else {
    content = (
      <>
        <ul className="space-y-3">
          {data.items.map((item) => (
            <NotificationItem
              key={item.id}
              notification={item}
              onMarkRead={onMarkRead}
              isMutating={isMutating}
            />
          ))}
        </ul>

        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={data.totalCount}
          itemLabel="notification"
          onPageChange={onPageChange}
        />
      </>
    )
  }

  return content
}

export default NotificationsList