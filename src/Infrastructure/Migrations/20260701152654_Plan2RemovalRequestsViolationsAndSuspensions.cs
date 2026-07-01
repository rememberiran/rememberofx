using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class Plan2RemovalRequestsViolationsAndSuspensions : Migration
{
    private static readonly string[] RemovalApprovalCompositeIndex = ["RequestId", "ApprovedByUserId"];
    private static readonly string[] RemovalRequestCompositeIndex = ["FolderId", "TweetId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TrustedContributors");

        migrationBuilder.AddColumn<DateTime>(
            name: "SuspendedAt",
            table: "Users",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SuspendedByUserId",
            table: "Users",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SuspendedReason",
            table: "Users",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuditLog_Action",
            table: "AuditLog",
            column: "Action");

        migrationBuilder.CreateTable(
            name: "RemovalRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TweetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RequestedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "pending"),
                ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RemovalRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_RemovalRequests_Folders_FolderId",
                    column: x => x.FolderId,
                    principalTable: "Folders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RemovalRequests_Tweets_TweetId",
                    column: x => x.TweetId,
                    principalTable: "Tweets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RemovalRequests_Users_RequestedByUserId",
                    column: x => x.RequestedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ViolationReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReportedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReportedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "pending"),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ViolationReports", x => x.Id);
                table.ForeignKey(
                    name: "FK_ViolationReports_Users_ReportedByUserId",
                    column: x => x.ReportedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ViolationReports_Users_ReportedUserId",
                    column: x => x.ReportedUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ViolationReports_Users_ReviewedByUserId",
                    column: x => x.ReviewedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RemovalApprovals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsVoid = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RemovalApprovals", x => x.Id);
                table.ForeignKey(
                    name: "FK_RemovalApprovals_RemovalRequests_RequestId",
                    column: x => x.RequestId,
                    principalTable: "RemovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RemovalApprovals_Users_ApprovedByUserId",
                    column: x => x.ApprovedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_SuspendedByUserId",
            table: "Users",
            column: "SuspendedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_RemovalApprovals_ApprovedByUserId",
            table: "RemovalApprovals",
            column: "ApprovedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_RemovalApprovals_RequestId",
            table: "RemovalApprovals",
            column: "RequestId");

        migrationBuilder.CreateIndex(
            name: "IX_RemovalApprovals_RequestId_ApprovedByUserId",
            table: "RemovalApprovals",
            columns: RemovalApprovalCompositeIndex,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RemovalRequests_FolderId_TweetId",
            table: "RemovalRequests",
            columns: RemovalRequestCompositeIndex);

        migrationBuilder.CreateIndex(
            name: "IX_RemovalRequests_RequestedByUserId",
            table: "RemovalRequests",
            column: "RequestedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_RemovalRequests_Status",
            table: "RemovalRequests",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_RemovalRequests_TweetId",
            table: "RemovalRequests",
            column: "TweetId");

        migrationBuilder.CreateIndex(
            name: "IX_ViolationReports_ReportedByUserId",
            table: "ViolationReports",
            column: "ReportedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ViolationReports_ReportedUserId",
            table: "ViolationReports",
            column: "ReportedUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ViolationReports_ReviewedByUserId",
            table: "ViolationReports",
            column: "ReviewedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ViolationReports_Status",
            table: "ViolationReports",
            column: "Status");

        migrationBuilder.AddForeignKey(
            name: "FK_Users_Users_SuspendedByUserId",
            table: "Users",
            column: "SuspendedByUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Users_Users_SuspendedByUserId",
            table: "Users");

        migrationBuilder.DropTable(
            name: "RemovalApprovals");

        migrationBuilder.DropTable(
            name: "ViolationReports");

        migrationBuilder.DropTable(
            name: "RemovalRequests");

        migrationBuilder.DropIndex(
            name: "IX_Users_SuspendedByUserId",
            table: "Users");

        migrationBuilder.DropIndex(
            name: "IX_AuditLog_Action",
            table: "AuditLog");

        migrationBuilder.DropColumn(
            name: "SuspendedAt",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "SuspendedByUserId",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "SuspendedReason",
            table: "Users");

        migrationBuilder.CreateTable(
            name: "TrustedContributors",
            columns: table => new
            {
                OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TrustedXUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrustedContributors", x => new { x.OwnerUserId, x.TrustedXUsername });
                table.ForeignKey(
                    name: "FK_TrustedContributors_Users_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
    }
}
