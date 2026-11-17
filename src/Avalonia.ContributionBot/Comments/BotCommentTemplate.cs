namespace Avalonia.ContributionBot.Comments;

/// <summary>
/// The pull request comments posted by the bot.
/// Each value matches a <c>templates/{name}.md</c> file embedded in the assembly.
/// </summary>
public enum BotCommentTemplate
{
    NotVouched,
    Denounced,
    ClaRequired,
    ClaAccepted
}
