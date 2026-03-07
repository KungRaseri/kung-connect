using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KungConnect.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineSystemInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemInfoJson",
                table: "Machines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemInfoJson",
                table: "Machines");
        }
    }
}
