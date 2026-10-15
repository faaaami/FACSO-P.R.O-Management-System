import AdminDetailRow from '../clients/AdminDetailRow'
import { formatDateTime } from '../clients/clientDisplay'
import {
  staffRoleLabel,
  staffActiveLabel,
  staffApprovedLabel,
  staffEmailVerifiedLabel,
} from './staffDisplay'

/**
 * Overview of one staff account, from GET /api/v1/admin/staff/{staffId}.
 *
 * EVERY ROW HERE IS A REAL FIELD ON GetStaffByIdResponseDto:
 *   id, fullName, email, phone, role, isActive, isApproved, emailVerified,
 *   createdAt, updatedAt
 * Nothing is inferred, and no profile data is synthesised — this dataset has no
 * job title, department, avatar, start date or country, so the panel is
 * deliberately short.
 *
 * WHY `updatedAt` IS THE ONE FIELD THE LIST CANNOT SHOW. StaffListItemDto omits
 * it and carries createdAt only, so "last updated" is a detail-only fact. It is
 * the field that tells an admin whether an edit they just made actually landed.
 *
 * THE THREE STATUS BOOLEANS ARE THREE ROWS, not one "Status" row, for the reason
 * set out in staffDisplay.js: each is a separate gate and a combined row would
 * report the wrong one. The labels are spelled out rather than reduced to
 * "Yes"/"No", because "Email verification: No" is ambiguous about whether the
 * absence is a problem.
 *
 * `emailVerified` and `phone` are the two genuinely optional fields, so a dash
 * there means "not set" and is trustworthy. Every other field is non-nullable on
 * the DTO.
 */
function AdminStaffOverviewSection({ staff }) {
  return (
    <dl className="flex flex-col">
      <AdminDetailRow label="Full name" value={staff?.fullName} />
      <AdminDetailRow label="Email" value={staff?.email} />
      <AdminDetailRow label="Phone" value={staff?.phone} />

      {/*
        Role is shown, never offered as a control. The dataset is Agent-only and
        no endpoint accepts a role, so there is nothing to change it to.
      */}
      <AdminDetailRow label="Role" value={staffRoleLabel(staff)} />

      <AdminDetailRow label="Account" value={staffActiveLabel(staff)} />
      <AdminDetailRow label="Approval" value={staffApprovedLabel(staff)} />
      <AdminDetailRow label="Email verification" value={staffEmailVerifiedLabel(staff)} />

      <AdminDetailRow label="Created" value={formatDateTime(staff?.createdAt)} />
      <AdminDetailRow label="Last updated" value={formatDateTime(staff?.updatedAt)} />
    </dl>
  )
}

export default AdminStaffOverviewSection
