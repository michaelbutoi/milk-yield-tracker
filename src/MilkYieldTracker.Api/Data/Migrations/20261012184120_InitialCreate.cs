using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilkYieldTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TagNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MilkingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CowId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Litres = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkingSessions_Cows_CowId",
                        column: x => x.CowId,
                        principalTable: "Cows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cows_TagNumber",
                table: "Cows",
                column: "TagNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MilkingSessions_CowId_Date_Period",
                table: "MilkingSessions",
                columns: new[] { "CowId", "Date", "Period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilkingSessions");

            migrationBuilder.DropTable(
                name: "Cows");
        }
    }
}
