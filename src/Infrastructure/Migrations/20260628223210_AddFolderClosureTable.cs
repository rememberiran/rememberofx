using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddFolderClosureTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FolderClosures",
            columns: table => new
            {
                AncestorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DescendantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Depth = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FolderClosures", x => new { x.AncestorId, x.DescendantId });
                table.ForeignKey(
                    name: "FK_FolderClosures_Folders_AncestorId",
                    column: x => x.AncestorId,
                    principalTable: "Folders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FolderClosures_Folders_DescendantId",
                    column: x => x.DescendantId,
                    principalTable: "Folders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FolderClosures_DescendantId",
            table: "FolderClosures",
            column: "DescendantId");

        migrationBuilder.Sql(@"
                ;WITH FolderCTE AS (
                    SELECT Id AS AncestorId, Id AS DescendantId, 0 AS Depth
                    FROM Folders
                    UNION ALL
                    SELECT cte.AncestorId, f.Id AS DescendantId, cte.Depth + 1
                    FROM FolderCTE cte
                    INNER JOIN Folders f ON f.ParentFolderId = cte.DescendantId
                )
                INSERT INTO FolderClosures (AncestorId, DescendantId, Depth)
                SELECT AncestorId, DescendantId, Depth
                FROM FolderCTE
                OPTION (MAXRECURSION 100);
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FolderClosures");
    }
}
