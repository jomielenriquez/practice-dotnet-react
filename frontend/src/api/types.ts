/**
 * Types for the API boundary. Every response the app consumes is declared here
 * so nothing crosses the boundary as `any`.
 *
 * Property names are camelCase because that is ASP.NET Core's default JSON
 * naming policy — these mirror the C# records one-for-one.
 */

/**
 * Mirrors the `Severity` enum in
 * backend/src/RiskRegister.Core/Enums/Severity.cs.
 *
 * A union of the enum's *member names*, not its ordinals, because `Program.cs`
 * registers `JsonStringEnumConverter` — the wire carries `"Critical"`, not `3`.
 * A string-literal union rather than a TS enum: `erasableSyntaxOnly` is on.
 */
export type Severity = 'Low' | 'Medium' | 'High' | 'Critical'

/**
 * Mirrors the `RiskStatus` enum in
 * backend/src/RiskRegister.Core/Enums/RiskStatus.cs. Names again, per above —
 * and these names are also the values stored in `dbo.Risks.Status`.
 */
export type RiskStatus = 'Open' | 'Mitigating' | 'Accepted' | 'Closed'

/** Mirrors `RiskResponse` in backend/src/RiskRegister.Api/Contracts/RiskResponse.cs */
export interface Risk {
  id: number
  title: string
  /** The only nullable field on the record; absent descriptions arrive as `null`. */
  description: string | null
  /** The person accountable for the risk. */
  owner: string
  /** 1–5. A C# `byte`, but there is no byte on the wire. */
  likelihood: number
  /** 1–5, as above. */
  impact: number
  /** `likelihood × impact`, 1–25. Computed by SQL Server, never by the client. */
  score: number
  /** Derived from `score` server-side by `RiskScoring.SeverityFor`. */
  severity: Severity
  status: RiskStatus
  /** ISO-8601 instant, serialised from a C# `DateTimeOffset`. */
  createdUtc: string
}
