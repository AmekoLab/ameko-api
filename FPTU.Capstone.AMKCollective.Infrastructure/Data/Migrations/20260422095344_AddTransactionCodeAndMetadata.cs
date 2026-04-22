using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCodeAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IdempotencyKey if not exists
            migrationBuilder.Sql(@"
                SET @col1 = (SELECT COUNT(1) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Transactions' AND COLUMN_NAME = 'IdempotencyKey');
                SET @ddl1 = IF(@col1 = 0,
                    'ALTER TABLE `Transactions` ADD COLUMN `IdempotencyKey` varchar(100) CHARACTER SET utf8mb4 NULL',
                    'SELECT 1');
                PREPARE s1 FROM @ddl1; EXECUTE s1; DEALLOCATE PREPARE s1;
            ");

            // Add MetadataJson if not exists
            migrationBuilder.Sql(@"
                SET @col2 = (SELECT COUNT(1) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Transactions' AND COLUMN_NAME = 'MetadataJson');
                SET @ddl2 = IF(@col2 = 0,
                    'ALTER TABLE `Transactions` ADD COLUMN `MetadataJson` json NULL',
                    'SELECT 1');
                PREPARE s2 FROM @ddl2; EXECUTE s2; DEALLOCATE PREPARE s2;
            ");

            // Add TransactionCode if not exists
            migrationBuilder.Sql(@"
                SET @col3 = (SELECT COUNT(1) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Transactions' AND COLUMN_NAME = 'TransactionCode');
                SET @ddl3 = IF(@col3 = 0,
                    'ALTER TABLE `Transactions` ADD COLUMN `TransactionCode` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT \'\'',
                    'SELECT 1');
                PREPARE s3 FROM @ddl3; EXECUTE s3; DEALLOCATE PREPARE s3;
            ");

            // Backfill unique codes for any existing rows before creating the unique index
            migrationBuilder.Sql("UPDATE `Transactions` SET `TransactionCode` = CONCAT('TXN-LEGACY-', REPLACE(CAST(`Id` AS CHAR), '-', '')) WHERE `TransactionCode` = ''");

            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Wallets` (`Id`, `Balance`, `CreatedAt`, `CreatedBy`, `Currency`, `HeldBalance`, `IsActive`, `IsDeleted`, `PinHash`, `PinResetCode`, `PinResetExpiry`, `UpdatedAt`, `UpdatedBy`, `UserId`)
                VALUES ('99999999-9999-9999-9999-999999999999', 0, '2024-01-01 00:00:00', NULL, 'VND', 0, 1, 0, NULL, NULL, NULL, NULL, NULL, '00000000-0000-0000-0000-000000000001')
            ");

            // Create IX_Transactions_IdempotencyKey if not exists
            migrationBuilder.Sql(@"
                SET @idx1 = (SELECT COUNT(1) FROM information_schema.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Transactions' AND INDEX_NAME = 'IX_Transactions_IdempotencyKey');
                SET @ddl4 = IF(@idx1 = 0,
                    'CREATE UNIQUE INDEX `IX_Transactions_IdempotencyKey` ON `Transactions` (`IdempotencyKey`)',
                    'SELECT 1');
                PREPARE s4 FROM @ddl4; EXECUTE s4; DEALLOCATE PREPARE s4;
            ");

            // Create IX_Transactions_TransactionCode if not exists
            migrationBuilder.Sql(@"
                SET @idx2 = (SELECT COUNT(1) FROM information_schema.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Transactions' AND INDEX_NAME = 'IX_Transactions_TransactionCode');
                SET @ddl5 = IF(@idx2 = 0,
                    'CREATE UNIQUE INDEX `IX_Transactions_TransactionCode` ON `Transactions` (`TransactionCode`)',
                    'SELECT 1');
                PREPARE s5 FROM @ddl5; EXECUTE s5; DEALLOCATE PREPARE s5;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransactionCode",
                table: "Transactions");

            migrationBuilder.DeleteData(
                table: "Wallets",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"));

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransactionCode",
                table: "Transactions");
        }
    }
}
