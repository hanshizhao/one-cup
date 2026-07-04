using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneCup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefinePermissionCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000104"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000106"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000108"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000110"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000112"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000113"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000114"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000115"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000116"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000121"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000122"));

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000101"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000102"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000103"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000105"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000107"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000109"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000111"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000117"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000103"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000105"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000107"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000109"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000111"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000117"));

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000301"), "fabric:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看面料开发", null },
                    { new Guid("00000000-0000-0000-0000-000000000302"), "fabric:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入面料开发", null },
                    { new Guid("00000000-0000-0000-0000-000000000303"), "fabric:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑面料开发", null },
                    { new Guid("00000000-0000-0000-0000-000000000304"), "fabric:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除面料开发", null },
                    { new Guid("00000000-0000-0000-0000-000000000305"), "material:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看原料物料", null },
                    { new Guid("00000000-0000-0000-0000-000000000306"), "material:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入原料物料", null },
                    { new Guid("00000000-0000-0000-0000-000000000307"), "material:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑原料物料", null },
                    { new Guid("00000000-0000-0000-0000-000000000308"), "material:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除原料物料", null },
                    { new Guid("00000000-0000-0000-0000-000000000309"), "equipment:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看设备", null },
                    { new Guid("00000000-0000-0000-0000-00000000030a"), "equipment:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入设备", null },
                    { new Guid("00000000-0000-0000-0000-00000000030b"), "equipment:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑设备", null },
                    { new Guid("00000000-0000-0000-0000-00000000030c"), "equipment:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除设备", null },
                    { new Guid("00000000-0000-0000-0000-00000000030d"), "customer:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看客户", null },
                    { new Guid("00000000-0000-0000-0000-00000000030e"), "customer:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入客户", null },
                    { new Guid("00000000-0000-0000-0000-00000000030f"), "customer:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑客户", null },
                    { new Guid("00000000-0000-0000-0000-000000000310"), "customer:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除客户", null },
                    { new Guid("00000000-0000-0000-0000-000000000311"), "color:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看颜色对色", null },
                    { new Guid("00000000-0000-0000-0000-000000000312"), "color:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入颜色对色", null },
                    { new Guid("00000000-0000-0000-0000-000000000313"), "color:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑颜色对色", null },
                    { new Guid("00000000-0000-0000-0000-000000000314"), "color:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除颜色对色", null },
                    { new Guid("00000000-0000-0000-0000-000000000315"), "product:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看产品", null },
                    { new Guid("00000000-0000-0000-0000-000000000316"), "product:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入产品", null },
                    { new Guid("00000000-0000-0000-0000-000000000317"), "product:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑产品", null },
                    { new Guid("00000000-0000-0000-0000-000000000318"), "product:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除产品", null },
                    { new Guid("00000000-0000-0000-0000-000000000319"), "system:user:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看用户", null },
                    { new Guid("00000000-0000-0000-0000-00000000031a"), "system:user:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "新增用户", null },
                    { new Guid("00000000-0000-0000-0000-00000000031b"), "system:user:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑用户", null },
                    { new Guid("00000000-0000-0000-0000-00000000031c"), "system:user:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除用户", null },
                    { new Guid("00000000-0000-0000-0000-00000000031d"), "system:user:reset-password", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "重置用户密码", null },
                    { new Guid("00000000-0000-0000-0000-00000000031e"), "system:role:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看角色", null },
                    { new Guid("00000000-0000-0000-0000-00000000031f"), "system:role:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "新增角色", null },
                    { new Guid("00000000-0000-0000-0000-000000000320"), "system:role:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑角色", null },
                    { new Guid("00000000-0000-0000-0000-000000000321"), "system:role:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除角色", null },
                    { new Guid("00000000-0000-0000-0000-000000000322"), "system:numbering:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看编号管理", null },
                    { new Guid("00000000-0000-0000-0000-000000000323"), "system:numbering:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "新增编号规则", null },
                    { new Guid("00000000-0000-0000-0000-000000000324"), "system:numbering:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑编号规则", null },
                    { new Guid("00000000-0000-0000-0000-000000000325"), "system:numbering:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除编号规则", null },
                    { new Guid("00000000-0000-0000-0000-000000000326"), "system:unit:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看计量单位", null },
                    { new Guid("00000000-0000-0000-0000-000000000327"), "system:unit:create", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "新增计量单位", null },
                    { new Guid("00000000-0000-0000-0000-000000000328"), "system:unit:update", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "编辑计量单位", null },
                    { new Guid("00000000-0000-0000-0000-000000000329"), "system:unit:delete", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "删除计量单位", null },
                    { new Guid("00000000-0000-0000-0000-00000000032a"), "system:audit:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看审计日志", null }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000301"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000302"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000303"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000304"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000305"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000309"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-00000000030d"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000311"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000315"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-00000000032a"), new Guid("00000000-0000-0000-0000-000000000003") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000306"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000307"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000308"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000030a"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000030b"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000030c"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000030e"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000030f"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000310"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000312"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000313"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000314"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000316"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000317"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000318"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000319"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000031a"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000031b"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000031c"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000031d"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000031e"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000031f"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000320"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000321"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000322"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000323"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000324"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000325"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000326"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000327"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000328"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000329"));

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000301"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000302"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000303"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000304"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000305"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000309"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-00000000030d"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000311"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000315"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-00000000032a"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000301"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000302"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000303"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000304"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000305"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000309"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000030d"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000311"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000315"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000032a"));

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "description", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), "fabric:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看面料开发", null },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "fabric:write", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入/编辑面料开发", null },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "material:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看原料物料", null },
                    { new Guid("00000000-0000-0000-0000-000000000104"), "material:write", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "维护原料物料", null },
                    { new Guid("00000000-0000-0000-0000-000000000105"), "equipment:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看设备", null },
                    { new Guid("00000000-0000-0000-0000-000000000106"), "equipment:write", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "维护设备", null },
                    { new Guid("00000000-0000-0000-0000-000000000107"), "customer:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看客户", null },
                    { new Guid("00000000-0000-0000-0000-000000000108"), "customer:write", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "维护客户", null },
                    { new Guid("00000000-0000-0000-0000-000000000109"), "color:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看颜色对色", null },
                    { new Guid("00000000-0000-0000-0000-000000000110"), "color:write", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "维护颜色对色", null },
                    { new Guid("00000000-0000-0000-0000-000000000111"), "product:read", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看产品", null },
                    { new Guid("00000000-0000-0000-0000-000000000112"), "system:user:manage", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "管理用户", null },
                    { new Guid("00000000-0000-0000-0000-000000000113"), "system:role:manage", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "管理角色与权限", null },
                    { new Guid("00000000-0000-0000-0000-000000000114"), "system:numbering:view", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看编号管理", null },
                    { new Guid("00000000-0000-0000-0000-000000000115"), "system:numbering:manage", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "管理编号规则", null },
                    { new Guid("00000000-0000-0000-0000-000000000116"), "product:write", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "录入/编辑产品", null },
                    { new Guid("00000000-0000-0000-0000-000000000117"), "system:audit:view", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看审计日志", null },
                    { new Guid("00000000-0000-0000-0000-000000000121"), "system:unit:view", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "查看计量单位", null },
                    { new Guid("00000000-0000-0000-0000-000000000122"), "system:unit:manage", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "管理计量单位", null }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000102"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000103"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000105"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000107"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000109"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000111"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000117"), new Guid("00000000-0000-0000-0000-000000000003") }
                });
        }
    }
}
