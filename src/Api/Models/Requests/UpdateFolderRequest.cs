using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public class UpdateFolderRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    [MaxLength(10)]
    public string? Visibility { get; set; }

    public Guid? ParentFolderId { get; set; }
}
