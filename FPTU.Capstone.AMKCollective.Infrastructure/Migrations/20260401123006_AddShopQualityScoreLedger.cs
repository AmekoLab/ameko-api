using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopQualityScoreLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Badge",
                table: "ShopProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentQualityScore",
                table: "ShopProfiles",
                type: "int",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.CreateTable(
                name: "QualityScoreSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ShopId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CapturedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IssueRate = table.Column<double>(type: "double", nullable: false),
                    AvgResponseHours = table.Column<double>(type: "double", nullable: false),
                    RefundRate = table.Column<double>(type: "double", nullable: false),
                    RepurchaseRate = table.Column<double>(type: "double", nullable: false),
                    PositiveFeedbackRate = table.Column<double>(type: "double", nullable: false),
                    AutoCancelRate = table.Column<double>(type: "double", nullable: false),
                    FeedbackCount = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<int>(type: "int", nullable: false),
                    Badge = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityScoreSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityScoreSnapshots_ShopProfiles_ShopId",
                        column: x => x.ShopId,
                        principalTable: "ShopProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_QualityScoreSnapshots_ShopId",
                table: "QualityScoreSnapshots",
                column: "ShopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualityScoreSnapshots");

            migrationBuilder.DropColumn(
                name: "Badge",
                table: "ShopProfiles");

            migrationBuilder.DropColumn(
                name: "CurrentQualityScore",
                table: "ShopProfiles");
        }
    }
}
