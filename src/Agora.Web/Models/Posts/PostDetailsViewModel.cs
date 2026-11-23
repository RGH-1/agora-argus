using Agora.Web.Data.Entities;
using Agora.Web.Models.Comments;

namespace Agora.Web.Models.Posts;

public sealed class PostDetailsViewModel
{
    public Post Post { get; init; } = null!;
    public IReadOnlyList<Comment> Comments { get; init; } = [];
    public CreateCommentViewModel NewComment { get; init; } = new();
    public bool IsCurrentUserBanned { get; init; }
}
