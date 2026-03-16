using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchemaVoucherTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FK_Payments_OrderGroups_OrderGroupId safely
            migrationBuilder.Sql(@"
                SET @fkName1 := (SELECT CONSTRAINT_NAME 
                                FROM information_schema.KEY_COLUMN_USAGE 
                                WHERE TABLE_SCHEMA = DATABASE()
                                AND TABLE_NAME = 'Payments' 
                                AND COLUMN_NAME = 'OrderGroupId' 
                                AND REFERENCED_TABLE_NAME = 'OrderGroups' 
                                LIMIT 1);
                SET @sql1 := IF(@fkName1 IS NOT NULL, CONCAT('ALTER TABLE Payments DROP FOREIGN KEY ', @fkName1), 'DO 0');
                PREPARE stmt1 FROM @sql1;
                EXECUTE stmt1;
                DEALLOCATE PREPARE stmt1;
            ");

            // Drop FK_Payments_Orders_RelatedOrderId safely
            migrationBuilder.Sql(@"
                SET @fkName2 := (SELECT CONSTRAINT_NAME 
                                FROM information_schema.KEY_COLUMN_USAGE 
                                WHERE TABLE_SCHEMA = DATABASE()
                                AND TABLE_NAME = 'Payments' 
                                AND COLUMN_NAME = 'RelatedOrderId' 
                                AND REFERENCED_TABLE_NAME = 'Orders' 
                                LIMIT 1);
                SET @sql2 := IF(@fkName2 IS NOT NULL, CONCAT('ALTER TABLE Payments DROP FOREIGN KEY ', @fkName2), 'DO 0');
                PREPARE stmt2 FROM @sql2;
                EXECUTE stmt2;
                DEALLOCATE PREPARE stmt2;
            ");

            // Drop FK_Payments_Wallets_WalletId safely
            migrationBuilder.Sql(@"
                SET @fkName3 := (SELECT CONSTRAINT_NAME 
                                FROM information_schema.KEY_COLUMN_USAGE 
                                WHERE TABLE_SCHEMA = DATABASE()
                                AND TABLE_NAME = 'Payments' 
                                AND COLUMN_NAME = 'WalletId' 
                                AND REFERENCED_TABLE_NAME = 'Wallets' 
                                LIMIT 1);
                SET @sql3 := IF(@fkName3 IS NOT NULL, CONCAT('ALTER TABLE Payments DROP FOREIGN KEY ', @fkName3), 'DO 0');
                PREPARE stmt3 FROM @sql3;
                EXECUTE stmt3;
                DEALLOCATE PREPARE stmt3;
            ");

            // Added columns first to enable data migration
            // Safe Add Column ApplyOrder
            migrationBuilder.Sql(@"
                SET @colName1 := (SELECT COLUMN_NAME 
                                FROM information_schema.COLUMNS 
                                WHERE TABLE_SCHEMA = DATABASE() 
                                AND TABLE_NAME = 'VoucherUsageLogs' 
                                AND COLUMN_NAME = 'ApplyOrder' 
                                LIMIT 1);
                SET @sqlCol1 := IF(@colName1 IS NULL, 'ALTER TABLE VoucherUsageLogs ADD ApplyOrder int NOT NULL DEFAULT 0', 'DO 0');
                PREPARE stmtCol1 FROM @sqlCol1;
                EXECUTE stmtCol1;
                DEALLOCATE PREPARE stmtCol1;
            ");

            // Safe Add Column VoucherType
            migrationBuilder.Sql(@"
                SET @colName2 := (SELECT COLUMN_NAME 
                                FROM information_schema.COLUMNS 
                                WHERE TABLE_SCHEMA = DATABASE() 
                                AND TABLE_NAME = 'VoucherUsageLogs' 
                                AND COLUMN_NAME = 'VoucherType' 
                                LIMIT 1);
                SET @sqlCol2 := IF(@colName2 IS NULL, 'ALTER TABLE VoucherUsageLogs ADD VoucherType int NOT NULL DEFAULT 0', 'DO 0');
                PREPARE stmtCol2 FROM @sqlCol2;
                EXECUTE stmtCol2;
                DEALLOCATE PREPARE stmtCol2;
            ");

            // Data Migration SQL - OrderVouchers to VoucherUsageLog (Safely)
            migrationBuilder.Sql(@"
                SET @tblExists := (SELECT TABLE_NAME 
                                FROM information_schema.TABLES 
                                WHERE TABLE_SCHEMA = DATABASE() 
                                AND TABLE_NAME = 'OrderVouchers' 
                                LIMIT 1);
                
                SET @sqlDataMigrate := IF(@tblExists IS NOT NULL, 
                    'INSERT INTO VoucherUsageLogs (Id, OrderId, VoucherId, UserId, Code, DiscountApplied, VoucherType, ApplyOrder, CreatedAt, IsDeleted) SELECT UUID(), ov.OrderId, ov.VoucherId, o.CustomerId, v.Code, 0, v.Type, 0, NOW(), 0 FROM OrderVouchers ov JOIN Orders o ON ov.OrderId = o.Id JOIN Vouchers v ON ov.VoucherId = v.Id', 
                    'DO 0');

                PREPARE stmtData FROM @sqlDataMigrate;
                EXECUTE stmtData;
                DEALLOCATE PREPARE stmtData;
            ");

            migrationBuilder.Sql("DROP TABLE IF EXISTS OrderVouchers");

            // Safe drop index
            migrationBuilder.Sql(@"
                SET @idxName1 := (SELECT INDEX_NAME 
                                FROM information_schema.STATISTICS 
                                WHERE TABLE_SCHEMA = DATABASE() 
                                AND TABLE_NAME = 'Payments' 
                                AND INDEX_NAME = 'IX_Payments_RelatedOrderId' 
                                LIMIT 1);
                SET @sqlIdx1 := IF(@idxName1 IS NOT NULL, 'DROP INDEX IX_Payments_RelatedOrderId ON Payments', 'DO 0');
                PREPARE stmtIdx1 FROM @sqlIdx1;
                EXECUTE stmtIdx1;
                DEALLOCATE PREPARE stmtIdx1;
            ");

            migrationBuilder.Sql(@"
                SET @idxName2 := (SELECT INDEX_NAME 
                                FROM information_schema.STATISTICS 
                                WHERE TABLE_SCHEMA = DATABASE() 
                                AND TABLE_NAME = 'Payments' 
                                AND INDEX_NAME = 'IX_Payments_WalletId' 
                                LIMIT 1);
                SET @sqlIdx2 := IF(@idxName2 IS NOT NULL, 'DROP INDEX IX_Payments_WalletId ON Payments', 'DO 0');
                PREPARE stmtIdx2 FROM @sqlIdx2;
                EXECUTE stmtIdx2;
                DEALLOCATE PREPARE stmtIdx2;
            ");

            // Safe Drop Column RelatedOrderId
            migrationBuilder.Sql(@"
                SET @colName3 := (SELECT COLUMN_NAME 
                                FROM information_schema.COLUMNS 
                                WHERE TABLE_SCHEMA = DATABASE() 
                                AND TABLE_NAME = 'Payments' 
                                AND COLUMN_NAME = 'RelatedOrderId' 
                                LIMIT 1);
                SET @sqlCol3 := IF(@colName3 IS NOT NULL, 'ALTER TABLE Payments DROP COLUMN RelatedOrderId', 'DO 0');
                PREPARE stmtCol3 FROM @sqlCol3;
                EXECUTE stmtCol3;
                DEALLOCATE PREPARE stmtCol3;
            ");

            // Safe Drop Column WalletId
            migrationBuilder.Sql(@"
                SET @colName4 := (SELECT COLUMN_NAME 
                                FROM information_schema.COLUMNS 
                                WHERE TABLE_SCHEMA = DATABASE() 
                                AND TABLE_NAME = 'Payments' 
                                AND COLUMN_NAME = 'WalletId' 
                                LIMIT 1);
                SET @sqlCol4 := IF(@colName4 IS NOT NULL, 'ALTER TABLE Payments DROP COLUMN WalletId', 'DO 0');
                PREPARE stmtCol4 FROM @sqlCol4;
                EXECUTE stmtCol4;
                DEALLOCATE PREPARE stmtCol4;
            ");

            // Insert System Bot User first
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Banner", "ConsecutiveSuccesses", "CreatedAt", "CreatedBy", "DateOfBirth", "Email", "EmailConfirmed", "FirstName", "Gender", "HashedPassword", "Image", "IsDeleted", "LastName", "PhoneNumber", "PhoneNumberConfirmed", "ResetPasswordToken", "RoleId", "Status", "StoreAddress", "StoreDescription", "SuccessDeliveryRate", "TotalAutoCancels", "UpdatedAt", "UpdatedBy", "Username", "VerificationCode", "VerificationCodeExpiryTime", "YMonthlyAutoCancels" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), null, 0, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "noreply@amkcollective.com", true, "AMK", null, "NoPasswordNeededForBot", null, false, "System", null, false, null, new Guid("11111111-1111-1111-1111-111111111111"), 0, null, null, null, 0, null, null, "systembot", null, null, 0 });

            // Backfill NULL CreatorId to avoid errors when converting to NOT NULL
            migrationBuilder.Sql("UPDATE Vouchers SET CreatorId = '00000000-0000-0000-0000-000000000001' WHERE CreatorId IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatorId",
                table: "Vouchers",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "MaxUsesPerUser",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                table: "Vouchers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WalletId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrderGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RelatedOrderId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false, defaultValue: "VND")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_OrderGroups_OrderGroupId",
                        column: x => x.OrderGroupId,
                        principalTable: "OrderGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Orders_RelatedOrderId",
                        column: x => x.RelatedOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageLogs_OrderId",
                table: "VoucherUsageLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ShopId",
                table: "Vouchers",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OrderGroupId",
                table: "Transactions",
                column: "OrderGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RelatedOrderId",
                table: "Transactions",
                column: "RelatedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_WalletId",
                table: "Transactions",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_OrderGroups_OrderGroupId",
                table: "Payments",
                column: "OrderGroupId",
                principalTable: "OrderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_ShopProfiles_ShopId",
                table: "Vouchers",
                column: "ShopId",
                principalTable: "ShopProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VoucherUsageLogs_Orders_OrderId",
                table: "VoucherUsageLogs",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_OrderGroups_OrderGroupId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_ShopProfiles_ShopId",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherUsageLogs_Orders_OrderId",
                table: "VoucherUsageLogs");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_VoucherUsageLogs_OrderId",
                table: "VoucherUsageLogs");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_ShopId",
                table: "Vouchers");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DropColumn(
                name: "ApplyOrder",
                table: "VoucherUsageLogs");

            migrationBuilder.DropColumn(
                name: "VoucherType",
                table: "VoucherUsageLogs");

            migrationBuilder.DropColumn(
                name: "MaxUsesPerUser",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "ShopId",
                table: "Vouchers");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatorId",
                table: "Vouchers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedOrderId",
                table: "Payments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId",
                table: "Payments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "OrderVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VoucherId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplyOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DiscountApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    VoucherCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VoucherType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderVouchers_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderVouchers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RelatedOrderId",
                table: "Payments",
                column: "RelatedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_WalletId",
                table: "Payments",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderVouchers_OrderId_VoucherId",
                table: "OrderVouchers",
                columns: new[] { "OrderId", "VoucherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderVouchers_VoucherId",
                table: "OrderVouchers",
                column: "VoucherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_OrderGroups_OrderGroupId",
                table: "Payments",
                column: "OrderGroupId",
                principalTable: "OrderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Orders_RelatedOrderId",
                table: "Payments",
                column: "RelatedOrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Wallets_WalletId",
                table: "Payments",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
