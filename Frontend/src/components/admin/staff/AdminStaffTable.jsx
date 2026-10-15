import { UserRound } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import { MISSING_VALUE, displayText, formatDate, presentText, truncate } from '../clients/clientDisplay'
import {
  staffName,
  staffRoleLabel,
  staffActiveLabel,
  staffActiveTone,
  staffApprovedLabel,
  staffApprovedTone,
  staffEmailVerifiedLabel,
  staffEmailVerifiedTone,
} from './staffDisplay'

/**
 * Admin staff list, as a table on wide screens and a card stack below `md` so a
 * 375px viewport never needs horizontal scrolling to read a row.
 *
 * THE THREE STATUS BOOLEANS GET THREE SEPARATE VISIBLE FACTS, not one pill.
 * isActive, isApproved and emailVerified are independent, and each blocks or
 * changes sign-in differently — see the header of staffDisplay.js. Two of them
 * are rendered in the Status cell as stacked pills, and email verification gets
 * its own column so it cannot be missed by someone scanning for "can this person
 * actually log in". Every pill carries its label as text, so nothing here
 * depends on colour.
 *
 * WHY THE TABLE IS A TABLE AND NOT A TABULAR GRID of cards on desktop: the
 * columns are a genuine record set and a screen reader navigating by column
 * header is the whole point of <th scope="col">.
 *
 * ROWS ARE KEYBOARD REACHABLE. The name cell is a real <button>, so Tab reaches
 * it and Enter/Space opens the drawer. The <tr> is NOT clickable and carries no
 * role: a click handler on a non-interactive row would be invisible to a
 * keyboard user and would lie to assistive tech about what is focusable. The
 * mobile card repeats the same button so the drawer is reachable at every width.
 *
 * DEACTIVATE IS A ROW ACTION ONLY WHERE IT IS SAFE TO OFFER. The button is
 * hidden for an already-inactive row because the endpoint rejects that with
 * "Staff member is already inactive". Whether the action is actually PERMITTED
 * depends on the agent's open task count, which the table does not know and must
 * not guess — so it always opens the confirmation dialog, and that dialog is
 * where the workload gate lives. Hiding it here on a guess would be a lie; the
 * honest check is made with real data one click later.
 *
 * The action buttons carry sr-only agent names so they stay distinguishable when
 * several identical "Edit" labels are on screen at once.
 */
function AdminStaffTable({ items, onOpenDetails, onEdit, onDeactivate }) {
  if (items.length === 0) {
    return null
  }

  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full border-collapse text-left text-sm">
          <caption className="sr-only">
            Firm staff accounts, newest records first
          </caption>
          <thead>
            <tr className="border-b border-[#E2E4E9]">
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] first:pl-0"
              >
                Full name
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Email
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Role
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Status
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Email verification
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Created
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280] last:pr-0"
              >
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((staff) => {
              const name = staffName(staff)
              const email = presentText(staff?.email)

              return (
                <tr
                  key={staff?.id}
                  className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-[#F7F8FA]/60"
                >
                  <td className="max-w-[18rem] px-3 py-3.5 align-middle first:pl-0">
                    <div className="flex items-center gap-2">
                      <UserRound
                        size={15}
                        className="shrink-0 text-[#6B7280]"
                        aria-hidden="true"
                      />
                      <button
                        type="button"
                        onClick={() => onOpenDetails(staff?.id)}
                        className="min-w-0 cursor-pointer text-left font-semibold text-[#16181D] underline-offset-2 hover:underline focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:rounded-[4px]"
                      >
                        <span className="block break-words" title={name}>
                          {truncate(name, 44) ?? MISSING_VALUE}
                        </span>
                      </button>
                    </div>
                  </td>
                  <td className="max-w-[16rem] px-3 py-3.5 align-middle text-[#16181D]">
                    {email ? (
                      <span className="min-w-0 break-words" title={email}>
                        {truncate(email, 38) ?? MISSING_VALUE}
                      </span>
                    ) : (
                      <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>
                    )}
                  </td>
                  <td className="px-3 py-3.5 align-middle whitespace-nowrap text-[#6B7280]">
                    {staffRoleLabel(staff)}
                  </td>
                  <td className="px-3 py-3.5 align-middle">
                    <div className="flex flex-col items-start gap-1.5">
                      <StatusPill
                        label={staffActiveLabel(staff)}
                        tone={staffActiveTone(staff)}
                      />
                      <StatusPill
                        label={staffApprovedLabel(staff)}
                        tone={staffApprovedTone(staff)}
                      />
                    </div>
                  </td>
                  <td className="px-3 py-3.5 align-middle">
                    <StatusPill
                      label={staffEmailVerifiedLabel(staff)}
                      tone={staffEmailVerifiedTone(staff)}
                    />
                  </td>
                  <td className="px-3 py-3.5 align-middle whitespace-nowrap text-[#16181D]">
                    {formatDate(staff?.createdAt)}
                  </td>
                  <td className="px-3 py-3.5 text-right align-middle last:pr-0">
                    <div className="flex flex-wrap items-center justify-end gap-1.5">
                      <RowAction
                        label="Details"
                        name={name}
                        onClick={() => onOpenDetails(staff?.id)}
                      />
                      <RowAction
                        label="Edit"
                        name={name}
                        onClick={() => onEdit(staff)}
                      />
                      {staff?.isActive ? (
                        <RowAction
                          label="Deactivate"
                          name={name}
                          tone="danger"
                          onClick={() => onDeactivate(staff)}
                        />
                      ) : null}
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((staff) => {
          const name = staffName(staff)
          const email = presentText(staff?.email)

          return (
            <li
              key={staff?.id}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-center gap-2">
                  <UserRound
                    size={15}
                    className="shrink-0 text-[#6B7280]"
                    aria-hidden="true"
                  />
                  <span className="min-w-0 break-words text-sm font-semibold text-[#16181D]">
                    {name}
                  </span>
                </div>
                <StatusPill
                  label={staffActiveLabel(staff)}
                  tone={staffActiveTone(staff)}
                />
              </div>

              <dl className="mt-3 flex flex-col gap-1.5">
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Email</dt>
                  <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                    {email ? email : displayText(email)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Role</dt>
                  <dd className="text-right text-xs font-medium text-[#16181D]">
                    {staffRoleLabel(staff)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Approval</dt>
                  <dd className="text-right text-xs font-medium text-[#16181D]">
                    {staffApprovedLabel(staff)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Created</dt>
                  <dd className="whitespace-nowrap text-right text-xs font-medium text-[#16181D]">
                    {formatDate(staff?.createdAt)}
                  </dd>
                </div>
              </dl>

              <div className="mt-3 flex flex-col gap-1.5">
                <StatusPill
                  label={staffEmailVerifiedLabel(staff)}
                  tone={staffEmailVerifiedTone(staff)}
                />
              </div>

              <div className="mt-3 flex flex-col gap-1.5">
                <RowAction
                  label="Details"
                  name={name}
                  block
                  onClick={() => onOpenDetails(staff?.id)}
                />
                <RowAction
                  label="Edit"
                  name={name}
                  block
                  onClick={() => onEdit(staff)}
                />
                {staff?.isActive ? (
                  <RowAction
                    label="Deactivate"
                    name={name}
                    tone="danger"
                    block
                    onClick={() => onDeactivate(staff)}
                  />
                ) : null}
              </div>
            </li>
          )
        })}
      </ul>
    </>
  )
}

const ACTION_BASE =
  'rounded-[8px] border px-2.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer'
const ACTION_TONES = {
  default: 'border-[#E2E4E9] bg-white text-[#16181D] hover:bg-[#F7F8FA]',
  danger: 'border-[#DC2626]/25 bg-white text-[#B91C1C] hover:bg-red-50',
}

/**
 * One row action. The agent's name rides along as sr-only text so a screen
 * reader announcing a list of buttons says "Edit for Amina Farouk" rather than
 * six identical "Edit" buttons.
 */
function RowAction({ label, name, onClick, tone = 'default', block = false }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`${ACTION_BASE} ${ACTION_TONES[tone] ?? ACTION_TONES.default} ${
        block ? 'w-full' : ''
      }`}
    >
      {label}
      <span className="sr-only"> for {name}</span>
    </button>
  )
}

export default AdminStaffTable
