using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAddonEligibleToModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAddonEligible",
                table: "Models",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Models_ShopId_IsAddonEligible_IsActive",
                table: "Models",
                columns: new[] { "ShopId", "IsAddonEligible", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Models_ShopId_IsAddonEligible_IsActive",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "IsAddonEligible",
                table: "Models");
        }
    }
}
