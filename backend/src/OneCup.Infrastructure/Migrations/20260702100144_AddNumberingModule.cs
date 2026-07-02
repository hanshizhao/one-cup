using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "numbering_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    include_category = table.Column<bool>(type: "boolean", nullable: false),
                    date_segment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    seq_length = table.Column<short>(type: "smallint", nullable: false),
                    separator = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    reset_period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    remark = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "numbering_counters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: ""),
                    period_key = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: ""),
                    current_seq = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_counters", x => x.id);
                    table.ForeignKey(
                        name: "FK_numbering_counters_numbering_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "numbering_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "numbering_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    category_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    period_key = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    seq_value = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_numbering_logs_numbering_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "numbering_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000114"), "system:numbering:view", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看编号管理", null },
                    { new Guid("00000000-0000-0000-0000-000000000115"), "system:numbering:manage", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "管理编号规则", null }
                });

            migrationBuilder.CreateIndex(
                name: "ux_numbering_counters_bucket",
                table: "numbering_counters",
                columns: new[] { "rule_id", "category_code", "period_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_numbering_logs_code",
                table: "numbering_logs",
                column: "generated_code");

            migrationBuilder.CreateIndex(
                name: "ix_numbering_logs_rule_id",
                table: "numbering_logs",
                columns: new[] { "rule_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_numbering_logs_target_type",
                table: "numbering_logs",
                columns: new[] { "target_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_numbering_rules_target_type",
                table: "numbering_rules",
                column: "target_type");

            migrationBuilder.CreateIndex(
                name: "ux_numbering_rules_target_type_active",
                table: "numbering_rules",
                columns: new[] { "target_type", "is_active" },
                unique: true,
                filter: "\"is_active\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "numbering_counters");

            migrationBuilder.DropTable(
                name: "numbering_logs");

            migrationBuilder.DropTable(
                name: "numbering_rules");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000114"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000115"));
        }
    }
}
