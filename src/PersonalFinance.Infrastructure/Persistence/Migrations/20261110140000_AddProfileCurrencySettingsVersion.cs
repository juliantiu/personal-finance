using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalFinance.Infrastructure.Persistence;

#nullable disable

namespace PersonalFinance.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AccountIdentityDbContext))]
[Migration("20261110140000_AddProfileCurrencySettingsVersion")]
public partial class AddProfileCurrencySettingsVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<long>(
            name: "CurrencySettingsVersion",
            table: "BudgetProfiles",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "CurrencySettingsVersion", table: "BudgetProfiles");
}
