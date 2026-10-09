# Field Inspection Form and Image Analysis UI Design

## Goal

Make the Field Officer pre-planting assessment easier to complete and the optional AI image-analysis output easier to review without changing workflow behavior, permissions, validation, API contracts, or backend processing.

## Scope

The change is limited to the React Field Officer assessment experience in `PrePlantingAssessmentPanel`. It preserves the existing workflow status, crop-plan context, form fields, note assistance, evidence upload, representative-image selection, AI analysis requests, review decisions, audit history, and field-analysis action.

## Assessment Form

The Soil, Water, Readiness, and Risks groups become controlled accordion sections. Each section has a concise heading, description, and completion indicator derived from the same required and conditional field rules already used by the form.

- The first incomplete section opens when an editable assessment is loaded.
- Completed sections show a clear success indicator.
- Officers can reopen any completed section and may keep more than one section open.
- A failed submission opens the first section containing invalid or incomplete data and moves focus to that section.
- Browser-native required-field validation remains authoritative for individual controls.
- Evidence upload, submission warnings, and final Save, Upload, and Submit actions remain visible after the accordion instead of being hidden within a section.

A compact progress summary above the sections reports how many assessment groups are complete. This is an operational aid, not a new workflow state.

## Evidence

Evidence upload remains optional and continues using the existing endpoints. Uploaded evidence is presented as an inspectable thumbnail collection rather than metadata-only cards. The representative image has a strong but restrained selected state, and eligible images retain the existing Select for AI action.

Broken or unavailable image previews fall back to the existing evidence link and metadata without blocking the inspection.

## Image Analysis Workspace

The current stacked image-analysis panels become one inline review workspace.

On desktop:

- The left column shows the representative image, selection context, metadata, and Analyze or Analyze Again action.
- The right column shows analysis status, visible findings, possible concerns, severity, uncertainty, non-chemical actions, supporting sources, and the Field Officer review controls.
- Accept and Reject remain direct review decisions.
- Edit enables the existing structured editor in place. Saving it records the existing Officer Edited review.

The original AI result remains distinguishable from the officer's editable projection. Notices for running, failed, unavailable, frozen, or unreviewed states remain visible and use the existing `Notice` and `StatusPill` components.

Staff audit history remains a collapsed `details` region below the complete workspace and loads through the existing lazy request.

## Responsive Behavior

The review workspace uses two columns on wide screens and stacks the image above findings on tablet and mobile widths. Buttons wrap on medium screens and become full-width where needed on narrow screens. Form grids collapse to one column without changing field order. Text, status labels, previews, and action controls must not overlap or cause horizontal scrolling.

## Visual Direction

The redesign remains consistent with the existing quiet operational console:

- Existing color, spacing, border, shadow, typography, notice, status-pill, and button tokens are reused.
- Form groups read as structured work sections rather than decorative nested cards.
- Green communicates completed or accepted states, amber communicates review attention, and red is reserved for rejection or failure.
- The AI assistance area remains explicitly optional and does not visually outrank the officer's assessment.

## Accessibility

- Accordion triggers are real buttons with `aria-expanded` and `aria-controls`.
- Section completion is conveyed with text as well as color and icons.
- The representative preview has useful alternative text.
- Focus moves to the relevant accordion trigger after submission validation exposes a section.
- Existing keyboard operation and native form validation are preserved.
- Motion is minimal and respects reduced-motion preferences.

## Testing

React tests will cover:

- first incomplete section selection;
- completion indicators and progress count;
- reopening completed sections;
- submission opening the relevant invalid section;
- representative image preview and selection state;
- analysis status and result rendering;
- Accept, Edit, Save Officer Edited, and Reject behavior;
- collapsed audit history with lazy loading;
- preservation of existing endpoints and permissions.

Verification includes React lint, production build, the full React test suite, and browser-size visual checks at desktop, tablet, and narrow mobile widths.
