import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Search, UserCog } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { DataTable } from '../components/DataTable'
import { SelectInput } from '../components/FormControls'
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

export function UsersPage() {
  const [users, setUsers] = useState<UserProfile[]>([])
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [selectedUser, setSelectedUser] = useState<UserProfile | null>(null)
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
      <PageHeader eyebrow="Administration" title="User Management" description="Manage staff accounts, roles and account status using existing admin APIs.">
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
            { header: 'Actions', className: 'actions-cell', render: (row) => (
              <details className="action-menu">
                <summary aria-label={`Actions for ${row.fullName}`}>Actions</summary>
                <div>
                  <button type="button" onClick={() => openRoleModal(row)}>Change Role</button>
                  <button type="button" onClick={() => confirmStatus(row)}>{row.isActive ? 'Deactivate' : 'Activate'}</button>
                </div>
              </details>
            ) },
          ]} />
        </section>
      )}

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
