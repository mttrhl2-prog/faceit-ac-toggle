# Windows 11 x64 validation

These checks require the target Windows machine with FACEIT Anti-Cheat already installed. Record the initial documented values and restore the desired profile when finished. Changes here are explicit manual tests, never part of the automated test suite.

1. Build/publish with the README commands. Launch the executable on Windows 11 (primarily 25H2+) and confirm one UAC prompt, charcoal/red theme, both profile cards, four rows and no extra per-row actions. Check 100%, 150% and 200% display scaling, resize and keyboard focus. A ScrollViewer preserves access on constrained displays.
2. Open and refresh for inspection only. Compare the four configured groups to the exact README table and `bcdedit /enum {current}`. Confirm this causes no writes or service stops. Create a mixed FACEIT configuration on the test machine deliberately; reopening must show Mixed/Custom without normalizing it.
3. Click an individual toggle. Confirm all other groups are unchanged, controls remain disabled during the operation, and the visual state changes only after readback. Set Memory Integrity On while hypervisor launch is Off and confirm the dependency warning and Custom state.
4. Enable the master profile. Check all eight registry values/actions in the table and hypervisor Auto, particularly `ForceEnableIommu=2` **before** reboot. Check any errors and the local log. Restart through the confirmation button, save work and confirm a normal unforced restart. After one reboot, independently check FACEIT readiness and the WMI runtime statuses. The app alone cannot prove anti-cheat compatibility.
5. Disable the master profile. Confirm all starts are `4`, the service stop is best effort, `ForceEnableIommu` is absent and its Smm key/other values remain, HVCI values are `0`, blocklist is `0`, and only hypervisor launch changes in BCD. Reboot and independently verify the intended runtime state.
6. Confirm unexpected blocklist values show Unknown, registry/BCD access failures show Error, and a write failure reports its operation and leaves any partial changes visible. Re-scan to observe changes made by another tool. WMI unavailable must remain distinct from runtime Off.
7. Cancel the restart confirmation and confirm no reboot occurs. Check logs for operation results and process exit codes, without unrelated data. Restore the desired configuration.

This checklist has **not** been executed in the Linux cloud environment.

## v1.0.2 patch acceptance on the target PC

Preserve the user-tested baseline: TPM 2.0, Secure Boot, BIOS virtualization/SVM and IOMMU remain enabled; FACEIT-enabled hypervisor launch is Auto and HVCI is running. The reported target Device Guard state is `VirtualizationBasedSecurityStatus=2`, `SecurityServicesConfigured={2}`, `SecurityServicesRunning={2}`. The user confirmed FACEIT works with HypervisorPlatform, VirtualMachinePlatform and Hyper-V disabled and explicit `hypervisoriommupolicy` absent. The utility must not add any of those back or manage Credential Guard/System Guard.

- **A — Startup:** compare all four configured states and runtime HVCI/hypervisor/FACEIT/FACEIT_IOMMU states. Confirm opening and Refresh perform no writes or service stops.
- **B — Hypervisor scan:** when `bcdedit /enum {current}` shows `hypervisorlaunchtype Auto`, the row must be On rather than Error, including with the target Windows region/language. Other fields such as `isolatedcontext Yes` or `hypervisoriommupolicy Enable` must be ignored and never changed. Check Off and missing-element defaults as well. If a scan fails, retain the exact error and BCDEdit exit/stderr log entry for diagnosis.
- **C — Enabled:** all four configured On means Current state Enabled, Enabled card active and Disabled card neutral. The badge uses the red accent.
- **D — Custom:** turn one individual setting Off. Current state is Custom, both cards are neutral and the badge is amber. No other setting is implicitly changed.
- **E — Disabled:** all four configured Off means Current state Disabled, Disabled card active and Enabled card neutral. The badge is neutral gray.
- **F — Labels/accessibility:** normal On/Off rows have no redundant `Configured: On/Off` text. Mixed/Unknown/Error remain visible. Keyboard/screen-reader names still report setting, configured state, intended action and error information; technical Detail remains in the tooltip.
- **G — Error:** simulate a read failure. The affected row stays indeterminate/Error, its tooltip and status panel show the exact reason and Refresh retries the scan. Error is never displayed as Off.
- **H — Driver restart:** disable FACEIT while its drivers are loaded. The configured toggle is Off, runtime driver(s) stay running and the footer reports `Restart required — configured changes are pending.` No kernel-driver unload occurs. Neither profile card contains a static restart bullet.
- **I — Reopen:** close before reboot and reopen. The actual driver mismatch must still produce Restart required. Check HVCI and hypervisor On/Off mismatches likewise. After a real reboot with matching observed states, the derived warning clears. Query failures remain unavailable. Blocklist-only changes have a session fallback because no reliable effective-state API is assumed.
- **J — FACEIT enable:** before reboot verify `FACEIT_IOMMU Start=0`, `FACEIT Start=1`, `FACEITService Start=3` and `ForceEnableIommu=2`. Reboot once, then independently confirm FACEIT does not request an additional installation restart and the user-tested HVCI/security checks still pass.

The Windows CI runner's tests do not replace this real Windows 11/FACEIT checklist.
