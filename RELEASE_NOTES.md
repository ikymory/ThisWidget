ThisWidget 1.1.0 makes adding the fixed home screen widget explicit.

- Add Widget opens a home screen setup panel with add, status and reset-position controls.
- Widget placement stays behind apps, with a locked position when added. Floating and always-on-top controls have been removed.
- Color, size, opacity, exercise checklist, weekly plans and all saved progress are preserved.
- No Windows Widgets provider, board package, separate service or board setup is included.
- Updates retain widget visibility preferences; relaunching the startup command cannot silently restore a removed widget.

Download and extract the matching zip, then run Install.cmd to update. Open ThisWidget.exe to show the widget, or choose Add Widget in the calendar.

The runtime is included. Windows x64 was tested locally; ARM64 is cross-built and hardware execution remains unverified. Releases are unsigned.

## 1.0.1

ThisWidget 1.0.1 fixes desktop widget startup after an installation through a packaged launcher.

- Windows sign-in and shortcuts now point to the physical executable instead of an AppData path visible only inside the installer container.
- Installed apps reuse the same journal from Explorer, retaining progress, plans, colors and size settings.
- Updates and uninstall resolve the same physical installation folder.
- Added native desktop visibility checks and installed/portable profile regression tests.

Download and extract the matching zip, then run Install.cmd to update. Your journal is preserved. The widget stays pinned behind other apps; show your desktop to see it.

The runtime is included. Windows x64 was tested locally; ARM64 is cross-built and hardware execution remains unverified. Releases are unsigned.

## 1.0.0

ThisWidget 1.0.0 introduced an offline Windows workout calendar and a compact desktop widget.

- Full-year completion boxes, today's weekday/focus, weekly goals and streaks.
- Recurring weekday workouts, individual checkmarks, date overrides and notes.
- Slim themed scrollbars and controls; color, theme, scale, opacity, position lock and week-start settings.
- Fixed Explorer desktop widget with position locked by default, optional floating/on-top modes, tray recovery and Ctrl+Alt+W.
- Starts as a fixed desktop widget behind apps, with position locked. The calendar opens from the widget or tray.
- Optional local reminders, startup, JSON backup/restore and PNG export.
- Previous Yearform journals migrate without modifying the originals.

Download the zip matching your Windows architecture, extract it, and run ThisWidget.exe. Install.cmd optionally creates shortcuts. The runtime is included. Uninstall.cmd preserves your journal.

Launching the app shows the fixed desktop widget. Use its ↗ button or Ctrl+Alt+W to open the separate calendar editor. Unlock position in Settings only when you want to move the widget.

Windows x64 was tested locally. ARM64 is cross-built by the release workflow; execution on real ARM64 hardware is not yet verified. Current releases are unsigned.
