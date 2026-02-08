using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShopModule_Audit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropIndex(
            //     name: "IX_ShopProfiles_CitizenId",
            //     table: "ShopProfiles");

            migrationBuilder.AlterColumn<double>(
                name: "Rating",
                table: "ShopProfiles",
                type: "double",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double");

            migrationBuilder.AlterColumn<string>(
                name: "AdminNote",
                table: "ShopProfiles",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastResubmitTime",
                table: "ShopProfiles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResubmitCount",
                table: "ShopProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShopProfiles_CitizenId",
                table: "ShopProfiles",
                column: "CitizenId",
                unique: true,
                filter: "`CitizenId` IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopProfiles_CitizenId",
                table: "ShopProfiles");

            migrationBuilder.DropColumn(
                name: "LastResubmitTime",
                table: "ShopProfiles");

            migrationBuilder.DropColumn(
                name: "ResubmitCount",
                table: "ShopProfiles");

            migrationBuilder.AlterColumn<double>(
                name: "Rating",
                table: "ShopProfiles",
                type: "double",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double",
                oldDefaultValue: 0.0);

            migrationBuilder.AlterColumn<string>(
                name: "AdminNote",
                table: "ShopProfiles",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ShopProfiles_CitizenId",
                table: "ShopProfiles",
                column: "CitizenId",
                unique: true,
                filter: "`CitizenId` IS NOT NULL");
        }
    }
}
