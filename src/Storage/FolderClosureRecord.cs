namespace Storage;

/// <summary>
/// Represents a record in the folder closure table, which is used to efficiently query hierarchical folder structures.
/// </summary>
public class FolderClosureRecord
{
    public Guid AncestorId { get; set; }
    public Guid DescendantId { get; set; }
    public int Depth { get; set; }

    public FolderRecord Ancestor { get; set; } = default!;
    public FolderRecord Descendant { get; set; } = default!;
}
