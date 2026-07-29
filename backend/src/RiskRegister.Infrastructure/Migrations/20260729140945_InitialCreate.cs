using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskRegister.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Risks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Likelihood = table.Column<byte>(type: "tinyint", nullable: false),
                    Impact = table.Column<byte>(type: "tinyint", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false, computedColumnSql: "ISNULL(CONVERT(INT, [Likelihood]) * CONVERT(INT, [Impact]), 0)", stored: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Risks", x => x.Id);
                    table.CheckConstraint("CK_Risks_CreatedUtc", "DATEPART(TZOFFSET, [CreatedUtc]) = 0");
                    table.CheckConstraint("CK_Risks_Impact", "[Impact] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Risks_Likelihood", "[Likelihood] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Risks_Owner", "LEN([Owner]) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_Risks_Status", "[Status] IN (N'Open', N'Mitigating', N'Accepted', N'Closed')");
                    table.CheckConstraint("CK_Risks_Title", "LEN([Title]) BETWEEN 3 AND 200");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Risks_Score",
                table: "Risks",
                columns: new[] { "Score", "CreatedUtc", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Risks_Status_Score",
                table: "Risks",
                columns: new[] { "Status", "Score", "CreatedUtc", "Id" },
                descending: new[] { false, true, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Risks");
        }
    }
}
