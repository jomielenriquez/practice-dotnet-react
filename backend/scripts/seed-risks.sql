-- Sample data for local development. Not part of the schema: seeding through EF's HasData would
-- bake demo rows into a migration, and these are illustrative, not reference data.
--
-- Run from the repo root, once the container is healthy and the migration is applied:
--
--   PW=$(grep MSSQL_SA_PASSWORD .env | cut -d= -f2-)
--   docker cp backend/scripts/seed-risks.sql riskregister-mssql:/tmp/seed-risks.sql
--   docker exec riskregister-mssql /opt/mssql-tools18/bin/sqlcmd \
--     -S localhost -U sa -P "$PW" -C -I -d RiskRegister -i /tmp/seed-risks.sql
--
-- -I matters: QUOTED_IDENTIFIER must be ON to write to a table with an index on a computed column,
-- and sqlcmd defaults it off.
--
-- Score is never supplied — it is computed. CreatedUtc is supplied only where a specific ordering
-- is being demonstrated; elsewhere it defaults to SYSUTCDATETIME().

SET NOCOUNT ON;

DELETE FROM dbo.Risks;
DBCC CHECKIDENT('dbo.Risks', RESEED, 0) WITH NO_INFOMSGS;

INSERT INTO dbo.Risks (Title, Description, Owner, Likelihood, Impact, Status, CreatedUtc) VALUES
-- Critical, 16-25
(N'Customer database has no tested restore path',
 N'Backups run nightly but a restore has never been rehearsed. Recovery time is unknown.',
 N'Priya Raman', 5, 5, N'Open',       '2026-07-01T09:15:00+00:00'),
(N'Single cloud region with no failover',
 N'The whole platform runs in one region. A regional outage takes the product down entirely.',
 N'Marcus Bell', 4, 5, N'Mitigating', '2026-07-03T14:20:00+00:00'),
(N'Payment provider contract expires in 60 days',
 N'Renewal talks have not started. Lapsing would stop card payments outright.',
 N'Elena Fischer', 4, 4, N'Open',     '2026-07-05T08:00:00+00:00'),

-- High, 10-15
(N'Sole maintainer of the billing service',
 N'One engineer holds all working knowledge of billing. No pairing, no runbook.',
 N'Priya Raman', 3, 5, N'Mitigating', '2026-07-08T11:30:00+00:00'),
-- These two both score 12, which is exactly the tie SPEC.md leaves undefined. Newest first.
(N'Third-party analytics SDK ships unreviewed updates',
 N'The vendor auto-updates a script we load on every page. No changelog, no pinning.',
 N'Tom Whitfield', 3, 4, N'Open',     '2026-07-10T16:45:00+00:00'),
(N'Access reviews have not run for two quarters',
 N'Leavers may retain production access. No evidence trail for the auditor.',
 N'Sarah Ng', 4, 3, N'Open',          '2026-07-12T10:05:00+00:00'),
(N'Staging uses a copy of production data',
 N'Real customer records sit in an environment with weaker access controls.',
 N'Sarah Ng', 2, 5, N'Accepted',      '2026-07-14T13:00:00+00:00'),

-- Medium, 5-9
(N'Dependency upgrades are manual and irregular',
 N'Security patches land whenever someone remembers. Two libraries are a major version behind.',
 N'Tom Whitfield', 3, 3, N'Open',     '2026-07-16T09:40:00+00:00'),
(N'On-call rota has single-person coverage overnight',
 N'No secondary escalation between 22:00 and 07:00.',
 N'Marcus Bell', 2, 3, N'Mitigating', '2026-07-18T17:25:00+00:00'),
(N'Office network shared with guest wifi',
 NULL,
 N'Elena Fischer', 2, 3, N'Accepted', '2026-07-20T12:10:00+00:00'),

-- Low, 1-4
(N'Internal wiki search returns stale pages',
 N'Slows onboarding. Annoying rather than harmful.',
 N'Sarah Ng', 2, 2, N'Open',          '2026-07-22T15:55:00+00:00'),
(N'Legacy status page still linked from the footer',
 N'Points at a decommissioned host. Cosmetic, but confusing during an incident.',
 N'Tom Whitfield', 1, 2, N'Closed',   '2026-07-24T08:30:00+00:00');

SELECT Id, Score, Status, Title FROM dbo.Risks
ORDER BY Score DESC, CreatedUtc DESC, Id DESC;
