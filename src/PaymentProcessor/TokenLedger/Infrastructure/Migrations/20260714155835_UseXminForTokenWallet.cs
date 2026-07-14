using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentProcessor.TokenLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseXminForTokenWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TokenWallets");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "TokenWallets",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "TokenWallets");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TokenWallets",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
