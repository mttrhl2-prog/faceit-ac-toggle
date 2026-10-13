FACEIT Switch is a Windows 11 x64 desktop utility built with .NET 10, WPF UI and CommunityToolkit.Mvvm.

Download **FACEIT-Switch-win-x64.zip**, extract it and launch **FaceitSwitch.exe**. The executable includes the .NET runtime and requests administrator privileges once at launch. Source code is available below and in the repository.

The application scans actual Windows configuration on startup, offers Enabled/Disabled profiles and four independent setting controls, verifies configuration writes, reports partial failures and keeps configured settings separate from runtime state. Hypervisor changes are limited to `hypervisorlaunchtype`; the exact registry contract is documented in the README.

The configuration suite has 42 automated tests. A Release cross-build and the tests passed in the Linux development environment. The release workflow additionally builds and tests on a Windows runner. Real Windows UI, UAC, registry/BCD/WMI, reboot and FACEIT compatibility checks still require the target machine: see `windows-validation.md` in the ZIP. The executable is unsigned.
