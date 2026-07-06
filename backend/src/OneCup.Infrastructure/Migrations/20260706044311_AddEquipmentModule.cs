using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_templates_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_equipment_templates_processes_process_id",
                        column: x => x.process_id,
                        principalTable: "processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment_type_parameters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value_type = table.Column<int>(type: "integer", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    min_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    max_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    precision = table.Column<int>(type: "integer", nullable: true),
                    options = table.Column<string>(type: "text", nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_type_parameters", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_type_parameters_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_equipment_type_parameters_measurement_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "measurement_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    equipment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    supplier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: true),
                    warranty_expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipments", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipments_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment_template_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_template_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_template_values_equipment_templates_equipment_tem~",
                        column: x => x.equipment_template_id,
                        principalTable: "equipment_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "numbering_target_types",
                columns: new[] { "id", "code", "created_at", "is_active", "name_en", "name_zh", "sort_order", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000208"), "equipment-type", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "EquipmentType", "设备类型", 8, null });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-00000000032f"), "equipment-type:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看设备类型", null },
                    { new Guid("00000000-0000-0000-0000-000000000330"), "equipment-type:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入设备类型", null },
                    { new Guid("00000000-0000-0000-0000-000000000331"), "equipment-type:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑设备类型", null },
                    { new Guid("00000000-0000-0000-0000-000000000332"), "equipment-type:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除设备类型", null }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000032f"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.CreateIndex(
                name: "ux_equipment_template_values_template_parameter",
                table: "equipment_template_values",
                columns: new[] { "equipment_template_id", "parameter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_equipment_templates_process_id",
                table: "equipment_templates",
                column: "process_id");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_templates_type_process_name",
                table: "equipment_templates",
                columns: new[] { "equipment_type_id", "process_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_equipment_type_parameters_unit_id",
                table: "equipment_type_parameters",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_type_parameters_type_name",
                table: "equipment_type_parameters",
                columns: new[] { "equipment_type_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_equipment_types_code",
                table: "equipment_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_equipment_types_name",
                table: "equipment_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_equipments_equipment_type_id",
                table: "equipments",
                column: "equipment_type_id");

            migrationBuilder.CreateIndex(
                name: "ux_equipments_code",
                table: "equipments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_equipments_name",
                table: "equipments",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_template_values");

            migrationBuilder.DropTable(
                name: "equipment_type_parameters");

            migrationBuilder.DropTable(
                name: "equipments");

            migrationBuilder.DropTable(
                name: "equipment_templates");

            migrationBuilder.DropTable(
                name: "equipment_types");

            migrationBuilder.DeleteData(
                table: "numbering_target_types",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000208"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000330"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000331"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000332"));

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-00000000032f"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000032f"));
        }
    }
}
