using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GekkoLab.Migrations
{
    /// <inheritdoc />
    public partial class AddGekkoDetections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GekkoDetections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GekkoDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<float>(type: "REAL", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BoundingBoxX = table.Column<int>(type: "INTEGER", nullable: true),
                    BoundingBoxY = table.Column<int>(type: "INTEGER", nullable: true),
                    BoundingBoxWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    BoundingBoxHeight = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GekkoDetections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GekkoDetections_GekkoDetected",
                table: "GekkoDetections",
                column: "GekkoDetected");

            migrationBuilder.CreateIndex(
                name: "IX_GekkoDetections_Timestamp",
                table: "GekkoDetections",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GekkoDetections");
        }
    }
}
