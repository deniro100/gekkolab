using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GekkoLab.Migrations
{
    /// <inheritdoc />
    public partial class AddGekkoSightings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GekkoSightings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Confidence = table.Column<float>(type: "REAL", nullable: false),
                    PositionX = table.Column<int>(type: "INTEGER", nullable: true),
                    PositionY = table.Column<int>(type: "INTEGER", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GekkoSightings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GekkoSightings_Timestamp",
                table: "GekkoSightings",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GekkoSightings");
        }
    }
}
