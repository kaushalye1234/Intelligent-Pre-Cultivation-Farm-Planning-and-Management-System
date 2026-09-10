import type { FormEvent } from 'react'

export type SelectOption = {
  value: string | number
  label: string
}

export function TextInput({
  label,
  value,
  onChange,
  type = 'text',
  required,
  placeholder,
  min,
  step,
  disabled,
  error,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  type?: string
  required?: boolean
  placeholder?: string
  min?: string | number
  step?: string | number
  disabled?: boolean
  error?: string
}) {
  return (
    <label className="field-control">
      <span>{label}{required ? <strong aria-hidden="true"> *</strong> : null}</span>
      <input
        required={required}
        type={type}
        value={value}
        placeholder={placeholder}
        min={min}
        step={step}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        onChange={(event) => onChange(event.target.value)}
      />
      {error ? <small>{error}</small> : null}
    </label>
  )
}

export function SelectInput({
  label,
  value,
  options,
  onChange,
  required,
  disabled,
  error,
}: {
  label: string
  value: string | number
  options: SelectOption[]
  onChange: (value: string) => void
  required?: boolean
  disabled?: boolean
  error?: string
}) {
  return (
    <label className="field-control">
      <span>{label}{required ? <strong aria-hidden="true"> *</strong> : null}</span>
      <select required={required} value={value} disabled={disabled} aria-invalid={Boolean(error)} onChange={(event) => onChange(event.target.value)}>
        <option value="">Select</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error ? <small>{error}</small> : null}
    </label>
  )
}

export function TextAreaInput({
  label,
  value,
  onChange,
  required,
  placeholder,
  rows = 4,
  disabled,
  error,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  required?: boolean
  placeholder?: string
  rows?: number
  disabled?: boolean
  error?: string
}) {
  return (
    <label className="field-control field-control-wide">
      <span>{label}{required ? <strong aria-hidden="true"> *</strong> : null}</span>
      <textarea
        required={required}
        value={value}
        placeholder={placeholder}
        rows={rows}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        onChange={(event) => onChange(event.target.value)}
      />
      {error ? <small>{error}</small> : null}
    </label>
  )
}

export function FormPanel({
  title,
  onSubmit,
  children,
  submitLabel,
}: {
  title: string
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  children: React.ReactNode
  submitLabel: string
}) {
  return (
    <form className="form-panel" onSubmit={onSubmit}>
      <h3>{title}</h3>
      <div className="form-grid">{children}</div>
      <button type="submit" className="primary-button">
        {submitLabel}
      </button>
    </form>
  )
}
