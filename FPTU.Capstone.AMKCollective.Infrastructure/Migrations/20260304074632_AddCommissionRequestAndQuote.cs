using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionRequestAndQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TargetedShopId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferenceImages = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionRequests_ShopProfiles_TargetedShopId",
                        column: x => x.TargetedShopId,
                        principalTable: "ShopProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CommissionQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CommissionRequestId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ShopId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    QuotedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedDays = table.Column<int>(type: "int", nullable: false),
                    ShopNotes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionQuotes_CommissionRequests_CommissionRequestId",
                        column: x => x.CommissionRequestId,
                        principalTable: "CommissionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommissionQuotes_ShopProfiles_ShopId",
                        column: x => x.ShopId,
                        principalTable: "ShopProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionQuotes_CommissionRequestId",
                table: "CommissionQuotes",
                column: "CommissionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionQuotes_ShopId",
                table: "CommissionQuotes",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRequests_TargetedShopId",
                table: "CommissionRequests",
                column: "TargetedShopId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRequests_UserId",
                table: "CommissionRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionQuotes");

            migrationBuilder.DropTable(
                name: "CommissionRequests");
        }
    }
}
