import { RefreshCw, Settings } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import AdminProfileSummary from '../../components/admin/settings/AdminProfileSummary'
import AdminProfileForm from '../../components/admin/settings/AdminProfileForm'
import AdminChangePasswordForm from '../../components/admin/settings/AdminChangePasswordForm'
import { useAdminProfile } from '../../hooks/admin/useAdminProfile'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Settings — the signed-in Admin's own profile and sign-in security,
 * backed by the shared `[Authorize]` UsersController:
 *   GET  /api/v1/users/me
 *   PATCH /api/v1/users/me              (fullName, phone only)
 *   POST /api/v1/users/change-password
 *
 * There is no Admin-side user, role or account administration in the backend, so
 * nothing on this page manages other users. The profile query is cached for five
 * minutes, which is why the header carries an explicit refresh control.
 */
function AdminSettingsPage() {
  const { data, isLoading, isError, isFetching, error, refresh } = useAdminProfile()

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Settings"
        subtitle="Your own Admin profile and sign-in security. Other users, roles and accounts are not managed from this portal."
        action={
          <button
            type="button"
            onClick={() => refresh()}
            disabled={isFetching}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <RefreshCw
              size={13}
              strokeWidth={2}
              className={isFetching ? 'animate-spin' : ''}
              aria-hidden="true"
            />
            Refresh
            <span className="sr-only"> the profile</span>
          </button>
        }
      />

      {isLoading && <LoadingState label="Loading settings…" />}

      {!isLoading && isError && (
        <SectionCard
          title="Settings"
          icon={Settings}
          subtitle="Account and security settings"
        >
          <ErrorState
            message={extractApiErrorMessage(
              error,
              'Unable to load your profile. Please try again.'
            )}
            onRetry={() => refresh()}
          />
        </SectionCard>
      )}

      {!isLoading && !isError && data && (
        <>
          <AdminProfileSummary profile={data} />

          <AdminProfileForm profile={data} />

          <AdminChangePasswordForm />
        </>
      )}
    </div>
  )
}

export default AdminSettingsPage
