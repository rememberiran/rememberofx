using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTrustedContributorsAndSubmissionStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymous",
            table: "Tweets",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReviewedAt",
            table: "FolderTweets",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "FolderTweets",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: false,
            defaultValue: "approved");

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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TrustedContributors");

        migrationBuilder.DropColumn(
            name: "IsAnonymous",
            table: "Tweets");

        migrationBuilder.DropColumn(
            name: "ReviewedAt",
            table: "FolderTweets");

        migrationBuilder.DropColumn(
            name: "Status",
            table: "FolderTweets");
    }
}
