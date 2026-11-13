using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptedCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentMethods_Kind",
                table: "PaymentMethods");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "PaymentMethods",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4)",
                oldMaxLength: 4);

            migrationBuilder.AddColumn<string>(
                name: "LastFour",
                table: "PaymentMethods",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchingKeyId",
                table: "PaymentMethods",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "NumberFingerprint",
                table: "PaymentMethods",
                type: "varbinary(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedNumber",
                table: "PaymentMethods",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAt",
                table: "PaymentMethods",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "PaymentMethods",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_ProfileId_NumberFingerprint",
                table: "PaymentMethods",
                columns: new[] { "ProfileId", "NumberFingerprint" },
                unique: true,
                filter: "[Kind] = 'credit_card'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentMethods_CardShape",
                table: "PaymentMethods",
                sql: "([Kind] = 'cash' AND [ProtectedNumber] IS NULL AND [NumberFingerprint] IS NULL AND [MatchingKeyId] IS NULL AND [LastFour] IS NULL AND [RetiredAt] IS NULL) OR ([Kind] = 'credit_card' AND [ProtectedNumber] IS NOT NULL AND DATALENGTH([ProtectedNumber]) > 0 AND [NumberFingerprint] IS NOT NULL AND DATALENGTH([NumberFingerprint]) = 32 AND [MatchingKeyId] IS NOT NULL AND LEN([MatchingKeyId]) = 64 AND [LastFour] IS NOT NULL AND DATALENGTH([LastFour]) = 8 AND [LastFour] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentMethods_Kind",
                table: "PaymentMethods",
                sql: "[Kind] IN ('cash', 'credit_card')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [PaymentMethods] WHERE [Kind] = 'credit_card') THROW 51000, 'Cannot downgrade while saved cards exist. Restore a compatible application or follow the reviewed recovery procedure.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_ProfileId_NumberFingerprint",
                table: "PaymentMethods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentMethods_CardShape",
                table: "PaymentMethods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentMethods_Kind",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "LastFour",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "MatchingKeyId",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "NumberFingerprint",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "ProtectedNumber",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PaymentMethods");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "PaymentMethods",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(11)",
                oldMaxLength: 11);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentMethods_Kind",
                table: "PaymentMethods",
                sql: "[Kind] = 'cash'");
        }
    }
}
