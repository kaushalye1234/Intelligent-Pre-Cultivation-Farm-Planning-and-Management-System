import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { KeyRound, Search, UserCog, UserPlus } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { DataTable } from '../components/DataTable'
import { SelectInput, TextInput } from '../components/FormControls'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { Button, ConfirmDialog, Modal, Notice, PageHeader, Toolbar } from '../components/Ui'
import { roleLabels } from '../labels'
import type { ApplicationRole, PagedResult, UserProfile } from '../types'

type ConfirmAction = {
  title: string
  message: string
  label: string
  variant?: 'primary' | 'danger'
  action: () => Promise<void>
  success: string
} | null

const roleOptions = Object.entries(roleLabels).map(([value, label]) => ({ value, label }))
const staffRoleOptions = roleOptions.filter((option) => option.value !== '1')
const emptyCreateForm = {
  fullName: '',
  email: '',
  role: '',
  temporaryPassword: '',
  confirmPassword: '',
  currentAdminPassword: '',
}

function validateTemporaryPassword(password: string, confirmation: string) {
  if (password.length < 12) return 'Temporary passwords must contain at least 12 characters.'
  if (new TextEncoder().encode(password).length > 72) return 'Temporary passwords cannot exceed 72 UTF-8 bytes.'
  if (password !== confirmation) return 'The password confirmation does not match.'
  return ''
}

export function UsersPage() {
  const { user: currentAdmin } = useAuth()
  const [users, setUsers] = useState<UserProfile[]>([])
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [selectedUser, setSelectedUser] = useState<UserProfile | null>(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [createForm, setCreateForm] = useState(emptyCreateForm)
  const [resetUser, setResetUser] = useState<UserProfile | null>(null)
  const [resetPassword, setResetPassword] = useState('')
  const [resetConfirmation, setResetConfirmation] = useState('')
  const [resetAdminPassword, setResetAdminPassword] = useState('')
  const [roleForm, setRoleForm] = useState('')
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [success, setSuccess] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function loadData(nextSearch = search, nextRole = roleFilter, nextStatus = statusFilter) {
    setIsLoading(true)
    setError('')
    try {
      const response = await api.get<PagedResult<UserProfile>>('/users', {
        params: {
          search: nextSearch,
          sortBy: 'fullName',
          role: nextRole ? Number(nextRole) : undefined,
          isActive: nextStatus ? nextStatus === 'true' : undefined,
        },
      })
      setUsers(response.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData('', '', '')
  }, [])

  function openRoleModal(user: UserProfile) {
    setSelectedUser(user)
    setRoleForm(String(user.role))
    setActionError('')
  }

  async function runAction(action: () => Promise<void>, message: string) {
    setIsSubmitting(true)
    setActionError('')
    setSuccess('')
    try {
      await action()
      setSuccess(message)
      setSelectedUser(null)
      setConfirmAction(null)
      await loadData()
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function updateRole(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedUser) return

    await runAction(async () => {
      await api.patch(`/users/${selectedUser.id}/role`, { role: Number(roleForm) as ApplicationRole })
    }, 'User role updated successfully.')
  }

  function openCreateStaff() {
    setCreateForm(emptyCreateForm)
    setActionError('')
    setIsCreateOpen(true)
  }

  async function createStaff(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setActionError('')
    setSuccess('')
    const passwordError = validateTemporaryPassword(createForm.temporaryPassword, createForm.confirmPassword)
    if (passwordError) {
      setActionError(passwordError)
      return
    }
    if (!createForm.fullName.trim() || !createForm.email.trim() || !createForm.role) {
      setActionError('Full name, email, and staff role are required.')
      return
    }
    if (Number(createForm.role) === 5 && !createForm.currentAdminPassword) {
      setActionError('Enter your current Admin password to create another Admin.')
      return
    }

    setIsSubmitting(true)
    try {
      await api.post('/admin/users', {
        fullName: createForm.fullName.trim(),
        email: createForm.email.trim(),
        role: Number(createForm.role) as ApplicationRole,
        temporaryPassword: createForm.temporaryPassword,
        currentAdminPassword: Number(createForm.role) === 5 ? createForm.currentAdminPassword : null,
      })
      setIsCreateOpen(false)
      setCreateForm(emptyCreateForm)
      setSuccess('Staff account created. Share the temporary password through an approved secure channel.')
      await loadData()
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  function openResetPassword(user: UserProfile) {
    setResetUser(user)
    setResetPassword('')
    setResetConfirmation('')
    setResetAdminPassword('')
    setActionError('')
  }

  async function resetStaffPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!resetUser) return
    setActionError('')
    setSuccess('')
    const passwordError = validateTemporaryPassword(resetPassword, resetConfirmation)
    if (passwordError) {
      setActionError(passwordError)
      return
    }
    if (resetUser.role === 5 && !resetAdminPassword) {
      setActionError('Enter your current Admin password to reset another Admin.')
      return
    }

    setIsSubmitting(true)
    try {
      await api.post(`/admin/users/${resetUser.id}/reset-password`, {
        temporaryPassword: resetPassword,
        currentAdminPassword: resetUser.role === 5 ? resetAdminPassword : null,
      })
      setResetUser(null)
      setSuccess('Temporary password reset. Existing sessions for that staff account are no longer valid.')
      await loadData()
    } catch (err) {
      setActionError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  function confirmStatus(user: UserProfile) {
    const nextActive = !user.isActive
    setConfirmAction({
      title: nextActive ? 'Activate user?' : 'Deactivate user?',
      message: `${user.fullName} will be marked ${nextActive ? 'active' : 'inactive'} in the existing user management API.`,
      label: nextActive ? 'Activate' : 'Deactivate',
      variant: nextActive ? 'primary' : 'danger',
      action: async () => { await api.patch(`/users/${user.id}/active`, { isActive: nextActive }) },
      success: nextActive ? 'User activated successfully.' : 'User deactivated successfully.',
    })
  }

  return (
    <section className="page-stack">
      <PageHeader
        eyebrow="Administration"
        title="User Management"
        description="Create staff accounts, issue temporary credentials, and control role and account status."
        actions={<Button icon={<UserPlus size={16} aria-hidden="true" />} onClick={openCreateStaff}>Create staff account</Button>}
      >
        <Toolbar>
          <form className="search-box user-search" onSubmit={(event) => { event.preventDefault(); void loadData(search, roleFilter, statusFilter) }}>
            <Search size={16} aria-hidden="true" />
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search users" aria-label="Search users" />
            <select aria-label="Role filter" value={roleFilter} onChange={(event) => { setRoleFilter(event.target.value); void loadData(search, event.target.value, statusFilter) }}>
              <option value="">All roles</option>
              {roleOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
            <select aria-label="Status filter" value={statusFilter} onChange={(event) => { setStatusFilter(event.target.value); void loadData(search, roleFilter, event.target.value) }}>
              <option value="">All status</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
            <Button variant="secondary" type="submit">Search</Button>
          </form>
        </Toolbar>
      </PageHeader>

      {success ? <Notice tone="success">{success}</Notice> : null}
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <section className="work-section">
          <DataTable rows={users} emptyTitle="No users found" emptyMessage="No account records matched the current filters." getRowKey={(row) => row.id} columns={[
            { header: 'Name', render: (row) => <div><strong>{row.fullName}</strong><p className="muted-text">{row.id.slice(0, 8)}</p></div> },
            { header: 'Email', render: (row) => row.email },
            { header: 'Role', render: (row) => roleLabels[row.role] },
            { header: 'Status', render: (row) => <StatusPill label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'good' : 'bad'} /> },
            { header: 'Password', render: (row) => <StatusPill label={row.mustChangePassword ? 'Change required' : 'Current'} tone={row.mustChangePassword ? 'warn' : 'good'} /> },
            { header: 'Actions', className: 'actions-cell', render: (row) => (
              <details className="action-menu">
                <summary aria-label={`Actions for ${row.fullName}`}>Actions</summary>
                <div>
                  <button type="button" onClick={() => openRoleModal(row)}>Change Role</button>
                  <button type="button" onClick={() => confirmStatus(row)}>{row.isActive ? 'Deactivate' : 'Activate'}</button>
                  {row.role !== 1 && row.id !== currentAdmin?.id ? <button type="button" onClick={() => openResetPassword(row)}>Reset Password</button> : null}
                </div>
              </details>
            ) },
          ]} />
        </section>
      )}

      <Modal
        open={isCreateOpen}
        title="Create staff account"
        description="The staff member must replace this temporary password at first sign-in."
        onClose={() => setIsCreateOpen(false)}
        footer={
          <>
            <Button variant="secondary" onClick={() => setIsCreateOpen(false)} disabled={isSubmitting}>Cancel</Button>
            <Button type="submit" form="create-staff-form" disabled={isSubmitting}>{isSubmitting ? 'Creating...' : 'Create account'}</Button>
          </>
        }
      >
        <form id="create-staff-form" className="form-grid" onSubmit={(event) => void createStaff(event)}>
          <TextInput label="Full name" value={createForm.fullName} required onChange={(value) => setCreateForm((current) => ({ ...current, fullName: value }))} />
          <TextInput label="Email" type="email" value={createForm.email} required onChange={(value) => setCreateForm((current) => ({ ...current, email: value }))} />
          <SelectInput label="Staff role" value={createForm.role} required options={staffRoleOptions} onChange={(value) => setCreateForm((current) => ({ ...current, role: value, currentAdminPassword: '' }))} />
          <label className="field-control">
            <span>Temporary password<strong aria-hidden="true"> *</strong></span>
            <input type="password" autoComplete="new-password" value={createForm.temporaryPassword} onChange={(event) => setCreateForm((current) => ({ ...current, temporaryPassword: event.target.value }))} />
          </label>
          <label className="field-control">
            <span>Confirm temporary password<strong aria-hidden="true"> *</strong></span>
            <input type="password" autoComplete="new-password" value={createForm.confirmPassword} onChange={(event) => setCreateForm((current) => ({ ...current, confirmPassword: event.target.value }))} />
          </label>
          {Number(createForm.role) === 5 ? (
            <label className="field-control">
              <span>Your current Admin password<strong aria-hidden="true"> *</strong></span>
              <input type="password" autoComplete="current-password" value={createForm.currentAdminPassword} onChange={(event) => setCreateForm((current) => ({ ...current, currentAdminPassword: event.target.value }))} />
            </label>
          ) : null}
          <div className="security-guidance field-control-wide">
            <KeyRound size={18} aria-hidden="true" />
            <span>Use 12–72 UTF-8 bytes and share it only through an approved secure channel. Plaintext passwords are never returned by the API.</span>
          </div>
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal
        open={Boolean(resetUser)}
        title="Reset staff password"
        description={resetUser ? `Issue a one-time temporary password for ${resetUser.fullName}.` : undefined}
        onClose={() => setResetUser(null)}
        footer={
          <>
            <Button variant="secondary" onClick={() => setResetUser(null)} disabled={isSubmitting}>Cancel</Button>
            <Button type="submit" form="reset-staff-password-form" disabled={isSubmitting}>{isSubmitting ? 'Resetting...' : 'Reset password'}</Button>
          </>
        }
      >
        <form id="reset-staff-password-form" className="form-grid" onSubmit={(event) => void resetStaffPassword(event)}>
          <label className="field-control">
            <span>Temporary password<strong aria-hidden="true"> *</strong></span>
            <input type="password" autoComplete="new-password" value={resetPassword} onChange={(event) => setResetPassword(event.target.value)} />
          </label>
          <label className="field-control">
            <span>Confirm temporary password<strong aria-hidden="true"> *</strong></span>
            <input type="password" autoComplete="new-password" value={resetConfirmation} onChange={(event) => setResetConfirmation(event.target.value)} />
          </label>
          {resetUser?.role === 5 ? (
            <label className="field-control field-control-wide">
              <span>Your current Admin password<strong aria-hidden="true"> *</strong></span>
              <input type="password" autoComplete="current-password" value={resetAdminPassword} onChange={(event) => setResetAdminPassword(event.target.value)} />
            </label>
          ) : null}
          <div className="security-guidance field-control-wide">
            <KeyRound size={18} aria-hidden="true" />
            <span>Resetting invalidates the staff member's existing sessions and requires a password change on the next login.</span>
          </div>
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <Modal open={Boolean(selectedUser)} title="Change User Role" description="Update this account role using the existing admin role endpoint." onClose={() => setSelectedUser(null)} footer={<><Button variant="secondary" onClick={() => setSelectedUser(null)} disabled={isSubmitting}>Cancel</Button><Button type="submit" form="role-form" disabled={isSubmitting}>{isSubmitting ? 'Saving...' : 'Save Role'}</Button></>}>
        <form id="role-form" className="form-grid" onSubmit={(event) => void updateRole(event)}>
          <div className="identity-panel field-control-wide">
            <UserCog size={20} aria-hidden="true" />
            <div>
              <strong>{selectedUser?.fullName}</strong>
              <span>{selectedUser?.email}</span>
            </div>
          </div>
          <SelectInput label="Role" value={roleForm} required options={roleOptions} onChange={setRoleForm} />
          {actionError ? <div className="form-error field-control-wide" role="alert">{actionError}</div> : null}
        </form>
      </Modal>

      <ConfirmDialog
        open={Boolean(confirmAction)}
        title={confirmAction?.title ?? ''}
        message={confirmAction?.message ?? ''}
        confirmLabel={confirmAction?.label ?? 'Confirm'}
        variant={confirmAction?.variant}
        isSubmitting={isSubmitting}
        onCancel={() => setConfirmAction(null)}
        onConfirm={() => confirmAction ? runAction(confirmAction.action, confirmAction.success) : undefined}
      />
    </section>
  )
}
