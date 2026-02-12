using MacroRecorder.Core.Domain;

namespace MacroRecorder.Input;

public interface IInputPlaybackService
{
    Task PlaybackAsync(IReadOnlyList<MacroAction> actions, CancellationToken cancellationToken = default);
}
