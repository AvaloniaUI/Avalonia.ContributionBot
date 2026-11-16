namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles the payload of one kind of GitHub event.
/// </summary>
public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent payload, CancellationToken cancellationToken);
}
