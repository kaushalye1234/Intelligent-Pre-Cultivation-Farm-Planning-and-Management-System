# ADR 0004: Cloudinary Boundary

## Decision

Upload inspection images through the ASP.NET Core API only. Client apps submit files to the backend; they do not hold Cloudinary secrets.

## Status

Accepted.

## Consequences

Cloudinary credentials remain server-side, and upload validation is centralized.