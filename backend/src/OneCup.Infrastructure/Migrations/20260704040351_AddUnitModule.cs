using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "measurement_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_zh = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_en = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_base = table.Column<bool>(type: "boolean", nullable: false),
                    factor = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    precision = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_units", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "measurement_units",
                columns: new[] { "id", "category", "code", "created_at", "factor", "is_active", "is_base", "name_en", "name_zh", "precision", "sort_order", "symbol", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000010001"), "LENGTH", "meter", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, true, "Meter", "米", 2, 1, "m", null },
                    { new Guid("00000000-0000-0000-0000-000000010002"), "LENGTH", "decimeter", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.1m, true, false, "Decimeter", "分米", 2, 2, "dm", null },
                    { new Guid("00000000-0000-0000-0000-000000010003"), "LENGTH", "centimeter", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.01m, true, false, "Centimeter", "厘米", 2, 3, "cm", null },
                    { new Guid("00000000-0000-0000-0000-000000010004"), "LENGTH", "yard", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.9144m, true, false, "Yard", "码", 2, 4, "yd", null },
                    { new Guid("00000000-0000-0000-0000-000000010005"), "LENGTH", "foot", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.3048m, true, false, "Foot", "英尺", 2, 5, "ft", null },
                    { new Guid("00000000-0000-0000-0000-000000010010"), "WEIGHT", "kilogram", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, true, "Kilogram", "千克", 2, 1, "kg", null },
                    { new Guid("00000000-0000-0000-0000-000000010011"), "WEIGHT", "gram", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.001m, true, false, "Gram", "克", 2, 2, "g", null },
                    { new Guid("00000000-0000-0000-0000-000000010012"), "WEIGHT", "ton", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1000m, true, false, "Ton", "吨", 2, 3, "t", null },
                    { new Guid("00000000-0000-0000-0000-000000010013"), "WEIGHT", "pound", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.453592m, true, false, "Pound", "磅", 2, 4, "lb", null },
                    { new Guid("00000000-0000-0000-0000-000000010020"), "AREA", "square_meter", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, true, "Square Meter", "平方米", 2, 1, "㎡", null },
                    { new Guid("00000000-0000-0000-0000-000000010021"), "AREA", "square_yard", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.836127m, true, false, "Square Yard", "平方码", 2, 2, "yd²", null },
                    { new Guid("00000000-0000-0000-0000-000000010030"), "COUNT", "piece", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, true, "Piece", "件", 0, 1, "件", null },
                    { new Guid("00000000-0000-0000-0000-000000010031"), "COUNT", "roll", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, false, "Roll", "卷", 0, 2, "卷", null },
                    { new Guid("00000000-0000-0000-0000-000000010032"), "COUNT", "bolt", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, false, "Bolt", "匹", 0, 3, "匹", null },
                    { new Guid("00000000-0000-0000-0000-000000010033"), "COUNT", "set", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, false, "Set", "套", 0, 4, "套", null },
                    { new Guid("00000000-0000-0000-0000-000000010040"), "VOLUME", "liter", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, true, "Liter", "升", 2, 1, "L", null },
                    { new Guid("00000000-0000-0000-0000-000000010041"), "VOLUME", "milliliter", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.001m, true, false, "Milliliter", "毫升", 2, 2, "mL", null },
                    { new Guid("00000000-0000-0000-0000-000000010050"), "YARN", "tex", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m, true, true, "Tex", "特", 2, 1, "tex", null },
                    { new Guid("00000000-0000-0000-0000-000000010051"), "YARN", "dtex", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10m, true, false, "Decitex", "分特", 2, 2, "dtex", null },
                    { new Guid("00000000-0000-0000-0000-000000010052"), "YARN", "denier", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), 9m, true, false, "Denier", "旦尼尔", 2, 3, "D", null }
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000121"), "system:unit:view", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看计量单位", null },
                    { new Guid("00000000-0000-0000-0000-000000000122"), "system:unit:manage", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "管理计量单位", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_measurement_units_category",
                table: "measurement_units",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ux_measurement_units_code",
                table: "measurement_units",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "measurement_units");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000121"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000122"));
        }
    }
}
