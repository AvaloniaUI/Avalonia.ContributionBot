namespace Avalonia.ContributionBot.Comments;

public static class DiscussionReplyExtensions
{
    /// <param name="reply">The reply to render.</param>
    extension(DiscussionReply reply)
    {
        /// <summary>
        /// Returns the reply body for <paramref name="reply"/>.
        /// </summary>
        /// <param name="user">The login of the user targeted by the command.</param>
        /// <param name="maintainer">The login of the maintainer who posted the command.</param>
        public string Render(string user, string maintainer)
            => CommentTemplate.Load($"{reply}Reply")
                .Replace("{user}", user)
                .Replace("{maintainer}", maintainer);
    }
}
