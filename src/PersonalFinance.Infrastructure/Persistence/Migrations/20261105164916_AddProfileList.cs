using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PersonalFinance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "BudgetProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MainCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetProfiles_Currencies_MainCurrencyCode",
                        column: x => x.MainCurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code");
                    table.ForeignKey(
                        name: "FK_BudgetProfiles_UserAccounts_OwnerAccountId",
                        column: x => x.OwnerAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProfileAccess",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileAccess", x => new { x.ProfileId, x.AccountId });
                    table.CheckConstraint("CK_ProfileAccess_Role", "[Role] IN ('editor', 'view_only')");
                    table.ForeignKey(
                        name: "FK_ProfileAccess_BudgetProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "BudgetProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileAccess_UserAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Code", "Name", "Symbol" },
                values: new object[,]
                {
                    { "GBP", "Pound sterling", "£" },
                    { "USD", "US dollar", "$" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetProfiles_MainCurrencyCode",
                table: "BudgetProfiles",
                column: "MainCurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetProfiles_OwnerAccountId",
                table: "BudgetProfiles",
                column: "OwnerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileAccess_AccountId",
                table: "ProfileAccess",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileAccess");

            migrationBuilder.DropTable(
                name: "BudgetProfiles");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
