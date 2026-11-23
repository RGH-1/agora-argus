using Agora.Web.Data.Entities;
namespace Agora.Web.Models.Forums;
public class ForumDetailsViewModel
{
    public Forum Forum { get; init; } = null!;
    public IReadOnlyList<Post> Posts { get; init; } = [];
    public IReadOnlyDictionary<int, int> LikeCounts { get; init; } = new Dictionary<int, int>();
}
