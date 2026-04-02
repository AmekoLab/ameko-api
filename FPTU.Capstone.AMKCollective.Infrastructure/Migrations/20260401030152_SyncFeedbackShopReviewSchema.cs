using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncFeedbackShopReviewSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Users_ToUserId",
                table: "Feedbacks");

            migrationBuilder.RenameColumn(
                name: "ToUserId",
                table: "Feedbacks",
                newName: "ShopId");

            migrationBuilder.RenameIndex(
                name: "IX_Feedbacks_ToUserId",
                table: "Feedbacks",
                newName: "IX_Feedbacks_ShopId");

            migrationBuilder.AddColumn<int>(
                name: "TotalReviews",
                table: "ShopProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Feedbacks",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShopRepliedAt",
                table: "Feedbacks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopReply",
                table: "Feedbacks",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FeedbackImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FeedbackId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackImages_Feedbacks_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "Feedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Feedbacks_Rating_Range",
                table: "Feedbacks",
                sql: "`Rating` >= 1 AND `Rating` <= 5");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackImages_FeedbackId",
                table: "FeedbackImages",
                column: "FeedbackId");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_ShopProfiles_ShopId",
                table: "Feedbacks",
                column: "ShopId",
                principalTable: "ShopProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_ShopProfiles_ShopId",
                table: "Feedbacks");

            migrationBuilder.DropTable(
                name: "FeedbackImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Feedbacks_Rating_Range",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "TotalReviews",
                table: "ShopProfiles");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ShopRepliedAt",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ShopReply",
                table: "Feedbacks");

            migrationBuilder.RenameColumn(
                name: "ShopId",
                table: "Feedbacks",
                newName: "ToUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Feedbacks_ShopId",
                table: "Feedbacks",
                newName: "IX_Feedbacks_ToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Users_ToUserId",
                table: "Feedbacks",
                column: "ToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
