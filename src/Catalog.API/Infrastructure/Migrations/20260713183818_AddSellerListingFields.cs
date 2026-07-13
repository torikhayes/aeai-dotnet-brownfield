using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eShop.Catalog.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerListingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "Catalog",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManufactureYear",
                table: "Catalog",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrls",
                table: "Catalog",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerId",
                table: "Catalog",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "ManufactureYear",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "PhotoUrls",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Catalog");
        }
    }
}
