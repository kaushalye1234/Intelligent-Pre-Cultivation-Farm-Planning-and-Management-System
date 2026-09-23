# Homepage Cultivation Spine Redesign

## Direction

The homepage uses a clean premium farm-tech visual language: restrained serif display type, clean sans-serif body copy, a dark navbar, a medium sage hero, warm ivory page surfaces, soft sage cards, one deeper-green workflow anchor, and subtle harvest-gold accents. The final CTA returns to a light surface with a strong green action. Existing public navigation, routes, authentication behavior, roles, copy, and backend integration remain unchanged.

## Section design

- **Hero:** A compact 55/45 split within a 1180px container. The headline is capped at 60px, body copy at 17px, and the field illustration remains inside its own grid cell. A low-contrast crop silhouette grows visually from the center field row behind the journey markers, filling the composition without competing with content.
- **Capabilities:** Four equal cards in a two-column desktop grid, collapsing to one column on mobile. Decorative elements remain inside each card.
- **Workflow:** A bounded two-column panel with the growing plant in a dedicated 300px column and the five operational steps in the adjacent content column. The plant never overlays cards.
- **Mobile growth:** The tall plant is replaced below 768px with a compact Seed, Sprout, and Mature progression above the workflow steps.
- **Roles:** Five cards use a 3+2 desktop grid, two columns on tablet, and one column on mobile.
- **CTA:** A compact sign-in panel with no oversized or overlapping decoration.

## Implementation constraints

- Use CSS Modules for all homepage and plant layout styling to prevent global class collisions.
- Keep scroll reveals based on `IntersectionObserver`.
- Keep plant progress on one passive, request-animation-frame-throttled scroll listener without React state updates.
- Render the mature plant and disable transitions when `prefers-reduced-motion: reduce` is active.
- Add no animation or UI dependencies and use no deployment-sensitive asset paths.
