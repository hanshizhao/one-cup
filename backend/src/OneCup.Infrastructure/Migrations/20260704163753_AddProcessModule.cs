using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processes", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "numbering_target_types",
                columns: new[] { "id", "code", "created_at", "is_active", "name_en", "name_zh", "sort_order", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000207"), "process", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Process", "工序", 7, null });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-00000000032b"), "process:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看工序", null },
                    { new Guid("00000000-0000-0000-0000-00000000032c"), "process:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入工序", null },
                    { new Guid("00000000-0000-0000-0000-00000000032d"), "process:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑工序", null },
                    { new Guid("00000000-0000-0000-0000-00000000032e"), "process:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除工序", null }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000032b"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.CreateIndex(
                name: "IX_processes_code",
                table: "processes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processes_name_category",
                table: "processes",
                columns: new[] { "name", "category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processes");

            migrationBuilder.DeleteData(
                table: "numbering_target_types",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000207"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000032c"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000032d"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000032e"));

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-00000000032b"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000032b"));
        }
    }
}
