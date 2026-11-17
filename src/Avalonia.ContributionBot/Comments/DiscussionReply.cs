namespace Avalonia.ContributionBot.Comments;

/// <summary>
/// The replies posted by the bot to discussion commands.
/// Each value matches a <c>templates/{name}Reply.md</c> file embedded in the assembly.
/// </summary>
public enum DiscussionReply
{
    Vouched,
    Denounced,
    Unvouched,
    Undenounced,
    AlreadyVouched,
    AlreadyDenounced,
    NothingToRemove,
    UserNotFound
}
