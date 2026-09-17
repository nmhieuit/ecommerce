using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTestUserSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TenantId", "TwoFactorEnabled", "UserName" },
                values: new object[] { "9f2b1e2a-9b7a-4e3e-8f21-9a2c9f6b6a11", 0, "b3e9a5b0-9d5d-4f2e-8a30-3a1f6e0c6f9a", "postman-test@local.test", true, false, null, "POSTMAN-TEST@LOCAL.TEST", "POSTMAN-TEST@LOCAL.TEST", null, null, false, "5e2a2b1d-6f5c-4e0a-9a1a-7b1e6b8f2c3d", "contoso", false, "postman-test@local.test" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "9f2b1e2a-9b7a-4e3e-8f21-9a2c9f6b6a11");
        }
    }
}
