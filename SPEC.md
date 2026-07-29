## Risk Register: list and capture

**Type:** Feature
**Stack:** .NET 8 Web API (C#) + React 19 / TypeScript / Vite
**Estimate:** 1 point

### Background

Our compliance customers currently track operational risks in spreadsheets. We're building
the first slice of an in-app **Risk Register** so a team can log a risk, see it scored
consistently, and review the register in one place.

This ticket covers the register list and the capture form. Editing, deletion, and audit
history are out of scope and will be separate tickets.

### Description

A **Risk** is a potential future event that could harm the business. Each risk is scored on
two axes, both integers from 1 to 5:

- **Likelihood** — how probable the event is
- **Impact** — how damaging it would be if it happened

The **risk score** is `likelihood × impact`, giving a value from 1 to 25. Each score falls
into a **severity band**, which is what management actually reads:

| Score range | Severity |
| --- | --- |
| 1–4 | Low |
| 5–9 | Medium |
| 10–15 | High |
| 16–25 | Critical |

A risk also has a **title**, an optional **description**, an **owner** (the person
accountable), a **status**, and a **created date**.

Statuses are: `Open`, `Mitigating`, `Accepted`, `Closed`.

### API requirements

**`GET /api/risks`**

Returns the register. Supports an optional `status` query parameter to filter
(`/api/risks?status=Open`). Results are ordered by risk score, highest first — the whole
point of a register is that the worst things are at the top.

**`POST /api/risks`**

Creates a risk. Accepts title, description, likelihood, impact, owner. Returns the created
risk including its computed score and severity.

Validation rules:

- `title` — required, 3 to 200 characters
- `likelihood` — required, integer 1–5
- `impact` — required, integer 1–5
- `owner` — required, 1 to 100 characters
- `description` — optional, max 2000 characters

Invalid submissions must return a structured error response the frontend can map back to
individual fields. Do not return a bare string.

### Frontend requirements

A single page with two parts:

**The register list**

- Shows title, owner, likelihood, impact, score, severity, and status
- Severity is visually distinguishable at a glance
- Handles loading, error, and empty states — an empty register should say something
  useful, not render a blank box
- A control to filter by status

**The capture form**

- Fields for title, description, likelihood, impact, owner
- Submit is disabled while the request is in flight
- Server validation errors are shown against the field that caused them
- On success, the new risk appears in the list without a full page reload

---

## Acceptance Criteria

### API

- [ ] `GET /api/risks` returns `200` with an array of risks
- [ ] `GET /api/risks?status=Open` returns only risks with that status
- [ ] `GET /api/risks?status=Nonsense` behaves sensibly and predictably (your call — but be
      able to justify it)
- [ ] Results are ordered by score descending
- [ ] `POST /api/risks` with a valid body returns `201` with the created risk in the body
- [ ] The response includes `score` and `severity`, computed server-side
- [ ] `POST /api/risks` with an invalid body returns a structured validation error naming
      the offending field(s)
- [ ] A risk with likelihood 3 and impact 3 has score 9 and severity `Medium`
- [ ] A risk with likelihood 5 and impact 5 has score 25 and severity `Critical`
- [ ] A risk with likelihood 1 and impact 1 has score 1 and severity `Low`
- [ ] Endpoints are async and do not block

### Frontend

- [ ] The register renders on load with a visible loading state first
- [ ] An API failure shows an error state, not a blank page or a crash
- [ ] An empty register shows an empty state with guidance
- [ ] Severity is visually distinct per band
- [ ] The status filter changes what is listed
- [ ] Submitting an invalid form shows the error against the correct field
- [ ] Submit is disabled while the request is in flight
- [ ] A successful submission adds the risk to the list without a page reload
- [ ] API response types are declared — no `any` at the boundary

### Quality

- [ ] At least one test covering the score → severity band mapping, including the
      boundary values
- [ ] At least one test covering a validation failure
- [ ] The app builds and runs

---
