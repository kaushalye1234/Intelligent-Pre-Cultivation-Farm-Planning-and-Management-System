# ADR 0001: Required Stack

## Decision

Use ASP.NET Core 8 Web API, EF Core 8 with Npgsql/PostgreSQL, React + Vite for staff/admin web workflows, and Flutter + Provider for farmer/mobile workflows.

## Status

Accepted.

## Consequences

The backend owns secrets, persistence, auth, Cloudinary access, and business rules. React and Flutter remain API clients.