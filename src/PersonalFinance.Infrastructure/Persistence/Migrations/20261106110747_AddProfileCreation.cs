using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_Kind", "[Kind] IN ('income', 'savings', 'debt')");
                    table.ForeignKey(
                        name: "FK_Categories_BudgetProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "BudgetProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.CheckConstraint("CK_PaymentMethods_Kind", "[Kind] = 'cash'");
                    table.ForeignKey(
                        name: "FK_PaymentMethods_BudgetProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "BudgetProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileCurrencies",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileCurrencies", x => new { x.ProfileId, x.CurrencyCode });
                    table.ForeignKey(
                        name: "FK_ProfileCurrencies_BudgetProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "BudgetProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileCurrencies_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SystemRole = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                    table.CheckConstraint("CK_Tags_SystemRole", "[SystemRole] IS NULL OR [SystemRole] IN ('iou', 'iou_repayment')");
                    table.ForeignKey(
                        name: "FK_Tags_BudgetProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "BudgetProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ProfileId_Kind",
                table: "Categories",
                columns: new[] { "ProfileId", "Kind" },
                unique: true,
                filter: "[Kind] IN ('income', 'savings', 'debt')");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_ProfileId_Kind",
                table: "PaymentMethods",
                columns: new[] { "ProfileId", "Kind" },
                unique: true,
                filter: "[Kind] = 'cash'");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCurrencies_CurrencyCode",
                table: "ProfileCurrencies",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_ProfileId_SystemRole",
                table: "Tags",
                columns: new[] { "ProfileId", "SystemRole" },
                unique: true,
                filter: "[SystemRole] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "ProfileCurrencies");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
