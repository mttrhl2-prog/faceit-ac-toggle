FACEIT Switch **v1.0.2** patches the existing Windows 11 x64 utility without changing its four-setting registry contract.

Download **FACEIT-Switch-win-x64.zip**, extract it and launch **FaceitSwitch.exe**. The executable includes the .NET runtime and requests administrator privileges once at launch. Source code is available below and in the repository.

Changes:

- Fix the reproduced v1.0.1 BCDEdit scan failure from unsupported culture-derived OEM code page `1`. Use native Windows code pages and raw UTF-8/UTF-16/OEM decoding; parse trimmed, whitespace-separated lines and ignore unrelated BCD fields. The reported target PC's exact exception still needs its log for confirmation.
- Read FACEIT and FACEIT_IOMMU kernel-driver runtime state separately through a restricted, read-only `Win32_SystemDriver` query.
- Derive restart requirements from actual HVCI, hypervisor and FACEIT configured/runtime mismatches, including after reopening. Retain only a conservative session fallback for writes whose effective state cannot be compared, including the blocklist.
- Highlight only the configured Enabled/Disabled profile card; Custom highlights neither. Hide normal On/Off row labels while retaining exceptional states, detailed tooltips and accessibility. Remove static restart bullets.
- Keep startup read-only, the four-command process allowlist, administrator manifest and all FACEIT/HVCI/blocklist write values unchanged. No extra Windows settings or features are managed.

The portable suite has 101 tests and the Windows suite has 19 tests. The release workflow requires both suites and the self-contained win-x64 Release publish to succeed, and attaches actual counts in **validation-results.json**. A Release cross-build and all portable tests also passed in the Linux development environment. Real Windows UI, UAC, registry/BCD/WMI, reboot and FACEIT compatibility checks still require the target machine: see `windows-validation.md` in the ZIP. The executable is unsigned.
