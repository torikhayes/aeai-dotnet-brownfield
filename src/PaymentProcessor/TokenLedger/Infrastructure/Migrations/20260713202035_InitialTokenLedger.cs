using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentProcessor.TokenLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTokenLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokenAwardedListings",
                columns: table => new
                {
                    CatalogItemId = table.Column<string>(type: "text", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenAwardedListings", x => x.CatalogItemId);
                });

            migrationBuilder.CreateTable(
                name: "TokenAwardLookupEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubCategory = table.Column<string>(type: "text", nullable: false),
                    ConditionGrade = table.Column<string>(type: "text", nullable: false),
                    TokenAmount = table.Column<int>(type: "integer", nullable: false),
                    TableVersion = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenAwardLookupEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RelatedEventId = table.Column<string>(type: "text", nullable: false),
                    LookupTableVersion = table.Column<string>(type: "text", nullable: true),
                    CatalogItemId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenWallets",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Balance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenWallets", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenAwardLookupEntries_ClubCategory_ConditionGrade_TableVe~",
                table: "TokenAwardLookupEntries",
                columns: new[] { "ClubCategory", "ConditionGrade", "TableVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_RelatedEventId",
                table: "TokenTransactions",
                column: "RelatedEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransactions_UserId_CreatedAt",
                table: "TokenTransactions",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenAwardedListings");

            migrationBuilder.DropTable(
                name: "TokenAwardLookupEntries");

            migrationBuilder.DropTable(
                name: "TokenTransactions");

            migrationBuilder.DropTable(
                name: "TokenWallets");
        }
    }
}
