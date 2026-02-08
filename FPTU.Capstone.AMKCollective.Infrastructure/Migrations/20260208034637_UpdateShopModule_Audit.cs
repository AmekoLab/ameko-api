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

            // Safe ADD COLUMN - skip if column already exists
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ShopProfiles' AND COLUMN_NAME = 'LastResubmitTime');
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE `ShopProfiles` ADD `LastResubmitTime` datetime(6) NULL', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ShopProfiles' AND COLUMN_NAME = 'ResubmitCount');
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE `ShopProfiles` ADD `ResubmitCount` int NOT NULL DEFAULT 0', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Safe CREATE INDEX - skip if index already exists
            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ShopProfiles' AND INDEX_NAME = 'IX_ShopProfiles_CitizenId');
                SET @sql = IF(@idx_exists = 0, 'CREATE UNIQUE INDEX `IX_ShopProfiles_CitizenId` ON `ShopProfiles` (`CitizenId`)', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
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
