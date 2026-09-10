# ADR 0005: No Local Git Initialization

## Decision

Do not initialize git in this workspace. Create source files and CI workflow YAML only.

## Status

Accepted for this build session.

## Consequences

Commit-based CI verification is intentionally skipped until the repository owner initializes git and pushes to a remote.