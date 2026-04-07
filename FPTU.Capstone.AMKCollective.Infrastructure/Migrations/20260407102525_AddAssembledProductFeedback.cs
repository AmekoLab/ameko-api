using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssembledProductFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "FeedbackId",
                table: "FeedbackImages",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "AssembledProductFeedbackId",
                table: "FeedbackImages",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "AssembledProducts",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TotalReviews",
                table: "AssembledProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AssembledProductFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrderItemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AssembledProductId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ShopId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EditCount = table.Column<int>(type: "int", nullable: false),
                    ShopReply = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShopRepliedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssembledProductFeedbacks", x => x.Id);
                    table.CheckConstraint("CK_AssembledProductFeedbacks_Rating_Range", "`Rating` >= 1 AND `Rating` <= 5");
                    table.ForeignKey(
                        name: "FK_AssembledProductFeedbacks_AssembledProducts_AssembledProduct~",
                        column: x => x.AssembledProductId,
                        principalTable: "AssembledProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssembledProductFeedbacks_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssembledProductFeedbacks_ShopProfiles_ShopId",
                        column: x => x.ShopId,
                        principalTable: "ShopProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssembledProductFeedbacks_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackImages_AssembledProductFeedbackId",
                table: "FeedbackImages",
                column: "AssembledProductFeedbackId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeedbackImages_ExclusiveArc",
                table: "FeedbackImages",
                sql: "(`FeedbackId` IS NOT NULL AND `AssembledProductFeedbackId` IS NULL) OR (`FeedbackId` IS NULL AND `AssembledProductFeedbackId` IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledProductFeedbacks_AssembledProductId",
                table: "AssembledProductFeedbacks",
                column: "AssembledProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledProductFeedbacks_FromUserId",
                table: "AssembledProductFeedbacks",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledProductFeedbacks_OrderItemId",
                table: "AssembledProductFeedbacks",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssembledProductFeedbacks_ShopId",
                table: "AssembledProductFeedbacks",
                column: "ShopId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackImages_AssembledProductFeedbacks_AssembledProductFee~",
                table: "FeedbackImages",
                column: "AssembledProductFeedbackId",
                principalTable: "AssembledProductFeedbacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackImages_AssembledProductFeedbacks_AssembledProductFee~",
                table: "FeedbackImages");

            migrationBuilder.DropTable(
                name: "AssembledProductFeedbacks");

            migrationBuilder.DropIndex(
                name: "IX_FeedbackImages_AssembledProductFeedbackId",
                table: "FeedbackImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeedbackImages_ExclusiveArc",
                table: "FeedbackImages");

            migrationBuilder.DropColumn(
                name: "AssembledProductFeedbackId",
                table: "FeedbackImages");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "TotalReviews",
                table: "AssembledProducts");

            migrationBuilder.AlterColumn<Guid>(
                name: "FeedbackId",
                table: "FeedbackImages",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }
    }
}
