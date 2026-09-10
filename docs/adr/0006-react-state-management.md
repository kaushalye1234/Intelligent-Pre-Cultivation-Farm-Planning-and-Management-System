# ADR 0006: React State Management

## Decision

Use React Context API and hooks for auth/session and local page state in the BASIC foundation.

## Status

Accepted.

## Consequences

The React app avoids Redux/Zustand until real complexity requires it. Server calls remain isolated behind the Axios API client.