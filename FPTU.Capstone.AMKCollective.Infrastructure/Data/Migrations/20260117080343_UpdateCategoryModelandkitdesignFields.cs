using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCategoryModelandkitdesignFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KitDesignOptions_Models_BaseKitId", 
                table: "KitDesignOptions");
            migrationBuilder.DropIndex(
                name: "IX_KitDesignOptions_BaseKitId",
                table: "KitDesignOptions");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ShopProfiles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShopName",
                table: "ShopProfiles",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OrderStatus",
                table: "Orders",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ShippingMethod",
                table: "Orders",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "Orders",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Models",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Models",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Models",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DefaultLayerImageUrl",
                table: "Models",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Models",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Models",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Specifications",
                table: "Models",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDefault",
                table: "KitDesignOptions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AddColumn<int>(
                name: "StepOrder",
                table: "KitDesignOptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Categories",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Categories",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDelete",
                table: "Categories",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Categories",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailURL",
                table: "Categories",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Models_PartType_IsActive",
                table: "Models",
                columns: new[] { "PartType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Models_Slug",
                table: "Models",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitDesignOptions_BaseKitId_ComponentId",
                table: "KitDesignOptions",
                columns: new[] { "BaseKitId", "ComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitDesignOptions_BaseKitId_StepOrder",
                table: "KitDesignOptions",
                columns: new[] { "BaseKitId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive",
                table: "Categories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsDelete",
                table: "Categories",
                column: "IsDelete");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);
            migrationBuilder.AddForeignKey(
                name: "FK_KitDesignOptions_Models_BaseKitId",
                table: "KitDesignOptions",
                column: "BaseKitId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Models_PartType_IsActive",
                table: "Models");

            migrationBuilder.DropIndex(
                name: "IX_Models_Slug",
                table: "Models");

            migrationBuilder.DropIndex(
                name: "IX_KitDesignOptions_BaseKitId_ComponentId",
                table: "KitDesignOptions");

            migrationBuilder.DropIndex(
                name: "IX_KitDesignOptions_BaseKitId_StepOrder",
                table: "KitDesignOptions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_IsActive",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_IsDelete",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ShopProfiles");

            migrationBuilder.DropColumn(
                name: "ShopName",
                table: "ShopProfiles");

            migrationBuilder.DropColumn(
                name: "OrderStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DefaultLayerImageUrl",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "Specifications",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "StepOrder",
                table: "KitDesignOptions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsDelete",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ThumbnailURL",
                table: "Categories");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Models",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Models",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Models",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDefault",
                table: "KitDesignOptions",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Categories",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_KitDesignOptions_BaseKitId",
                table: "KitDesignOptions",
                column: "BaseKitId");
            

        }
    }
}
