import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { Leaf, LockKeyhole } from 'lucide-react'
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
      const loggedInUser = await login(email.trim(), password)
      navigate(getDashboardPath(loggedInUser.role), { replace: true })
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="login-screen">
      <section className="login-panel">
        <div className="login-mark">
          <Leaf size={34} aria-hidden="true" />
          <div>
            <h1>Staff & Admin Login</h1>
            <p>Sign in to access the AgriAssist operations portal.</p>
          </div>
        </div>
        <form onSubmit={handleSubmit} className="login-form" noValidate>
          <label>
            <span>Email</span>
            <input value={email} type="email" placeholder="Enter your email" autoComplete="email" onChange={(event) => setEmail(event.target.value)} />
          </label>
          <label>
            <span>Password</span>
            <input value={password} type="password" placeholder="Enter your password" autoComplete="current-password" onChange={(event) => setPassword(event.target.value)} />
          </label>
          {error ? <div role="alert" className="form-error">{error}</div> : null}
          <Button type="submit" disabled={isSubmitting} icon={<LockKeyhole size={16} aria-hidden="true" />}>
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </Button>
        </form>
      </section>
    </section>
  )
}
