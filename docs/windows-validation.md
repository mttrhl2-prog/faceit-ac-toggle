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
