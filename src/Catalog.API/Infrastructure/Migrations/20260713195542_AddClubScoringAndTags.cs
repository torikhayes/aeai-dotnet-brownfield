using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace eShop.Catalog.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClubScoringAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "AverageRating",
                table: "Catalog",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "FavoriteCount",
                table: "Catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "Catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SellerId",
                table: "Catalog",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Catalog",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "Catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CatalogItemFavorite",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CatalogItemId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemFavorite", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemFavorite_Catalog_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "Catalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItemRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CatalogItemId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemRating_Catalog_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "Catalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_SellerId",
                table: "Catalog",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemFavorite_CatalogItemId_UserId",
                table: "CatalogItemFavorite",
                columns: new[] { "CatalogItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemRating_CatalogItemId_UserId",
                table: "CatalogItemRating",
                columns: new[] { "CatalogItemId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItemFavorite");

            migrationBuilder.DropTable(
                name: "CatalogItemRating");

            migrationBuilder.DropIndex(
                name: "IX_Catalog_SellerId",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "FavoriteCount",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "Catalog");
        }
    }
}
