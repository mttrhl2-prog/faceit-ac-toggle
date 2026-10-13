using FaceitSwitch.Core.Models;

namespace FaceitSwitch.Core.Services;

public sealed class ProfileService
{
    private readonly IReadOnlyList<ISettingController> controllers;
    private readonly SemaphoreSlim operationLock = new(1, 1);
    public ConfigurationSnapshot? LastSnapshotBeforeOperation { get; private set; }

    public ProfileService(IEnumerable<ISettingController> controllers)
    {
        this.controllers = controllers.ToArray();
        if (this.controllers.Count != 4 || this.controllers.Select(c => c.Id).Distinct().Count() != 4)
            throw new ArgumentException("Exactly one controller for each of the four settings is required.", nameof(controllers));
    }

    public async Task<ConfigurationSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false); }
        finally { operationLock.Release(); }
    }

    public Task<ProfileOperationResult> ApplyProfileAsync(bool enabled, CancellationToken cancellationToken = default)
        => ApplyAsync(controllers, enabled, cancellationToken);

    public Task<ProfileOperationResult> ApplySettingAsync(SettingId id, bool enabled, CancellationToken cancellationToken = default)
        => ApplyAsync([controllers.Single(c => c.Id == id)], enabled, cancellationToken);

    private async Task<ProfileOperationResult> ApplyAsync(IEnumerable<ISettingController> selected, bool enabled, CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var before = await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            LastSnapshotBeforeOperation = before;
            var operations = new Dictionary<SettingId, OperationResult>();
            foreach (var controller in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    operations[controller.Id] = await (enabled ? controller.EnableAsync(cancellationToken)
                        : controller.DisableAsync(cancellationToken)).ConfigureAwait(false);
                }
                catch (Exception ex) { operations[controller.Id] = new(false, $"{controller.Id}: {ex.Message}"); }
            }
            var after = await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            var expected = enabled ? SettingState.Enabled : SettingState.Disabled;
            foreach (var id in operations.Keys.ToArray())
                if (operations[id].Success && after.Settings[id].State != expected)
                    operations[id] = operations[id] with { Success = false, Message = $"{id}: final verification found {after.Settings[id].State}. {after.Settings[id].Detail}" };
            return new(before, after, operations);
        }
        finally { operationLock.Release(); }
    }

    private async Task<ConfigurationSnapshot> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        var readings = new Dictionary<SettingId, SettingReading>();
        foreach (var controller in controllers)
        {
            try { readings[controller.Id] = await controller.ReadStateAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { readings[controller.Id] = new(SettingState.Error, ex.Message); }
        }
        return new(readings);
    }
}
