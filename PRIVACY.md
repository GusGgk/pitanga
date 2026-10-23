# Privacy

**Pitanga does not collect, send or store any personal data.**

- **No network access.** All conversions and edits run locally on your PC. Pitanga never uploads your
  files and never contacts a server — there is no telemetry, analytics, crash reporting or update check.
- **Keyboard and mouse.** To detect the *drag a file + tap Shift* gesture, Pitanga installs a low-level
  keyboard and mouse hook. It only looks at the left mouse button, mouse movement while that button is
  held, `Shift` and `Esc`. It never records, logs or transmits keystrokes, and it never blocks any key.
- **Screen.** When the menu opens, Pitanga captures the small area behind it (about 440×440 pixels) to draw
  the frosted-glass effect. That image only lives in memory and is discarded when the menu closes.
- **Microsoft Office.** Converting Word, Excel or PowerPoint files uses the Office apps already installed on
  your PC, in the background. Pitanga opens the source file read-only.
- **Files on disk.** Pitanga only writes:
  - the converted/edited file, next to the original (it never overwrites an existing file);
  - `%LOCALAPPDATA%\Pitanga\pitanga.log` — errors and a one-line memory sample, for troubleshooting;
  - `%APPDATA%\Pitanga\settings.json` — your preferences;
  - the `Pitanga` value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, only if *Start with Windows* is on.

Uninstalling removes all of the above except the files you converted.

---

**Privacidade (pt-BR):** o Pitanga não coleta nem envia nenhum dado. Tudo roda no seu PC, sem internet.
O hook de teclado/mouse só observa o botão esquerdo, `Shift` e `Esc` para detectar o gesto — nada é gravado.
