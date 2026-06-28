using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Avoid constant arrays as arguments

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddFolderIcon : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                XUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                XUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
                table.ForeignKey(
                    name: "FK_Users_Users_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "AuditLog",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CorrelationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                EntityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLog", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuditLog_Users_PerformedByUserId",
                    column: x => x.PerformedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "Folders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ParentFolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Folders", x => x.Id);
                table.ForeignKey(
                    name: "FK_Folders_Folders_ParentFolderId",
                    column: x => x.ParentFolderId,
                    principalTable: "Folders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Folders_Users_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Tweets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                XTweetId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                XTweetUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                AuthorXUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                AuthorXUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TweetText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TweetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ScreenshotBlobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                VoteCount = table.Column<int>(type: "int", nullable: false),
                FetchStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ScrapeAttempts = table.Column<int>(type: "int", nullable: false),
                ScrapeError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ScrapedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tweets", x => x.Id);
                table.ForeignKey(
                    name: "FK_Tweets_Users_SubmittedByUserId",
                    column: x => x.SubmittedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "XUserProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                XUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                XUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CustomName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_XUserProfiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_XUserProfiles_Users_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_XUserProfiles_Users_UpdatedByUserId",
                    column: x => x.UpdatedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "FolderTweets",
            columns: table => new
            {
                FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TweetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FolderTweets", x => new { x.FolderId, x.TweetId });
                table.ForeignKey(
                    name: "FK_FolderTweets_Folders_FolderId",
                    column: x => x.FolderId,
                    principalTable: "Folders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_FolderTweets_Tweets_TweetId",
                    column: x => x.TweetId,
                    principalTable: "Tweets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_FolderTweets_Users_AddedByUserId",
                    column: x => x.AddedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TweetMedia",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TweetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MediaType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                BlobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                OriginalUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                OrderIndex = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TweetMedia", x => x.Id);
                table.ForeignKey(
                    name: "FK_TweetMedia_Tweets_TweetId",
                    column: x => x.TweetId,
                    principalTable: "Tweets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Votes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TweetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VoterIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                VoterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Votes", x => x.Id);
                table.ForeignKey(
                    name: "FK_Votes_Tweets_TweetId",
                    column: x => x.TweetId,
                    principalTable: "Tweets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Votes_Users_VoterUserId",
                    column: x => x.VoterUserId,
                    principalTable: "Users",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLog_CorrelationId",
            table: "AuditLog",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLog_PerformedByUserId",
            table: "AuditLog",
            column: "PerformedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Folders_CreatedByUserId",
            table: "Folders",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Folders_ParentFolderId",
            table: "Folders",
            column: "ParentFolderId");

        migrationBuilder.CreateIndex(
            name: "IX_FolderTweets_AddedByUserId",
            table: "FolderTweets",
            column: "AddedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FolderTweets_TweetId",
            table: "FolderTweets",
            column: "TweetId");

        migrationBuilder.CreateIndex(
            name: "IX_TweetMedia_TweetId",
            table: "TweetMedia",
            column: "TweetId");

        migrationBuilder.CreateIndex(
            name: "IX_Tweets_AuthorXUserId",
            table: "Tweets",
            column: "AuthorXUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Tweets_CreatedAt",
            table: "Tweets",
            column: "CreatedAt",
            descending: Array.Empty<bool>());

        migrationBuilder.CreateIndex(
            name: "IX_Tweets_FetchStatus",
            table: "Tweets",
            column: "FetchStatus");

        migrationBuilder.CreateIndex(
            name: "IX_Tweets_SubmittedByUserId",
            table: "Tweets",
            column: "SubmittedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Tweets_VoteCount",
            table: "Tweets",
            column: "VoteCount",
            descending: Array.Empty<bool>());

        migrationBuilder.CreateIndex(
            name: "IX_Tweets_XTweetId",
            table: "Tweets",
            column: "XTweetId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_CreatedByUserId",
            table: "Users",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_XUserId",
            table: "Users",
            column: "XUserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Votes_TweetId_VoterIp",
            table: "Votes",
            columns: new[] { "TweetId", "VoterIp" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Votes_VoterUserId",
            table: "Votes",
            column: "VoterUserId");

        migrationBuilder.CreateIndex(
            name: "IX_XUserProfiles_CreatedByUserId",
            table: "XUserProfiles",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_XUserProfiles_UpdatedByUserId",
            table: "XUserProfiles",
            column: "UpdatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_XUserProfiles_XUserId",
            table: "XUserProfiles",
            column: "XUserId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditLog");

        migrationBuilder.DropTable(
            name: "FolderTweets");

        migrationBuilder.DropTable(
            name: "TweetMedia");

        migrationBuilder.DropTable(
            name: "Votes");

        migrationBuilder.DropTable(
            name: "XUserProfiles");

        migrationBuilder.DropTable(
            name: "Folders");

        migrationBuilder.DropTable(
            name: "Tweets");

        migrationBuilder.DropTable(
            name: "Users");
    }
}
