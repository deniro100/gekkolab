using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GekkoLab.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexGekkoDetections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GekkoDetections_Timestamp_GekkoDetected",
                table: "GekkoDetections",
                columns: new[] { "Timestamp", "GekkoDetected" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GekkoDetections_Timestamp_GekkoDetected",
                table: "GekkoDetections");
        }
    }
}
