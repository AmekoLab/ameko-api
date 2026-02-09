using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FPTU.Capstone.AMKCollective.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalFieldsToAssembledProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SoundUrl",
                table: "ProductAssembledDetails",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Battery",
                table: "AssembledProducts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Connection",
                table: "AssembledProducts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AssembledProducts",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Image1",
                table: "AssembledProducts",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Image2",
                table: "AssembledProducts",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Image3",
                table: "AssembledProducts",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Layout",
                table: "AssembledProducts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Mounting",
                table: "AssembledProducts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PCB",
                table: "AssembledProducts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "AssembledProducts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoundUrl",
                table: "ProductAssembledDetails");

            migrationBuilder.DropColumn(
                name: "Battery",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Connection",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Image1",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Image2",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Image3",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Layout",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Mounting",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "PCB",
                table: "AssembledProducts");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "AssembledProducts");
        }
    }
}
