using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingColumnsFromBadSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Helper to add 'Vouchers' columns safely
            var addColumnSql = @"
SET @dbname = DATABASE();
SET @tablename = '{0}';
SET @columnname = '{1}';
SET @typedef = '{2}';
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (table_name = @tablename)
      AND (table_schema = @dbname)
      AND (column_name = @columnname)
  ) > 0,
  'SELECT 1',
  CONCAT('ALTER TABLE ', @tablename, ' ADD ', @columnname, ' ', @typedef, ';')
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;
";

            // Vouchers
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "Name", "VARCHAR(255) NOT NULL DEFAULT ''''"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "Description", "VARCHAR(1000) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "Value", "DECIMAL(18,2) NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "MinOrderValue", "DECIMAL(18,2) NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "MaxDiscountAmount", "DECIMAL(18,2) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "StartDate", "DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "EndDate", "DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "UsageLimit", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "UsedCount", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "Status", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "Type", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "DiscountType", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "CreatorId", "CHAR(36) NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000''"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "TargetUserId", "CHAR(36) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Vouchers", "Code", "VARCHAR(50) NOT NULL DEFAULT ''TEMP''"));

            // Payments
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "FeeAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "RelatedOrderId", "CHAR(36) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "OrderGroupId", "CHAR(36) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "Description", "LONGTEXT NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "FailureMessage", "LONGTEXT NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "BillingAddress", "VARCHAR(500) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "PayerEmail", "VARCHAR(255) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "StripePaymentIntentId", "VARCHAR(255) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "StripeSessionId", "VARCHAR(255) NULL"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "Method", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "Status", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "Type", "INT NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(string.Format(addColumnSql, "Payments", "Currency", "VARCHAR(3) NOT NULL DEFAULT ''VND''"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
