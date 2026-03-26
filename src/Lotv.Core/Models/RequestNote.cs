namespace Lotv.Core.Models;

public class RequestNote
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public PackageRequest? Request { get; set; }
    public string AuthorId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsInternal { get; set; } = true;   // internal notes not visible to requester
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
