using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSoftDeleteAndProductWrite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "name", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000116"), "product:write", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入/编辑产品", null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "is_deleted",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000116"));

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "users");
        }
    }
}
