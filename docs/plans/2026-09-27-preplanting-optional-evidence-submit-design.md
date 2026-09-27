# Pre-Planting Optional Evidence Submission Design

## Problem

The React submit handler saves the linked assessment, then awaits optional image uploads before calling the dedicated submit endpoint. A Cloudinary failure rejects that await, leaves the selected file in local state, and prevents `POST /api/crop-plans/{id}/pre-planting-assessment/submit` from being sent. The backend assessment submission path itself accepts the valid persisted draft.

## Design

Keep the current request order and contracts:

1. Save the latest form values with `PUT /api/crop-plans/{id}/pre-planting-assessment`.
2. Attempt selected image uploads through the existing generic image endpoint.
3. Capture an image failure as an optional-evidence warning instead of aborting.
4. Always call the dedicated assessment submit endpoint after the upload attempt.
5. On successful submission, use the returned assessment as authoritative state, clear the local file selection, render the completed assessment read-only, and expose Run Field Analysis.

If the dedicated submit endpoint fails, retain the saved draft response in component state, keep the form editable, preserve form values, and render the backend validation error. A successful assessment submission does not call Run Field Analysis or change the workflow step.

## Error handling

- Draft-save errors remain blocking because submission must validate persisted data.
- Optional upload errors are non-blocking only inside the submit sequence.
- If submission succeeds after an upload failure, show the normal submission success and a warning that optional evidence was not uploaded.
- If submission fails, show the submission error as the primary contained error.
- The existing action state continues to disable duplicate Save, Upload, Submit, and Run requests.

## Backend compatibility

No backend production contract, permission, workflow, schema, or generic-inspection behavior changes. Add HTTP-level coverage proving a valid Field Officer-owned draft completes through the existing endpoint, does not create another inspection, and leaves the workflow at `CropFieldAnalysisAgent`.

## Verification

Add React regression tests for no evidence, successful evidence, failed optional evidence, contained submit validation failure, read-only completion, Run Field Analysis availability, cleared local files, and retained Workflow Review Agent Evidence. Run focused and full React/backend tests, lint, production builds, whitespace checks, and a conflict-marker scan.
