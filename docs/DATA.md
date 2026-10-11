# Data and recovery

Journals accept up to 64 million serialized characters. The same limit applies before saving and when reading, so a successful save remains readable. Individual notes are limited to 8,000 characters. If the total limit is reached, the previous saved file is kept and the app asks you to export a backup.

Default journal: `%LOCALAPPDATA%\ThisWidget\journal.json`. All dates use ISO `yyyy-MM-dd`; weekday plans repeat for dates without a saved session snapshot. The app uses the Windows local clock for today and reminders.

An installed executable in `ThisWidget\app` always uses `ThisWidget\journal.json` beside that app folder. If installation ran through a packaged launcher, Windows may redirect that folder into the launcher's `LocalCache`. The physical path is retained for shortcuts, sign-in startup and the journal, so reopening from Explorer uses the existing history and appearance rather than a separate empty profile.

Every save writes a temporary file, then atomically replaces the journal and keeps the previous save as `.bak`. If the main file is invalid, it is preserved under a `.corrupt-...` name before attempting recovery from `.bak`. If neither file is readable, startup reports the problem and does not silently replace them with an empty journal.

Export and restore are available in Settings. A restore validates the schema, keeps a `.before-import-...` safety copy, and retains the current appearance settings. A backup includes workout plans, progress, notes and settings. Treat exported journals as personal data.

The first normal launch can copy a previous Yearform journal into ThisWidget. Migration never overwrites an existing ThisWidget journal and never modifies the original file. `--data` uses the supplied path and bypasses migration and startup registration.

The optional daily reminder is a local tray notification, not a server or Windows scheduled task. The app must be running. No network access is needed.

Uninstall removes `%LOCALAPPDATA%\ThisWidget\app` and app shortcuts only. Your journal remains. Export a backup before manually removing the complete data folder.
