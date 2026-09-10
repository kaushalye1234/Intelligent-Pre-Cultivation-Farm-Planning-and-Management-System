# ADR 0009: Deployment Boundary

## Decision

Deploy the ASP.NET Core API to a .NET-capable host, React as static Vite assets, PostgreSQL through Supabase, Cloudinary for image storage, and Flutter as platform builds.

## Status

Accepted.

## Consequences

Secrets remain server-side. React and Flutter need only public API base URLs.