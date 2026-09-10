import { useState } from 'react'
import type { FormEvent } from 'react'
import { MailCheck } from 'lucide-react'
import { Button, Notice } from '../components/Ui'

export function ContactPage() {
  const [form, setForm] = useState({ name: '', email: '', subject: '', message: '' })
  const [notice, setNotice] = useState('')

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setNotice('')

    if (!form.name.trim() || !form.email.trim() || !form.subject.trim() || !form.message.trim()) {
      setNotice('Please complete all contact form fields.')
      return
    }

    setNotice('Contact form backend integration pending. No message was sent.')
  }

  return (
    <div className="public-page public-content-page">
      <section className="public-title-band">
        <p>Contact Us</p>
        <h1>Project contact</h1>
        <span>Use this page for project enquiries. The public contact API is not implemented yet.</span>
      </section>

      <section className="contact-layout public-section">
        <form className="contact-form" onSubmit={handleSubmit} noValidate>
          <label>
            <span>Name</span>
            <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="Enter your name" required />
          </label>
          <label>
            <span>Email</span>
            <input value={form.email} type="email" onChange={(event) => setForm({ ...form, email: event.target.value })} placeholder="Enter your email" required />
          </label>
          <label>
            <span>Subject</span>
            <input value={form.subject} onChange={(event) => setForm({ ...form, subject: event.target.value })} placeholder="Enter a subject" required />
          </label>
          <label>
            <span>Message</span>
            <textarea value={form.message} onChange={(event) => setForm({ ...form, message: event.target.value })} placeholder="Write your message" required />
          </label>
          {notice ? <Notice tone={notice.includes('pending') ? 'warning' : 'error'}>{notice}</Notice> : null}
          <Button type="submit" icon={<MailCheck size={16} aria-hidden="true" />}>
            Check Message
          </Button>
        </form>

        <aside className="contact-panel">
          <h2>AgriAssist Project Office</h2>
          <p>Demo contact information only. Replace this with official project contact details when they are intentionally configured.</p>
          <dl>
            <div>
              <dt>Email</dt>
              <dd>project-contact@example.com</dd>
            </div>
            <div>
              <dt>Availability</dt>
              <dd>Project support hours to be configured</dd>
            </div>
          </dl>
        </aside>
      </section>
    </div>
  )
}
