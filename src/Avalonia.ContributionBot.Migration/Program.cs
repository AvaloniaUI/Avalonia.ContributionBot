using System.CommandLine;
using System.CommandLine.Help;
using System.Text.Json;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;

namespace Avalonia.ContributionBot.Migration;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var input = new Option<FileInfo>("--input")
        {
            Description = "Path to the legacy signee-cla.json file.",
            Required = true
        };

        var output = new Option<FileInfo>("--output")
        {
            Description = "Path of the contributors.json file to write.",
            Required = true
        };

        var repository = new Option<RepositoryName>("--repository")
        {
            Description = "Repository in which the legacy signatures were made (owner/name).",
            DefaultValueFactory = _ => new RepositoryName("AvaloniaUI", "Avalonia"),
            CustomParser = result =>
            {
                var value = result.Tokens[0].Value;
                if (RepositoryName.TryParse(value, out var name))
                    return name;

                result.AddError($"Option '--repository' must be in the 'owner/name' format, got '{value}'.");
                return default;
            }
        };

        var repositoryId = new Option<long>("--repository-id")
        {
            Description = "GitHub ID of the repository, which every legacy signature must reference.",
            DefaultValueFactory = _ => 14964475
        };

        var rootCommand = new RootCommand(
            "Converts the legacy CLA bot's signee-cla.json into contributors.json. " +
            "Every signer is imported as vouched. The output is written locally for review.")
        {
            input,
            output,
            repository,
            repositoryId
        };

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var inputFile = parseResult.GetRequiredValue(input);
            var outputFile = parseResult.GetRequiredValue(output);

            try
            {
                var legacy = LegacySignatureFile.Deserialize(await File.ReadAllTextAsync(inputFile.FullName, cancellationToken));
                var result = LegacyMigration.Migrate(legacy, parseResult.GetRequiredValue(repository), parseResult.GetRequiredValue(repositoryId));

                foreach (var merge in result.Merges)
                    Console.WriteLine($"Merged duplicate signature of {merge.Login} ({merge.Id}): kept {merge.KeptSource}, dropped {merge.DroppedSource}");

                await File.WriteAllTextAsync(outputFile.FullName, ContributorsDocumentSerializer.Serialize(result.Document), cancellationToken);

                Console.WriteLine(
                    $"Wrote {result.Document.Contributors.Count} contributors from {legacy.SignedContributors.Count} signatures " +
                    $"({result.Merges.Count} duplicates merged) to {outputFile.FullName}.");

                return 0;
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        });

        var parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0 && parseResult.Action is not HelpAction)
        {
            foreach (var error in parseResult.Errors)
                Console.Error.WriteLine(error.Message);

            return 2;
        }

        return await parseResult.InvokeAsync();
    }
}
