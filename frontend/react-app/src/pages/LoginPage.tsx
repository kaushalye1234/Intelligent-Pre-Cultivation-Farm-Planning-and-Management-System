import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { Eye, EyeOff, Leaf, LockKeyhole, Mail, ShieldCheck, Sprout } from 'lucide-react'
import { getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { Button } from '../components/Ui'
import { getDashboardPath } from '../routing'

export function LoginPage() {
  const { isAuthenticated, login, user } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [showPassword, setShowPassword] = useState(false)

  if (isAuthenticated) {
    return <Navigate to={getDashboardPath(user?.role)} replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')

    if (!email.trim() || !password.trim()) {
      setError('Email and password are required.')
      return
    }

    setIsSubmitting(true)
    try {
      const result = await login(email.trim(), password)
      navigate(
        result.status === 'passwordChangeRequired'
          ? '/change-temporary-password'
          : getDashboardPath(result.user.role),
        { replace: true },
      )
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="login-screen">
      <div className="login-background-orb login-background-orb-one" aria-hidden="true" />
      <div className="login-background-orb login-background-orb-two" aria-hidden="true" />
      <section className="login-shell" aria-label="Staff and admin login">
        <aside className="login-brand-panel">
          <div className="brand-panel-pattern" aria-hidden="true" />
          <div className="brand-panel-content">
            <div className="brand-panel-logo">
              <span>
                <Sprout size={22} aria-hidden="true" />
              </span>
              <strong>AgriAssist</strong>
            </div>
            <p className="login-eyebrow">Smart Agriculture Platform</p>
            <h1>Empowering Smarter Agriculture</h1>
            <p>Manage farm operations, field activities and agricultural resources through one intelligent platform.</p>
            <div className="brand-feature-list" aria-label="Platform strengths">
              <span><ShieldCheck size={16} aria-hidden="true" /> Secure Access</span>
              <span><Leaf size={16} aria-hidden="true" /> Smart Farm Management</span>
              <span><LockKeyhole size={16} aria-hidden="true" /> Real-time Operations</span>
            </div>
          </div>
          <div className="field-line-art" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
        </aside>
        <section className="login-panel">
          <div className="portal-badge">
            <ShieldCheck size={15} aria-hidden="true" />
            <span>Staff & Admin Portal</span>
          </div>
          <div className="login-heading">
            <h2>Welcome Back</h2>
            <p>Sign in to access the AgriAssist operations portal.</p>
          </div>
          <form onSubmit={handleSubmit} className="login-form" noValidate>
            <label className="login-field">
              <span>Email Address</span>
              <div className="login-input-shell">
                <Mail size={18} aria-hidden="true" />
                <input value={email} type="email" placeholder="Enter your email" autoComplete="email" onChange={(event) => setEmail(event.target.value)} />
              </div>
            </label>
            <label className="login-field">
              <span>Password</span>
              <div className="login-input-shell">
                <LockKeyhole size={18} aria-hidden="true" />
                <input value={password} type={showPassword ? 'text' : 'password'} placeholder="Enter your password" autoComplete="current-password" onChange={(event) => setPassword(event.target.value)} />
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
            {error ? <div role="alert" className="form-error login-error">{error}</div> : null}
            <Button type="submit" disabled={isSubmitting} className="login-submit" icon={<LockKeyhole size={16} aria-hidden="true" />}>
              {isSubmitting ? 'Signing in...' : 'Sign In'}
            </Button>
            <p className="secure-login-note"><ShieldCheck size={15} aria-hidden="true" /> Secure access for authorized staff</p>
          </form>
        </section>
      </section>
    </section>
  )
}
