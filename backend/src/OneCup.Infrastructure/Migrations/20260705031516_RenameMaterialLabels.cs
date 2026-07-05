using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameMaterialLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "numbering_target_types",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000202"),
                column: "name_zh",
                value: "物料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000305"),
                column: "name",
                value: "查看物料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000306"),
                column: "name",
                value: "录入物料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000307"),
                column: "name",
                value: "编辑物料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000308"),
                column: "name",
                value: "删除物料");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "numbering_target_types",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000202"),
                column: "name_zh",
                value: "原料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000305"),
                column: "name",
                value: "查看原料物料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000306"),
                column: "name",
                value: "录入原料物料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000307"),
                column: "name",
                value: "编辑原料物料");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000308"),
                column: "name",
                value: "删除原料物料");
        }
    }
}
