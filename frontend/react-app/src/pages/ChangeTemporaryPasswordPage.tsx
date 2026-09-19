import { useState } from 'react'
import type { FormEvent } from 'react'
import { Eye, EyeOff, KeyRound, LockKeyhole, ShieldCheck } from 'lucide-react'
import { Navigate, useNavigate } from 'react-router-dom'
import { getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { Button } from '../components/Ui'
import { getDashboardPath } from '../routing'

const maximumBcryptBytes = 72

export function ChangeTemporaryPasswordPage() {
  const {
    changeTemporaryPassword,
    hasPasswordChangeSession,
    isAuthenticated,
    logout,
    passwordChangeUser,
    user,
  } = useAuth()
  const navigate = useNavigate()
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (isAuthenticated) {
    return <Navigate to={getDashboardPath(user?.role)} replace />
  }

  if (!hasPasswordChangeSession || !passwordChangeUser) {
    return <Navigate to="/login" replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    if (!passwordChangeUser) {
      setError('Sign in again with your temporary password to continue.')
      return
    }
    if (newPassword.length < 12) {
      setError('Use at least 12 characters. A long passphrase is recommended.')
      return
    }
    if (new TextEncoder().encode(newPassword).length > maximumBcryptBytes) {
      setError('This password is too long for secure processing. Use no more than 72 UTF-8 bytes.')
      return
    }
    if (newPassword !== confirmPassword) {
      setError('The password confirmation does not match.')
      return
    }
    const normalizedPassword = newPassword.trim().toLocaleLowerCase()
    if (
      normalizedPassword === passwordChangeUser.email.trim().toLocaleLowerCase()
      || normalizedPassword === passwordChangeUser.fullName.trim().toLocaleLowerCase()
    ) {
      setError('Choose a password that is different from your email and full name.')
      return
    }

    setIsSubmitting(true)
    try {
      const authenticatedUser = await changeTemporaryPassword(newPassword)
      navigate(getDashboardPath(authenticatedUser.role), { replace: true })
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  function restartLogin() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <section className="login-screen security-screen">
      <section className="security-card" aria-labelledby="temporary-password-title">
        <div className="security-card-icon"><KeyRound size={26} aria-hidden="true" /></div>
        <p className="login-eyebrow">First sign-in security</p>
        <h1 id="temporary-password-title">Replace your temporary password</h1>
        <p className="security-card-intro">
          Welcome, {passwordChangeUser.fullName}. Create your private password before entering the operations console.
        </p>
        <div className="security-guidance" aria-label="Password requirements">
          <ShieldCheck size={18} aria-hidden="true" />
          <span>Use 12 or more characters. Long, unique passphrases are supported up to the secure BCrypt limit.</span>
        </div>
        <form className="login-form" onSubmit={handleSubmit} noValidate>
          <label className="login-field">
            <span>New password</span>
            <div className="login-input-shell">
              <LockKeyhole size={18} aria-hidden="true" />
              <input
                aria-label="New password"
                autoComplete="new-password"
                type={showPassword ? 'text' : 'password'}
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
              />
              <button
                type="button"
                className="password-visibility-button"
                aria-label={showPassword ? 'Hide secure entry' : 'Reveal secure entry'}
                onClick={() => setShowPassword((current) => !current)}
              >
                {showPassword ? <EyeOff size={18} aria-hidden="true" /> : <Eye size={18} aria-hidden="true" />}
              </button>
            </div>
          </label>
          <label className="login-field">
            <span>Confirm new password</span>
            <div className="login-input-shell">
              <LockKeyhole size={18} aria-hidden="true" />
              <input
                aria-label="Confirm new password"
                autoComplete="new-password"
                type={showPassword ? 'text' : 'password'}
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
              />
            </div>
          </label>
          {error ? <div role="alert" className="form-error login-error">{error}</div> : null}
          <Button type="submit" disabled={isSubmitting} className="login-submit">
            {isSubmitting ? 'Securing account...' : 'Set password and continue'}
          </Button>
          <Button type="button" variant="ghost" disabled={isSubmitting} onClick={restartLogin}>
            Sign in again
          </Button>
        </form>
      </section>
    </section>
  )
}
