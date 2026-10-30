# Localization check (non-English Windows)

Windows performance counter names are localized: on Portuguese Windows, `PhysicalDisk` is
`Disco Físico` and `% Idle Time` is `% Tempo Ocioso`. DiskPulse never uses those names. It
adds every counter with `PdhAddEnglishCounterW` and an English path, and Windows maps that
to the local name. Disk labels come from WMI, whose class and property names are never
localized.

Run this check before each release, and whenever the counter code changes.

## What you need

A Windows 10 or 11 install whose **display language** is not English. Any of these works:

- A VM (Hyper-V, VirtualBox) with a pt-BR install image.
- Your normal PC with the pt-BR language pack set as the display language.
- Windows Sandbox. It inherits the host's language, so set the host to pt-BR first.

To change the display language: **Settings → Time & language → Language & region →
Windows display language → Português (Brasil)**, then sign out and back in.

## Steps

1. Build or download `DiskPulse.Console`.
2. Open a normal (not administrator) Command Prompt and run:

   ```
   DiskPulse.Console --diag
   ```

3. Check the output:
   - `UI language` shows `pt-BR`.
   - Every counter line shows the English path, then a **localized** path, for example:

     ```
     \PhysicalDisk(*)\% Idle Time
       -> \Disco Físico(*)\% Tempo Ocioso
     ```

     If the arrow line still shows English text, the system is not actually running in
     another language, so the check proves nothing. Fix the display language and retry.
   - `Disks from WMI` lists every disk with model and type.
   - The sample shows one block per disk, with numbers in local format (`1,5 ms`).
   - The last line is `Result: OK`, and the exit code is 0 (`echo %ERRORLEVEL%`).

4. Run `DiskPulse.Console` with no options for about 30 seconds while copying a large file.
   Throughput and active time should move.

## If it fails

`Windows could not find the performance counter ...` means the counter registry is
damaged. This happens on any language. Run `lodctr /R` in an administrator Command
Prompt, then retry.

## Record the result

Add a line to the release notes, for example:
`Localization check: Windows 11 23H2 pt-BR, OK (2 disks).`
