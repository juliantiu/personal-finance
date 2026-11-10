using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Categories_Kind",
                table: "Categories");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Categories",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Categories",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Categories",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Categories_Kind",
                table: "Categories",
                sql: "[Kind] IN ('income', 'savings', 'debt', 'ordinary')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Categories_Kind",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Categories");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Categories",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Categories_Kind",
                table: "Categories",
                sql: "[Kind] IN ('income', 'savings', 'debt')");
        }
    }
}
