using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;

namespace RiskRegister.Infrastructure.Persistence.Configurations;

public class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    /// <summary>
    /// Likelihood × impact.
    /// </summary>
    /// <remarks>
    /// Both operands are CONVERTed to INT first: SQL Server does not widen TINYINT arithmetic, so
    /// TINYINT * TINYINT stays TINYINT and would overflow above 255. The cast removes that trap and
    /// makes the column INT, matching <see cref="Risk.Score"/>.
    /// <para>
    /// The ISNULL wrapper is what makes the column NOT NULL. SQL Server treats a computed column as
    /// nullable unless the expression is provably non-null, and does not infer that from the
    /// operands' own NOT NULL — without this the column is nullable while the CLR property is
    /// <see cref="int"/>. The fallback is unreachable: both operands are NOT NULL.
    /// </para>
    /// </remarks>
    private const string ScoreSql =
        "ISNULL(CONVERT(INT, [Likelihood]) * CONVERT(INT, [Impact]), 0)";

    public void Configure(EntityTypeBuilder<Risk> builder)
    {
        builder.ToTable("Risks", table =>
        {
            // The API validates first and returns field-mapped errors; these are the backstop so
            // no code path can write a row SPEC.md forbids. MaxLength alone cannot express a
            // minimum, which is why Title and Owner need constraints as well as lengths.
            table.HasCheckConstraint("CK_Risks_Title", "LEN([Title]) BETWEEN 3 AND 200");
            table.HasCheckConstraint("CK_Risks_Owner", "LEN([Owner]) BETWEEN 1 AND 100");
            table.HasCheckConstraint("CK_Risks_Likelihood", "[Likelihood] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Risks_Impact", "[Impact] BETWEEN 1 AND 5");
            table.HasCheckConstraint(
                "CK_Risks_Status",
                "[Status] IN (N'Open', N'Mitigating', N'Accepted', N'Closed')");
            // CreatedUtc is documented as UTC; enforce it rather than trust it.
            table.HasCheckConstraint("CK_Risks_CreatedUtc", "DATEPART(TZOFFSET, [CreatedUtc]) = 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.Property(r => r.Owner)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Score)
            .HasComputedColumnSql(ScoreSql, stored: true);

        // Stored as the enum name, so rows read as 'Open'/'Closed' in ad-hoc SQL and the stored
        // value is byte-identical to what the API accepts and returns. A TINYINT would leave the
        // meaning only in C#, where reordering the enum silently corrupts existing rows.
        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(RiskStatus.Open);

        // datetimeoffset, not datetime2: it round-trips to DateTimeOffset and serialises with an
        // explicit +00:00, where datetime2 comes back as DateTime with Kind=Unspecified and
        // reaches the frontend with no zone marker. SYSUTCDATETIME() converts to offset zero.
        builder.Property(r => r.CreatedUtc)
            .IsRequired()
            .HasPrecision(3)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Ignore(r => r.Severity);

        // Both indexes carry the full ORDER BY of the register, so the sort is free. Id is the
        // final tie-break: 3 × 4 and 4 × 3 both score 12, which SPEC.md leaves undefined.
        builder.HasIndex(r => new { r.Score, r.CreatedUtc, r.Id })
            .HasDatabaseName("IX_Risks_Score")
            .IsDescending(true, true, true);

        builder.HasIndex(r => new { r.Status, r.Score, r.CreatedUtc, r.Id })
            .HasDatabaseName("IX_Risks_Status_Score")
            .IsDescending(false, true, true, true);
    }
}
