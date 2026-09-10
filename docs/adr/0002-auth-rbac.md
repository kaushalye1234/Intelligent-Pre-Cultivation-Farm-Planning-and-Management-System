# ADR 0002: JWT Auth and RBAC

## Decision

Use local email/password login, BCrypt password hashes, JWT bearer authentication, and role-based authorization attributes.

## Status

Accepted.

## Consequences

The BASIC foundation can verify all role workflows without introducing external identity providers.