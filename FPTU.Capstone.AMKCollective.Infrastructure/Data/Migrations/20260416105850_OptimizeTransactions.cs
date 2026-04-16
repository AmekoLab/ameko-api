using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropForeignKey(
            //     name: "FK_Transactions_OrderGroups_OrderGroupId",
            //     table: "Transactions");

            // migrationBuilder.DropForeignKey(
            //     name: "FK_Transactions_Orders_RelatedOrderId",
            //     table: "Transactions");

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "FeeAmount",
            //     table: "Transactions",
            //     type: "decimal(18,2)",
            //     precision: 18,
            //     scale: 2,
            //     nullable: false,
            //     defaultValue: 0m,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(18,2)");

            // migrationBuilder.AddColumn<decimal>(
            //     name: "HeldBalanceAfterTransaction",
            //     table: "Transactions",
            //     type: "decimal(18,2)",
            //     precision: 18,
            //     scale: 2,
            //     nullable: false,
            //     defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transaction_FeeAmount_Positive",
                table: "Transactions",
                sql: "`FeeAmount` >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transaction_MutualExclusive_OrderRef",
                table: "Transactions",
                sql: "`OrderGroupId` IS NULL OR `RelatedOrderId` IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_OrderGroups_OrderGroupId",
                table: "Transactions",
                column: "OrderGroupId",
                principalTable: "OrderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Orders_RelatedOrderId",
                table: "Transactions",
                column: "RelatedOrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_OrderGroups_OrderGroupId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Orders_RelatedOrderId",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transaction_FeeAmount_Positive",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transaction_MutualExclusive_OrderRef",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "HeldBalanceAfterTransaction",
                table: "Transactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAmount",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_OrderGroups_OrderGroupId",
                table: "Transactions",
                column: "OrderGroupId",
                principalTable: "OrderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Orders_RelatedOrderId",
                table: "Transactions",
                column: "RelatedOrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
