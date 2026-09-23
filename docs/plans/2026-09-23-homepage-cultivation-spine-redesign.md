# Homepage Cultivation Spine Redesign

## Direction

The homepage uses a clean premium farm-tech visual language: restrained serif display type, clean sans-serif body copy, a dark navbar, a richer forest-to-muted-sage hero, warm ivory page surfaces, soft sage cards, one deeper-green workflow anchor, and subtle harvest-gold accents. The hero stays clearly lighter than near-black forest green while using warm off-white text for strong contrast. The final CTA returns to a light surface with a strong green action. Existing public navigation, routes, authentication behavior, roles, copy, and backend integration remain unchanged.

## Section design

- **Hero:** A balanced 55/45 split within a 1180px container fills the desktop viewport beneath the 76px navigation bar, while mobile retains natural content height. A forest-weighted `#234B35` to muted-sage `#6F8E75` gradient anchors the copy side, while a localized radial glow adds depth behind the illustration. The headline is capped at 56px with warm ivory text and the supporting copy uses a softer warm gray-green. The illustration remains in its own warm-ivory glass card with stronger crop, field, icon, and label contrast. A subtle crop silhouette grows visually from the center field row behind the journey markers without competing with content.
- **Capabilities:** Four equal cards in a two-column desktop grid, collapsing to one column on mobile. Decorative elements remain inside each card.
- **Workflow:** A bounded two-column panel with the growing plant in a dedicated 300px column and the five operational steps in the adjacent content column. The plant never overlays cards.
- **Mobile growth:** The tall plant is replaced below 768px with a compact Seed, Sprout, and Mature progression above the workflow steps.
- **Workflow depth:** The cultivation workflow uses a layered `#173D2A` to `#2B5B3F` forest gradient with a restrained gold glow. Translucent sage step cards remain distinct, while the plant occupies a slightly deeper dedicated panel with brighter botanical strokes for clear separation and legibility.
- **Roles:** Five cards use a 3+2 desktop grid, two columns on tablet, and one column on mobile.
- **CTA:** A compact sign-in panel with no oversized or overlapping decoration.

## Implementation constraints

- Use CSS Modules for all homepage and plant layout styling to prevent global class collisions.
- Keep scroll reveals based on `IntersectionObserver`.
- Keep plant progress on one passive, request-animation-frame-throttled scroll listener without React state updates.
- Render the mature plant and disable transitions when `prefers-reduced-motion: reduce` is active.
- Add no animation or UI dependencies and use no deployment-sensitive asset paths.
