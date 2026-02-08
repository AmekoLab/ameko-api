using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Addfielandchangeenumtoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. Fix Vouchers: Safe ADD - skip if column already exists ---
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Vouchers' AND COLUMN_NAME = 'Type');
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE `Vouchers` ADD `Type` int NOT NULL DEFAULT 0', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Vouchers' AND COLUMN_NAME = 'Status');
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE `Vouchers` ADD `Status` int NOT NULL DEFAULT 0', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Vouchers' AND COLUMN_NAME = 'DiscountType');
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE `Vouchers` ADD `DiscountType` int NOT NULL DEFAULT 0', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // --- 2. Fix Payments: Safe ADD - skip if column already exists ---
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Payments' AND COLUMN_NAME = 'Type');
                SET @sql = IF(@col_exists = 0, 'ALTER TABLE `Payments` ADD `Type` int NOT NULL DEFAULT 0', 'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Payments.Status is already Int in DB (per b.csv), so AlterColumn<int> is fine (metadata sync)
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Payments",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // --- 3. Fix Orders: Convert String Values to Int BEFORE Altering ---
            
            // Convert Orders.PaymentStatus
            migrationBuilder.Sql(@"
                UPDATE Orders SET PaymentStatus = CASE PaymentStatus
                    WHEN 'Pending' THEN 0 
                    WHEN 'Paid' THEN 1 
                    WHEN 'Failed' THEN 2 
                    WHEN 'Refunded' THEN 3
                    ELSE 0 
                END;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentStatus",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldDefaultValue: "Pending")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Convert Orders.OrderStatus
            migrationBuilder.Sql(@"
                UPDATE Orders SET OrderStatus = CASE OrderStatus
                    WHEN 'Pending' THEN '0' 
                    WHEN 'InCart' THEN '1' 
                    WHEN 'Processing' THEN '2' 
                    WHEN 'Shipped' THEN '3' 
                    WHEN 'Completed' THEN '4' 
                    WHEN 'Cancelled' THEN '5' 
                    WHEN 'Returning' THEN '6' 
                    WHEN 'Returned' THEN '7' 
                    WHEN 'Refunded' THEN '8'
                    ELSE '0' 
                END;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "OrderStatus",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Pending")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // --- 4. Fix OrderGroups: Convert String Values to Int ---
            migrationBuilder.Sql(@"
                UPDATE OrderGroups SET PaymentStatus = CASE PaymentStatus
                    WHEN 'Pending' THEN 0 
                    WHEN 'Paid' THEN 1 
                    WHEN 'Failed' THEN 2 
                    WHEN 'Refunded' THEN 3
                    ELSE 0 
                END;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentStatus",
                table: "OrderGroups",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldDefaultValue: "Pending")
                .OldAnnotation("MySql:CharSet", "utf8mb4");


            // --- 5. OrderIssues: Safe Handle (Create if Missing, Migrate if Exists) ---
            migrationBuilder.Sql(@"
                -- Check if table exists
                SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues');

                -- CASE 1: Table MISSING -> Create it (Full Schema with INT enums)
                SET @sql_create = 'CREATE TABLE `OrderIssues` (
                    `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OrderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Type` int NOT NULL,
                    `Status` int NOT NULL,
                    `Reason` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `Description` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `EvidenceUrl` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `IsSystemValid` tinyint(1) NOT NULL,
                    `ShopResponse` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `AdminNote` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    `CreatedBy` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `UpdatedBy` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `IsDeleted` tinyint(1) NOT NULL,
                    CONSTRAINT `PK_OrderIssues` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_OrderIssues_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_OrderIssues_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;';

                -- Exec logic separately
                -- If table missing, create it
                SET @sql_exec = IF(@table_exists = 0, @sql_create, 'SELECT 1');
                PREPARE stmt FROM @sql_exec; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");
            
            // Migrate Data Step 1 (Type) - Only run if table existed BEFORE create step (meaning we might need migration) OR we just ignore and run update safely if types match
            // Actually simpler: Just run UPDATE if table exists. We already created it if it was missing.
            // But wait, the newly created table has INT column, UPDATE ... IN ('String') will fail or do nothing.
            // Old table has VARCHAR column.
            
            // To be safe and avoid multi-statement syntax error in PREPARE: Split into separate blocks.
            
            migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues');
               -- Check if column Type is NOT INT (meaning it's varchar and needs migration)
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues' AND COLUMN_NAME = 'Type' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));
               
               SET @sql_migrate = IF(@table_exists = 1 AND @col_is_string = 1, 
                    'UPDATE OrderIssues SET Type = CASE Type WHEN \'CancelRequest\' THEN 0 WHEN \'ReturnRequest\' THEN 1 WHEN \'WarrantyClaim\' THEN 2 ELSE 0 END WHERE Type IN (\'CancelRequest\', \'ReturnRequest\', \'WarrantyClaim\')', 
                    'SELECT 1');
               PREPARE stmt FROM @sql_migrate; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");

             migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues');
               -- Check if column Status is NOT INT
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues' AND COLUMN_NAME = 'Status' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));

               SET @sql_migrate = IF(@table_exists = 1 AND @col_is_string = 1, 
                    'UPDATE OrderIssues SET Status = CASE Status WHEN \'Pending\' THEN 0 WHEN \'InProgress\' THEN 1 WHEN \'ShopAccepted\' THEN 2 WHEN \'Rejected\' THEN 3 WHEN \'AutoCancelled\' THEN 4 WHEN \'AwaitingReturn\' THEN 5 WHEN \'Returning\' THEN 6 WHEN \'Returned\' THEN 7 WHEN \'Completed\' THEN 8 ELSE 0 END WHERE Status IN (\'Pending\', \'InProgress\')', 
                    'SELECT 1');
               PREPARE stmt FROM @sql_migrate; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues');
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues' AND COLUMN_NAME = 'Type' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));
               
               SET @sql_alter = IF(@table_exists = 1 AND @col_is_string = 1, 'ALTER TABLE `OrderIssues` MODIFY COLUMN `Type` int NOT NULL', 'SELECT 1');
               PREPARE stmt FROM @sql_alter; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");
            
            migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues');
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues' AND COLUMN_NAME = 'Status' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));
               
               SET @sql_alter = IF(@table_exists = 1 AND @col_is_string = 1, 'ALTER TABLE `OrderIssues` MODIFY COLUMN `Status` int NOT NULL', 'SELECT 1');
               PREPARE stmt FROM @sql_alter; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");
            

            // Note: Indices for OrderIssues are handled separately if created manually, but standard Create Table includes PK/FK. 
            // We should ensure Indices exist if we created the table.
             migrationBuilder.Sql(@"
                SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues');
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues' AND INDEX_NAME = 'IX_OrderIssues_OrderId');
                
                SET @sql_index = IF(@table_exists = 1 AND @idx_exists = 0, 'CREATE INDEX `IX_OrderIssues_OrderId` ON `OrderIssues` (`OrderId`)', 'SELECT 1');
                PREPARE stmt FROM @sql_index; EXECUTE stmt; DEALLOCATE PREPARE stmt;

                SET @idx_exists_2 = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssues' AND INDEX_NAME = 'IX_OrderIssues_UserId');
                SET @sql_index_2 = IF(@table_exists = 1 AND @idx_exists_2 = 0, 'CREATE INDEX `IX_OrderIssues_UserId` ON `OrderIssues` (`UserId`)', 'SELECT 1');
                PREPARE stmt FROM @sql_index_2; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");


            // --- 6. OrderIssueLogs: Safe Handle (Create if Missing, Migrate if Exists) ---
            migrationBuilder.Sql(@"
                -- Check if table exists
                SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs');

                -- CASE 1: Table MISSING -> Create it (Full Schema with INT enums)
                SET @sql_create = 'CREATE TABLE `OrderIssueLogs` (
                    `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OrderIssueId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ActionById` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ActionByRole` int NOT NULL,
                    `Action` int NOT NULL,
                    `Comment` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `EvidenceUrl` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `AdminDecision` tinyint(1) NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    `CreatedBy` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `UpdatedBy` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `IsDeleted` tinyint(1) NOT NULL,
                    CONSTRAINT `PK_OrderIssueLogs` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_OrderIssueLogs_OrderIssues_OrderIssueId` FOREIGN KEY (`OrderIssueId`) REFERENCES `OrderIssues` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_OrderIssueLogs_Users_ActionById` FOREIGN KEY (`ActionById`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;';

                -- Exec Create if missing
                SET @sql_exec = IF(@table_exists = 0, @sql_create, 'SELECT 1');
                PREPARE stmt FROM @sql_exec; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");

            // Split migrations block to avoid syntax error
            migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs');
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs' AND COLUMN_NAME = 'ActionByRole' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));
               
               SET @sql_migrate = IF(@table_exists = 1 AND @col_is_string = 1, 
                    'UPDATE OrderIssueLogs SET ActionByRole = CASE ActionByRole WHEN \'Admin\' THEN 0 WHEN \'Customer\' THEN 1 WHEN \'Shop\' THEN 2 ELSE 1 END WHERE ActionByRole IN (\'Admin\',\'Customer\',\'Shop\')', 
                    'SELECT 1');
               PREPARE stmt FROM @sql_migrate; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs');
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs' AND COLUMN_NAME = 'Action' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));
               
               SET @sql_migrate = IF(@table_exists = 1 AND @col_is_string = 1, 
                    'UPDATE OrderIssueLogs SET Action = CASE Action WHEN \'Create\' THEN 0 WHEN \'ShopApprove\' THEN 1 WHEN \'ShopReject\' THEN 2 WHEN \'UserUpdate\' THEN 3 WHEN \'UserEscalate\' THEN 4 WHEN \'AdminDecision\' THEN 5 WHEN \'SystemCancel\' THEN 6 WHEN \'UserCancel\' THEN 7 WHEN \'UserShippedReturn\' THEN 8 WHEN \'ShopReceivedReturn\' THEN 9 ELSE 0 END WHERE Action IN (\'Create\')', 
                    'SELECT 1');
               PREPARE stmt FROM @sql_migrate; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs');
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs' AND COLUMN_NAME = 'ActionByRole' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));

               SET @sql_alter = IF(@table_exists = 1 AND @col_is_string = 1, 'ALTER TABLE `OrderIssueLogs` MODIFY COLUMN `ActionByRole` int NOT NULL', 'SELECT 1');
               PREPARE stmt FROM @sql_alter; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
               SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs');
               SET @col_is_string = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderIssueLogs' AND COLUMN_NAME = 'Action' AND DATA_TYPE IN ('varchar', 'longtext', 'text'));

               SET @sql_alter = IF(@table_exists = 1 AND @col_is_string = 1, 'ALTER TABLE `OrderIssueLogs` MODIFY COLUMN `Action` int NOT NULL', 'SELECT 1');
               PREPARE stmt FROM @sql_alter; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Vouchers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Vouchers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DiscountType",
                table: "Vouchers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Payments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "longtext",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "OrderStatus",
                table: "Orders",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "OrderIssues",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "OrderIssues",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ActionByRole",
                table: "OrderIssueLogs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "OrderIssueLogs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "OrderGroups",
                type: "longtext",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
