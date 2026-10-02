# Farmer Contact and Farm District Design

## Scope

Add a required Sri Lankan phone number and contact/home address to new farmer registrations, add a required Sri Lankan District to new and edited farms, expose farmer contact details only in the existing Field Officer pre-planting context, and give Member 3 a deterministic farm weather-location string.

This change does not add coordinates, Town/City columns, Field location columns, separate profile/location tables, or external geocoding. It does not change CropFinding, the Crop Planning coordinator, Member 2 analysis, Member 3 analysis rules, Member 4 approval, or workflow sequencing.

## Existing repository constraints

- `AppUser` stores name and email but no phone or address.
- `Farm` stores one required free-text `Location`; `Field` has no independent location and belongs to a Farm.
- Flutter registration and farm onboarding call the existing Auth and Crop Planning APIs directly.
- The React Crop Planning page also creates farms through the same request contract.
- Member 3 currently sends `Farm.Location` directly to the Weather Resource agent and weather tool.
- The Field Officer pre-planting context shows the farmer name and farm location but no contact details.
- No farm or field coordinates currently exist.

## Chosen approach

Use nullable additive columns and enforce the new requirements at new-write boundaries.

- Add nullable `PhoneNumber` and `ContactAddress` to `AppUser`.
- Add nullable `District` to `Farm`.
- Keep `Field` unchanged; it inherits its Farm's location.
- Apply a non-destructive EF migration. Existing records remain null and existing workflows remain usable.
- Require phone/address for every new farmer registration.
- Require District for every new farm and every farm update after deployment.

Separate profile or location tables would add unnecessary joins and workflow changes. Encoding District inside `Location` would preserve spelling ambiguity and cannot provide deterministic validation.

## Farmer contact data

`RegisterFarmerRequest` accepts `PhoneNumber` and `ContactAddress`. Backend validation is authoritative.

The phone normalizer removes spaces and hyphens, accepts a ten-digit Sri Lankan national number beginning with `0` or the equivalent `+94` number, rejects other characters and malformed lengths, and returns the canonical `+94XXXXXXXXX` representation for persistence. Flutter may run equivalent immediate validation, but it cannot bypass backend validation.

Contact address is required, trimmed, and length-bounded. It is personal contact information and is never copied to a Farm.

The farmer's own authenticated profile may include these values. Generic staff/admin user-list contracts remain unchanged. The existing pre-planting context gains nullable farmer phone/address fields and displays them beside the farmer name. General inspection endpoints and screens do not receive the fields.

## Farm District

A centralized backend `SriLankanDistricts` definition contains exactly the official 25 District names and performs case-insensitive canonicalization. `FarmRequestValidator` rejects null, empty, or unrecognized District values. `CropPlanningService` persists only the canonical name.

`FarmResponse` includes nullable District for legacy compatibility. Farm search can include District without changing ownership rules.

Flutter keeps one reusable local District catalog for the offline form control. A reusable searchable form field filters the fixed choices as the user types and only commits a selected choice; arbitrary input fails form validation. The farm onboarding request sends the selected District. Field onboarding remains unchanged and obtains location through its selected Farm.

The React farm-creation form is updated for the shared API contract using the existing form design and a fixed District selector. No new UI library is introduced.

## Weather location resolution

A deterministic resolver builds the location used by Member 3's workflow input and weather tool:

1. When District is null or invalid, return the legacy `Farm.Location` unchanged.
2. When Location is empty but District is valid, return `District, Sri Lanka`.
3. When Location already contains District as a normalized comma-delimited geographic part, do not append District again.
4. Otherwise return `Location, District, Sri Lanka`.
5. Avoid adding `Sri Lanka` twice when it is already present.

Comparison is trimmed and case-insensitive. Invalid stored District values are ignored so malformed data never becomes structured weather context.

The weather provider tries the complete context first, then the leading location component, then the recognized District. This provides District fallback while preserving existing safe unavailable behavior. Coordinates are not part of the current model and are not introduced here.

Member 3 still owns weather retrieval, interpretation, resource analysis, and recommendations. This change supplies cleaner input only.

## Backward compatibility

- Migration columns are nullable and contain no fake backfill values.
- Existing farmers can authenticate and existing farms/workflows continue to operate.
- Legacy weather requests with no District use the exact existing `Farm.Location` behavior.
- A legacy Farm must supply District when it is later updated through the new contract.
- Existing registration, ownership, authorization, audit, workflow, and soft-deletion rules remain in place.

## Testing

Backend tests cover phone normalization and rejection, required contact fields, valid/invalid Districts, persistence and response contracts, nullable legacy rows, pre-planting contact visibility, deterministic weather context, District provider fallback, and unchanged legacy behavior.

Flutter tests cover registration fields and payload, required/searchable District selection, rejection of arbitrary text, selected-value persistence in the model, and preservation of the existing farm Location value.

React tests cover the required District request field and pre-planting contact rendering. Existing backend, Flutter, React, migration, workflow, and Member 3 regression suites run before implementation commits.
