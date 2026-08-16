using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Products.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("9f8d6b1e-0001-4000-8000-000000000001"), "Field Notes Notebook", 12.50m },
                    { new Guid("9f8d6b1e-0001-4000-8000-000000000002"), "Ceramic Pour-Over Set", 48.00m },
                    { new Guid("9f8d6b1e-0001-4000-8000-000000000003"), "Linen Apron", 34.25m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("9f8d6b1e-0001-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("9f8d6b1e-0001-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("9f8d6b1e-0001-4000-8000-000000000003"));
        }
    }
}
