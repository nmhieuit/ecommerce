using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Baskets.Api.Migrations
{
    /// <inheritdoc />
    public partial class BasketLineItems : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// The CustomerId → CustomerRef swap is non-additive, against Principle X's expand/contract
        /// preference, and is taken as one step deliberately: the column has no released consumer,
        /// no deployed version reads it, and the table is empty (004 plan.md, Complexity Tracking).
        /// Note that on a table that was <em>not</em> empty this would fail — every existing row
        /// would take the "" default and then collide on the new unique index. Had there been data
        /// worth keeping, this would be an expand/contract pair instead.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Baskets");

            migrationBuilder.AddColumn<string>(
                name: "CustomerRef",
                table: "Baskets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BasketLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BasketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BasketLineItems_Baskets_BasketId",
                        column: x => x.BasketId,
                        principalTable: "Baskets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Baskets_CustomerRef",
                table: "Baskets",
                column: "CustomerRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BasketLineItems_BasketId_ProductId",
                table: "BasketLineItems",
                columns: new[] { "BasketId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BasketLineItems");

            migrationBuilder.DropIndex(
                name: "IX_Baskets_CustomerRef",
                table: "Baskets");

            migrationBuilder.DropColumn(
                name: "CustomerRef",
                table: "Baskets");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Baskets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
