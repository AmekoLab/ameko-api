using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefineShopCategoryWalletPaymentAndKitSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropIndex(
            //     name: "IX_KitDesignOptions_BaseKitId_ComponentId",
            //     table: "KitDesignOptions");

            // migrationBuilder.DropIndex(
            //     name: "IX_Categories_Slug",
            //     table: "Categories");

            // migrationBuilder.AddColumn<Guid>(
            //    name: "WalletId",
            //    table: "Payments",
            //    type: "char(36)",
            //    nullable: true,
            //    collation: "ascii_general_ci");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShopId",
                table: "Categories",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            // Fix for existing data: Set ShopId to NULL to convert existing categories to Global categories
            // This prevents FK violation when adding the constraint
            migrationBuilder.Sql("UPDATE Categories SET ShopId = NULL");

            /*
            migrationBuilder.CreateIndex(
                name: "IX_Payments_WalletId",
                table: "Payments",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_KitDesignOptions_BaseKitId_ComponentId",
                table: "KitDesignOptions",
                columns: new[] { "BaseKitId", "ComponentId" });

            migrationBuilder.CreateIndex(
                name: "IX_KitDesignOptions_BaseKitId_StepName_Tags",
                table: "KitDesignOptions",
                columns: new[] { "BaseKitId", "StepName", "Tags" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ShopId",
                table: "Categories",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ShopId_IsActive",
                table: "Categories",
                columns: new[] { "ShopId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug_ShopId",
                table: "Categories",
                columns: new[] { "Slug", "ShopId" },
                unique: true);
            */

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_ShopProfiles_ShopId",
                table: "Categories",
                column: "ShopId",
                principalTable: "ShopProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Wallets_WalletId",
                table: "Payments",
                column: "WalletId",
                principalTable: "Wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_ShopProfiles_ShopId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Wallets_WalletId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_WalletId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_KitDesignOptions_BaseKitId_ComponentId",
                table: "KitDesignOptions");

            migrationBuilder.DropIndex(
                name: "IX_KitDesignOptions_BaseKitId_StepName_Tags",
                table: "KitDesignOptions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ShopId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ShopId_IsActive",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug_ShopId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "Payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShopId",
                table: "Categories",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_KitDesignOptions_BaseKitId_ComponentId",
                table: "KitDesignOptions",
                columns: new[] { "BaseKitId", "ComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);
        }
    }
}
