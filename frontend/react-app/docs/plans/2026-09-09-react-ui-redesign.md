# AgriAssist React UI Redesign Implementation Plan

**Goal:** Build a public AgriAssist website and redesign the authenticated operations portal without changing backend, Flutter, database schema, or API contracts.

**Architecture:** Keep the current Vite React TypeScript app, Axios client, JWT auth context, and existing page modules. Add public routes and shared UI primitives, then refactor existing module pages in place into list-first, role-aware screens.

**Tech Stack:** React 19, TypeScript, React Router 7, Axios, lucide-react, Vitest, CSS modules through existing global stylesheet.

---

### Task 1: Public Website And Routing

**Files:**
- Create: `src/pages/HomePage.tsx`
- Create: `src/pages/AboutPage.tsx`
- Create: `src/pages/ContactPage.tsx`
- Create: `src/components/PublicLayout.tsx`
- Modify: `src/App.tsx`
- Modify: `src/pages/LoginPage.tsx`

**Steps:**
1. Add public layout, navbar, footer, and public pages.
2. Keep `/login` as the staff/admin login route.
3. Move authenticated dashboard to `/dashboard` and keep legacy protected module routes.
4. Redirect authenticated login users by role.

### Task 2: Shared Design System

**Files:**
- Modify: `src/styles.css`
- Modify: `src/index.css`
- Modify: existing components under `src/components`

**Steps:**
1. Define tokens for color, type, spacing, radius, shadow, focus, and state tones.
2. Standardize buttons, forms, tables, badges, alerts, modals, tabs, and toolbars.
3. Keep all styles responsive without introducing fake content.

### Task 3: Authenticated Shell

**Files:**
- Modify: `src/components/Layout.tsx`
- Create/Modify: route helper components as needed

**Steps:**
1. Add role-aware sidebar groups.
2. Add topbar with page context, user identity, role, and logout.
3. Add responsive mobile sidebar behavior.
4. Hide unauthorized navigation links.

### Task 4: Page Refactors

**Files:**
- Modify: `DashboardPage.tsx`
- Modify: `CropPlanningPage.tsx`
- Modify: `InspectionsPage.tsx`
- Modify: `ResourcesPage.tsx`
- Modify: `TaskApprovalPage.tsx`
- Modify: `UsersPage.tsx`

**Steps:**
1. Preserve existing API calls and state.
2. Convert form-heavy pages to tabbed, list-first screens.
3. Move create/approval actions into modal/confirmation flows.
4. Use only existing backend endpoints.
5. Use real API data and designed empty/error/loading states.

### Task 5: Tests And Build

**Files:**
- Modify: `src/App.test.tsx`

**Steps:**
1. Add tests for public routes, navbar, login validation, protected redirects, role redirects, role-aware nav, modal behavior, and API error handling.
2. Run `npm test`.
3. Run `npm run build`.
4. Fix every failure before final report.
