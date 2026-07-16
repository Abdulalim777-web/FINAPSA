using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FINAPSA.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdmissionNumber",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdmissionNumber",
                table: "Students");
        }
    }
}
