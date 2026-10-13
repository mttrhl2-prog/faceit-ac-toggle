using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public abstract class SettingController(ILoggingService log) : ISettingController
{
    public abstract SettingId Id { get; }
    protected abstract Task<SettingReading> ReadConfigurationAsync(CancellationToken cancellationToken);
    protected abstract Task ApplyConfigurationAsync(bool enabled, Changes changes, CancellationToken cancellationToken);

    public async Task<SettingReading> ReadStateAsync(CancellationToken cancellationToken = default)
    {
        try { return await ReadConfigurationAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception ex)
        {
            log.Write($"Read {Id} failed", ex);
            return new(SettingState.Error, ex.Message);
        }
    }

    public Task<OperationResult> EnableAsync(CancellationToken cancellationToken = default) => ApplyAsync(true, cancellationToken);
    public Task<OperationResult> DisableAsync(CancellationToken cancellationToken = default) => ApplyAsync(false, cancellationToken);

    private async Task<OperationResult> ApplyAsync(bool enabled, CancellationToken cancellationToken)
    {
        var changes = new Changes();
        var before = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        log.Write($"{Id}: old={before.State}; requested={(enabled ? "Enabled" : "Disabled")}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ApplyConfigurationAsync(enabled, changes, cancellationToken).ConfigureAwait(false);
            var after = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
            var expected = enabled ? SettingState.Enabled : SettingState.Disabled;
            if (after.State != expected)
                throw new InvalidOperationException($"Verification failed: expected {expected}, found {after.State}. {after.Detail}");
            log.Write($"{Id}: success; verified={after.State}; changed={changes.Any}");
            return new(true, $"{Id}: {after.State}", changes.Any, changes.Warnings.ToArray());
        }
        catch (Exception ex)
        {
            log.Write($"{Id}: operation failed; partial changes={changes.Any}", ex);
            return new(false, $"{Id}: {ex.Message}", changes.Any, changes.Warnings.ToArray());
        }
    }

    protected sealed class Changes
    {
        public bool Any { get; private set; }
        public List<string> Warnings { get; } = [];
        public void Record(bool changed) => Any |= changed;
    }
}
