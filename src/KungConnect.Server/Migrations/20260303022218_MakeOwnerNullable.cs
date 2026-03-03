using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KungConnect.Server.Migrations
{
    /// <inheritdoc />
    public partial class MakeOwnerNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Machines_Users_OwnerId",
                table: "Machines");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Machines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_Users_OwnerId",
                table: "Machines",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Machines_Users_OwnerId",
                table: "Machines");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Machines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_Users_OwnerId",
                table: "Machines",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
