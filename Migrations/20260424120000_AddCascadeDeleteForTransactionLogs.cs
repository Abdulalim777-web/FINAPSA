using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FINAPSA.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeDeleteForTransactionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionLogs_Payments_PaymentId",
                table: "TransactionLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionLogs_Payments_PaymentId",
                table: "TransactionLogs",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionLogs_Payments_PaymentId",
                table: "TransactionLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionLogs_Payments_PaymentId",
                table: "TransactionLogs",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id");
        }
    }
}
