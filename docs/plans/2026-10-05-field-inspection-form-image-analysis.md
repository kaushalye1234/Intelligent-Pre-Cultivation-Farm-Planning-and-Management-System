# Field Inspection Form and Image Analysis UI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Turn the Field Officer pre-planting assessment into a guided accordion form and a responsive inline image-analysis review workspace without changing existing contracts or workflow behavior.

**Architecture:** Keep `PrePlantingAssessmentPanel` as the workflow boundary and add small, local presentation helpers for section completion, accordion state, evidence selection, and review mode. Continue using the existing API functions, DTOs, shared form controls, buttons, notices, and status pills; only React markup, local UI state, CSS, and component tests change.

**Tech Stack:** React 19, TypeScript, Vite, Vitest, Testing Library, lucide-react, CSS custom properties.

---

### Task 1: Specify the guided assessment behavior

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`
- Test: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

**Step 1: Write failing accordion tests**

Add tests asserting that:

- a new assessment opens Soil while Water, Readiness, and Risks are collapsed;
- the trigger exposes `aria-expanded` and a visible `Complete` or `Needs attention` label;
- a fully populated draft reports `4 of 4 sections complete`;
- clicking a completed trigger reopens the section;
- submitting with unassessed risks opens and focuses the Risks section;
- native invalid controls open their owning section before `reportValidity` is presented.

Use accessible trigger names such as `/soil profile.*needs attention/i` and stable section ids such as `assessment-section-soil`.

**Step 2: Run the focused tests and verify failure**

Run:

```powershell
cd frontend/react-app
npm test -- PrePlantingAssessmentPanel.test.tsx
```

Expected: the new tests fail because the form sections are static fieldsets with no accordion state or completion summary.

**Step 3: Commit the failing tests with the implementation in Task 2**

Do not commit a permanently red test-only state. Keep the tests unstaged until Task 2 passes.

### Task 2: Implement the controlled assessment accordion

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.css`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

**Step 1: Add section state and completion helpers**

Add an `AssessmentSectionKey` union for `soil`, `water`, `readiness`, and `risks`. Add pure completion helpers based on the existing required and conditional rules:

- Soil requires type, condition, moisture, and notes when either selection is `Other`.
- Water requires availability, irrigation availability, reliability, plus water source and concerns when the existing helper rules require them.
- Readiness requires drainage condition, waterlogging risk, general condition, planting readiness, conditional drainage notes, and conditional general notes.
- Risks requires an explicit none/selected decision, at least one selected risk when selected, and risk notes when `Other` is selected.

Store open section keys in a `Set<AssessmentSectionKey>`. Initialize it once per loaded request to the first incomplete section, or no section when all four are complete.

**Step 2: Make `AssessmentSection` controlled and accessible**

Replace the static fieldset presentation with a section containing:

```tsx
<button
  type="button"
  aria-expanded={open}
  aria-controls={`assessment-section-${sectionKey}`}
  onClick={onToggle}
>
  <span>{title}</span>
  <span>{complete ? 'Complete' : 'Needs attention'}</span>
</button>
<div id={`assessment-section-${sectionKey}`} hidden={!open}>
  <div className="preplant-form-grid">{children}</div>
</div>
```

Use lucide `CheckCircle2` and `ChevronDown` icons, existing theme variables, and text labels so status is not color-only.

**Step 3: Add progress and validation routing**

Show `n of 4 sections complete` above the accordion. Before submission:

1. Find the first invalid native form control and its `data-assessment-section` owner.
2. Open that section, focus its trigger or invalid control after React renders, and call `reportValidity`.
3. For structured-risk validation, open Risks and focus its trigger before showing the existing error.

Do not change request construction or backend validation messages.

**Step 4: Keep evidence and actions outside the accordion**

Retain the existing evidence input, submission AI warning, Save draft, Upload evidence, and Submit assessment after all accordion sections so they are always visible.

**Step 5: Add responsive accordion styles**

Create a restrained progress band, full-width accordion triggers, completion labels, open-content divider, and mobile-safe spacing. Keep all existing control styling and the one-column breakpoint.

**Step 6: Run the focused tests**

Run:

```powershell
cd frontend/react-app
npm test -- PrePlantingAssessmentPanel.test.tsx
```

Expected: all assessment tests pass.

**Step 7: Commit**

```powershell
git add frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx frontend/react-app/src/pages/PrePlantingAssessmentPanel.css frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx
git commit -m "feat: guide field officers through assessment sections"
```

### Task 3: Specify the inline evidence and analysis workspace

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

**Step 1: Add an image-analysis fixture**

Create a representative evidence image and a succeeded `InspectionImageAnalysisState` fixture with visible findings, concerns, severity, uncertainty, non-chemical actions, sources, and `isReviewable: true`.

**Step 2: Write failing workspace tests**

Add tests asserting that:

- the representative image is rendered as an image preview in the left workspace column;
- evidence thumbnails expose Select for AI without changing its existing PUT endpoint;
- the right column renders structured findings and Accept, Edit findings, and Reject controls;
- Edit findings reveals the structured editor and Cancel edit restores the read-only projection;
- Save Officer Edited review posts the existing edited projection contract;
- Accept and Reject post the existing dispositions;
- audit history remains collapsed and loads only when its summary is opened.

**Step 3: Run focused tests and verify failure**

Run:

```powershell
cd frontend/react-app
npm test -- PrePlantingAssessmentPanel.test.tsx
```

Expected: preview, workspace grouping, and edit-mode assertions fail against the current stacked panels.

### Task 4: Implement the unified image-analysis workspace

**Files:**
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.css`
- Modify: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

**Step 1: Pass evidence context into `ImageAnalysisPanel`**

Pass the normalized assessment images and existing `selectRepresentativeImage` callback into the panel. Derive the representative image with `images.find(image => image.isRepresentativeForAi)`.

**Step 2: Build the left evidence column**

Render:

- the representative image with descriptive alt text;
- an empty state when no representative is selected;
- content type and file size metadata;
- compact selectable evidence thumbnails;
- the existing Analyze or Analyze Again action.

Keep evidence links as the fallback inspection path and keep `safeExternalUrl` behavior for external sources.

**Step 3: Build the right findings and review column**

Group the existing result into clear subsections for Findings, Assessment, Recommended actions, and Sources. Add a local edit-mode boolean:

- Accept calls the existing `onAccept` callback directly.
- Edit findings exposes the existing structured editor.
- Save Officer Edited review calls the existing `onEdit` callback.
- Cancel edit restores the editor from `state.result` and returns to read-only mode.
- Reject calls the existing `onReject` callback.

Do not change the review payload, endpoint, or frozen/read-only rules.

**Step 4: Preserve status and audit states**

Keep existing running, failure, frozen, unreviewed, and effective-review notices. Keep audit history in a collapsed `details` block below both columns with the existing lazy history request.

**Step 5: Add responsive workspace styles**

Use a stable `minmax(280px, 0.8fr) minmax(0, 1.2fr)` grid on desktop. Stack at tablet width, constrain previews with `aspect-ratio`, use `object-fit: contain`, and prevent long findings or URLs from overflowing. Make review actions full-width on narrow phones.

**Step 6: Run the focused tests**

Run:

```powershell
cd frontend/react-app
npm test -- PrePlantingAssessmentPanel.test.tsx
```

Expected: all assessment and analysis workspace tests pass.

**Step 7: Commit**

```powershell
git add frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx frontend/react-app/src/pages/PrePlantingAssessmentPanel.css frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx
git commit -m "feat: clarify field image analysis review"
```

### Task 5: Verify quality and responsive presentation

**Files:**
- Modify only if verification exposes a defect: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx`
- Modify only if verification exposes a defect: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.css`
- Modify only if verification exposes a regression: `frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx`

**Step 1: Run lint**

Run:

```powershell
cd frontend/react-app
npm run lint
```

Expected: exit code 0; existing repository warnings may remain, but no new errors.

**Step 2: Run the production build**

Run:

```powershell
cd frontend/react-app
npm run build
```

Expected: TypeScript and Vite build succeed.

**Step 3: Run the full React suite**

Run:

```powershell
cd frontend/react-app
npm test
```

Expected: all test files pass.

**Step 4: Perform browser-size visual checks**

Use the running local Vite application, or start it on an unused port. Inspect the Field Officer assessment at approximately:

- desktop: 1440 by 1000;
- tablet: 900 by 1100;
- mobile: 390 by 844.

Verify no overlap or horizontal scrolling, readable completion labels, usable collapsed sections, a correctly contained representative image, visible review actions, and collapsed audit history. Capture screenshots in ignored artifacts rather than committing them.

**Step 5: Run repository integrity checks**

Run:

```powershell
git diff --check
rg -n "^(<<<<<<<|=======|>>>>>>>)" frontend/react-app/src docs/plans
git status --short
```

Expected: no whitespace errors or conflict markers; only intended files are changed.

**Step 6: Commit verification fixes if needed**

```powershell
git add frontend/react-app/src/pages/PrePlantingAssessmentPanel.tsx frontend/react-app/src/pages/PrePlantingAssessmentPanel.css frontend/react-app/src/pages/PrePlantingAssessmentPanel.test.tsx
git commit -m "fix: polish responsive inspection review layout"
```

Do not create this commit if verification requires no code changes. Do not push any commit or change branches.
