LineNumbers = true;
OpenFileManager = false;
UseNerdIcons = true;
ShowTagBar = true; //(Ctrl+Shift+T toggles the Text-mode tag bar during a session)
DefaultKeysMode = Code; //{Code, VIM}
DefaultMode = Text; //{Text, Development}
DefaultTab = 5; //(The number of spaces to use when hitting tab.  This should be an actual tab and not the number of spaces set.  The number is just to give an idea of the amount of space.)
TabSpaces = false; //(Use spaces instead of tabs if true)
FileManagerWidth = 20; //(Percentage of the screen width that the File Manager Panel takes up)
FontSize = 12; //(The size in pts that the text should be by default)
ConsoleHeight = 10; //(The default height of the console when shown on screen.  Is a percentage of the total height)

if (Mode == Development)
{
	ShowConsole = true; //(Show a console at the bottom the window by default)
}
else
{
	ShowConsole = false;
}
