# Farmer Registration Input Validation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enforce safe full-name characters and strict ten-digit local phone input in Flutter Farmer registration.

**Architecture:** Put submit-time rules in shared validation utilities and attach input formatters to the two fields. Widget tests verify interactive filtering and form validation; utility tests verify values that bypass formatters.

**Tech Stack:** Flutter, Dart, flutter_test

---

### Task 1: Add failing validator tests

**Files:**
- Create: `mobile/flutter_app/lib/utils/farmer_registration_validation.dart`
- Create: `mobile/flutter_app/test/farmer_registration_validation_test.dart`
- Modify: `mobile/flutter_app/test/location_input_test.dart`

1. Test valid names with spaces, apostrophes, and hyphens.
2. Test empty, numeric, symbol, and malformed names.
3. Change phone tests to accept only ten-digit local input and reject formatting, letters, `+94`, and wrong lengths.
4. Run the focused tests and confirm they fail before implementation.

### Task 2: Implement submit-time validators

**Files:**
- Create: `mobile/flutter_app/lib/utils/farmer_registration_validation.dart`
- Modify: `mobile/flutter_app/lib/utils/sri_lankan_phone_validation.dart`

1. Implement `validateFarmerFullName` with the approved character and separator rules.
2. Require raw local phone input to match `^0[1-9][0-9]{8}$` before normalizing to `+94`.
3. Run focused utility tests and confirm they pass.

### Task 3: Add failing widget tests

**Files:**
- Modify: `mobile/flutter_app/test/auth_navigation_test.dart`

1. Test typed invalid name and phone characters are filtered.
2. Test pasted formatted/invalid phone values are reduced to digits and still rejected when not exactly ten local digits.
3. Bypass formatters through controller updates to simulate autofill/programmatic values and verify submit-time errors.
4. Run the focused widget tests and confirm failures before form wiring.

### Task 4: Wire the registration form

**Files:**
- Modify: `mobile/flutter_app/lib/screens/register_farmer_screen.dart`

1. Import Flutter services and the full-name validator.
2. Add stable field keys for tests.
3. Apply the approved name allow-list and phone digits-only formatters.
4. Replace inline name validation with the shared validator.
5. Run focused tests, `dart format`, `flutter analyze`, and `flutter test`.

### Task 5: Commit locally

1. Run conflict-marker and `git diff --check` checks.
2. Preserve the pre-existing `pubspec.lock` change and stage only completed implementation files and this plan.
3. Create a local implementation commit. Do not push or change branches.
