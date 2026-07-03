using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberingDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "numbering_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_zh = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_en = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "numbering_target_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_zh = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_en = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_target_types", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "numbering_target_types",
                columns: new[] { "id", "code", "created_at", "is_active", "name_en", "name_zh", "sort_order", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000201"), "fabric", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Fabric", "面料", 1, null },
                    { new Guid("00000000-0000-0000-0000-000000000202"), "material", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Material", "原料", 2, null },
                    { new Guid("00000000-0000-0000-0000-000000000203"), "equipment", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Equipment", "设备", 3, null },
                    { new Guid("00000000-0000-0000-0000-000000000204"), "customer", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Customer", "客户", 4, null },
                    { new Guid("00000000-0000-0000-0000-000000000205"), "color", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Color", "颜色", 5, null },
                    { new Guid("00000000-0000-0000-0000-000000000206"), "product", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Product", "产品", 6, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_numbering_categories_target_type",
                table: "numbering_categories",
                column: "target_type_code");

            migrationBuilder.CreateIndex(
                name: "ux_numbering_categories_type_code",
                table: "numbering_categories",
                columns: new[] { "target_type_code", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_numbering_target_types_code",
                table: "numbering_target_types",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "numbering_categories");

            migrationBuilder.DropTable(
                name: "numbering_target_types");
        }
    }
}
