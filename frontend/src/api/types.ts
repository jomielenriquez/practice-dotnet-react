/**
 * Types for the API boundary. Every response the app consumes is declared here
 * so nothing crosses the boundary as `any`.
 *
 * Property names are camelCase because that is ASP.NET Core's default JSON
 * naming policy — these mirror the C# records one-for-one.
 */

/** Mirrors `HelloResponse` in backend/src/RiskRegister.Api/Program.cs */
export interface HelloResponse {
  message: string
  /** ISO-8601 instant, serialised from a C# `DateTimeOffset`. */
  utcNow: string
}
