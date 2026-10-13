# FACEIT Switch

A local Windows 11 x64 utility for selecting the FACEIT Enabled or Disabled profile, or configuring four settings independently. The WPF window uses a charcoal theme, red accents, two profile cards and four security rows matching the supplied design. It uses .NET 10, WPF UI and CommunityToolkit.Mvvm; there are no application network requests, telemetry, background services or scheduled tasks.

Launch `FaceitSwitch.exe` and accept its single administrator UAC prompt. Opening or refreshing the window only reads Windows configuration. On, Off, Mixed, Unknown and Error states are distinct; clicking an indeterminate toggle explicitly requests On. Controls display verified configuration, not optimistic changes. Runtime Memory Integrity and hypervisor status appear separately.

## Download

Windows packages are published on the [GitHub Releases page](https://github.com/mttrhl2-prog/faceit-ac-toggle/releases). Download `FACEIT-Switch-win-x64.zip`, extract it and run `FACEIT-Switch/FaceitSwitch.exe`. GitHub also provides source ZIP downloads. A `v*` tag starts the release workflow: the Windows build and tests must succeed before the package is published.

## Build and test

Install the .NET SDK pinned in `global.json`. From the repository root on Windows:

```powershell
dotnet restore FaceitSwitch.slnx --locked-mode
dotnet build FaceitSwitch.slnx -c Release --no-restore
dotnet test tests/FaceitSwitch.Tests/FaceitSwitch.Tests.csproj -c Release --no-build --no-restore
dotnet publish src/FaceitSwitch/FaceitSwitch.csproj -c Release --no-restore -o artifacts/win-x64
```

The output is a self-contained Windows x64 executable, with .NET/WPF included. PDB files are optional debugging symbols. The application cannot run on Linux. Linux can compile the WPF project using `EnableWindowsTargeting` and run the portable configuration tests. `bash scripts/cloud-setup.sh` installs the pinned SDK outside the checkout, checks its official SHA-512, restores locked dependencies, builds, tests and publishes. The script does not edit source or package locks. No server is needed.

## Implementation

- `src/FaceitSwitch`: WPF MVVM UI and native Windows adapters for the 64-bit HKLM registry, System32 processes, FACEITService, WMI, local logging and restart.
- `src/FaceitSwitch.Core`: setting controllers and serialized profile orchestration, independent of WPF and native Windows calls. Registry paths are kept in services.
- `tests/FaceitSwitch.Tests`: tests using in-memory registry, process and service adapters. They exercise the exact state rules, changes, defaults, independent toggles, verification failures, partial failures and read-only startup. They never change the host's Windows configuration.

Every mutation is restricted to an explicit allowlist in the native adapters. The profile service reads an in-memory before snapshot, applies each selected group, verifies it, and rescans all groups. A failed group is reported individually. Other requested groups are still attempted; partial changes remain visible. There is no automatic rollback that could conceal a failure or overwrite a concurrent external change. FACEIT must already be installed; the utility does not create missing service registrations.

## Exact configuration contract

All registry paths below are under **HKLM**, using **Registry64**. Values written are **DWORDs**. No whole registry keys are deleted.

| Group / path / value | Enabled | Disabled |
| --- | --- | --- |
| `SYSTEM\CurrentControlSet\Services\FACEIT_IOMMU` / `Start` | `0` | `4` |
| `SYSTEM\CurrentControlSet\Services\FACEIT` / `Start` | `1` | `4` |
| `SYSTEM\CurrentControlSet\Services\FACEITService` / `Start` | `3` | `4` |
| `SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Smm` / `ForceEnableIommu` | `2`, created before restoring driver starts | Delete this value only |
| `SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity` / `Enabled` | `1` | `0` |
| Same HVCI path / `WasEnabledBy` | `2` | `0` |
| `SOFTWARE\Policies\Microsoft\Windows\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity` / `Enabled` | Delete this policy override value only | Leave unchanged |
| `SYSTEM\CurrentControlSet\Control\CI\Config` / `VulnerableDriverBlocklistEnable` | `1` | `0` |
| Current boot entry / `hypervisorlaunchtype` | `bcdedit /set {current} hypervisorlaunchtype auto` | `bcdedit /set {current} hypervisorlaunchtype off` |

Disabling FACEIT attempts to stop only the user-mode `FACEITService`; a stop failure is a visible warning and does not prevent changing its startup configuration. Kernel drivers are not forcibly unloaded.

FACEIT is On only if all four FACEIT values match the enabled column. It is Off only if all three starts are `4` and `ForceEnableIommu` is absent. Every other combination is Mixed. HVCI policy `Enabled=0` takes precedence; otherwise local `Enabled=1` means On and `0`/missing means Off. A missing hypervisor launch element defaults to Auto only after a successful, nonempty `bcdedit /enum {current}` response. A missing blocklist value defaults to On on the targeted modern Windows versions; unexpected values and types are Unknown.

The VBS-labelled row controls **hypervisor launch only**. It does not change Device Guard, Credential Guard, System Guard, optional Windows features, `IOMMUFlags` or other settings. An individually enabled Memory Integrity setting with hypervisor launch Off stays Custom and displays the specified informational warning.

WMI reads `Win32_DeviceGuard.SecurityServicesRunning` (value `2` means HVCI is running) and `Win32_ComputerSystem.HypervisorPresent`. Query failures are shown as unavailable, not Off. Configuration writes do not imply runtime success or FACEIT compatibility. The restart marker tracks writes in the current application session; a new scan can also identify Memory Integrity/hypervisor runtime mismatches. FACEIT driver and blocklist effective runtime enforcement is not inferred. After relaunch, there is no persisted toggle state to trust.

The restart button asks for confirmation and runs `shutdown.exe /r /t 0`, without `/f`. Local daily logs are under `%LocalAppData%\FaceitSwitch\Logs\` and record launch, requested/old setting state, verification results, process exit codes and exception messages. There is no automatic upload. A logging failure is surfaced without misreporting the result of a Windows configuration change.

## Windows validation

Linux validation covers cross-compilation and fake-backed configuration logic. It does not validate the displayed window, UAC, real registry permissions, BCDEdit, WMI, driver loading, restart or FACEIT readiness. Follow [the Windows 11 checklist](docs/windows-validation.md) on the target machine before treating those operations as verified. The executable is unsigned.
