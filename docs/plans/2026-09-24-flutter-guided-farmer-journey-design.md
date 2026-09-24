# Flutter Guided Farmer Journey UI Design

## Scope

Redesign the existing farmer Flutter screens with a modern, calm, premium agriculture visual language. This work changes presentation and local navigation only. Existing models, API contracts, permissions, workflow status values, validation rules, form fields, and submission behavior remain intact.

## Visual system

- Warm ivory canvas, white elevated surfaces, deep forest green primary actions, sage supporting surfaces, and restrained amber for warnings and emphasis.
- Clear type hierarchy, generous spacing, rounded surfaces with fine borders and subtle shadows, and consistent iconography.
- Reusable page headers, section cards, status pills, empty states, and form section headers. Keep touch targets and contrast accessible.
- Use a small agricultural line motif on authentication and key empty states. Avoid large green blocks or excessive gradients.

## Farmer journey

1. Authentication, registration, temporary password, and farm and field setup share the same branded shell and form styling. Their current inputs and actions remain unchanged.
2. Home puts Create Crop Plan, the next task, current plan progress, and warnings ahead of secondary statistics. Navigation exposes Home, Plans, and Tasks.
3. Plans has a scannable list and an entry to a separate guided form. The form has Farm & Crop, Season & Dates, History, Budget & Goal, and Review & Submit steps. Existing field choices and validation run before advancing and before the existing submit call.
4. After submission, the UI shows the latest workflow progress with current stage, completed and pending steps, warnings, and next action. Plan cards use the existing workflow status and step data. No pending plan is presented as final.
5. Approved plan detail groups existing data into overview, field insight, weather and resources, recommendations, linked tasks, irrigation, and approval. Existing ownership and approved-only opening rules stay in place.
6. Tasks gets a dedicated page with clear upcoming, completed, and other states, plus irrigation schedules and approval history. It uses current task status values and read-only data.

## State and error handling

Reuse `AppState` and its present API calls. Keep loading, empty, missing-detail, and error states visible and actionable. Preserve the existing refresh behavior. The UI never implies a task, irrigation schedule, or approval exists before the backend reports it.

## Verification

Run Flutter format, analyze, and tests. Update widget expectations for the new navigation and visual hierarchy while preserving tests for required inputs, filtered fields, approved-only plan details, and linked tasks and irrigation. Review source for conflict markers and inspect each local Git commit before creating it.
