using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "PaymentMethods",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
            migrationBuilder.Sql("UPDATE [PaymentMethods] SET [Nickname] = N'Cash' WHERE [Kind] = 'cash';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "PaymentMethods");
        }
    }
}
