# CustomMusicApp

This program is still in development for wider functional use and viewing. This is mainly a proof of concept. 

## Usage:

To load your local playlist, enter your folder path in the "folderPath" variable.

This app **locally** tracks play time, songs played, and history engaging with the app (Play, pause, album creation, etc.) which can be viewed in the "history.txt" and "tracking.sqlite" files in the bin folder. 

You can double click a song from the "Music Library" or "Album Songs" tab to play them in the "Play Queue".

You can create albums with the list box on the left through right-clicking. Right-click functionality for individual tracks only works when in the "Play Queue" tab (unless removing a song from an album in the album tab).

You can search for songs with the empty bar above the album view and view them in the "Search Results" tab.

## Known bugs:

- After extensive use with the program, it may begin to slow down and lag. Close and reopen the program to refresh it.
- Some filenames may not be compatable and may crash the program (specifically anything with emojis) and playing a blank line will also crash the program.
- Some filenames may be long and clip into the controls of the program. This is only a visual bug.
- Upon adding a song to an album for the first time, the tab will switch unintendedly.
- Using any of the media buttons may crash the program if there isn't anything in the play queue.
