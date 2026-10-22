# Nerd Font & Icons

RudyEdit includes FiraCode Nerd Font Mono (Nerd Fonts 3.4.0). The Windows
installer registers it and the Manjaro package installs it for fontconfig.
The font and its licenses are also in assets/fonts for portable installations.

Choose this font in your terminal host to display the graphical icons:

Windows Terminal: Settings → your profile → Appearance → Font face →
FiraCode Nerd Font Mono → Save. Restart the terminal after installing the font
if it does not appear in the list.
https://learn.microsoft.com/windows/terminal/tutorials/custom-prompt-setup

Ghostty: add the following line to your Ghostty configuration and reload it:
font-family = "FiraCode Nerd Font Mono"
You can also try: ghostty --font-family="FiraCode Nerd Font Mono"
https://ghostty.org/docs/config

Other terminals: select FiraCode Nerd Font Mono in their font preferences.
Portable builds do not register fonts; install the bundled .ttf first.

A console application cannot select a font for the host terminal. Text → Font
sets document markup, not the terminal's font. If icons appear as empty squares,
select the font above or use View → Toggle Graphic Icons to switch to plain
file-type labels for this session. For a persistent fallback, add this setting
to Config.cs, then save:
UseNerdIcons = false;
Omitting the setting defaults to true. Existing user configuration is preserved.

Icons appear on main menus, menu commands, the command palette and the Text-mode
formatting bar. Menu initials remain underlined and Alt+letter still works.
Bold, Italic and Underline use graphical buttons; Format opens a labeled list
of all controls. Ctrl+Alt+M, Left/Right and Enter provide keyboard access.

File Manager uses colored, single-cell icons for the parent folder, ordinary
and open folders, source/config/Git/GitHub/dependency folders, source languages,
text/Markdown, configuration/data, images, archives, office documents, PDF,
audio/video, fonts and binaries. Unknown file types retain a generic file icon.
Names and extensions remain visible, so identification does not rely on color.
Tag search results use the same file icons. Click the icon or filename to select
the entry, then Enter to open it. File icons do not change which formats the
editor can open; binary files are still rejected.

The legend below shows every bundled UI icon and its label. In plain mode it
shows file-type abbreviations instead. The icons are UI decoration only and
are never inserted into your documents by the menus or File Manager.
