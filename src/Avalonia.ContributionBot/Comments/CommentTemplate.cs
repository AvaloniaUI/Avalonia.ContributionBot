namespace Avalonia.ContributionBot.Comments;

/// <summary>
/// Loads the Markdown templates of <c>templates/</c>, embedded in the assembly.
/// </summary>
internal static class CommentTemplate
{
    /// <summary>
    /// Returns the text of <c>templates/{fileName}.md</c> with LF line endings and no trailing white space.
    /// </summary>
    public static string Load(string fileName)
    {
        var name = $"Templates.{fileName}.md";
        using var stream = typeof(CommentTemplate).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The comment template '{name}' isn't embedded.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd();
    }
}
