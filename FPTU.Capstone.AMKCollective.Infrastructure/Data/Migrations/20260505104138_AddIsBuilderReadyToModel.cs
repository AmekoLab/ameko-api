using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBuilderReadyToModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Models_PartType_IsActive",
                table: "Models");

            migrationBuilder.AddColumn<bool>(
                name: "IsBuilderReady",
                table: "Models",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Models_PartType_IsActive_IsBuilderReady",
                table: "Models",
                columns: new[] { "PartType", "IsActive", "IsBuilderReady" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Models_PartType_IsActive_IsBuilderReady",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "IsBuilderReady",
                table: "Models");

            migrationBuilder.CreateIndex(
                name: "IX_Models_PartType_IsActive",
                table: "Models",
                columns: new[] { "PartType", "IsActive" });
        }
    }
}
