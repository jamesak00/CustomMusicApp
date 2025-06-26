using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Linq;
using Microsoft.VisualBasic;
using System.Reflection.PortableExecutable;
using System.Data.SQLite;


namespace CustomMusicApp
{
    public partial class MainWindow : Window
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private List<string> songs = new List<string>();
        private List<string> albumSongs = new List<string>(); //this is used to populate the list of just the album songs to view them, while songs is used for the play queue
        private List<string> musicLib = new List<string>();
        private List<string> albums = new List<string>();
        private int currentSongIndex = 0;
        private Random random = new Random();
        private bool wasPlaying = false;
        private bool isDragging = false;
        DispatcherTimer timer = new DispatcherTimer();
        private List<string> searchResults = new List<string>();
        private string selectedSong;
        private SQLiteConnection connection;
        private bool loopBool = false;
        TextBoxStreamWriter writer;
        private string songName;

        public class MusicFile
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public DateTime DateAdded { get; set; }
        }

        public MainWindow()
        {
            /*Program initialization*/

            InitializeComponent();

            this.Closed += (s, e) =>
            {
                mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
                mediaPlayer.MediaEnded -= Media_Ended;
                listViewAlbums.SelectionChanged -= listViewAlbums_SelectionChanged;
                txtSearchBar.TextChanged -= TxtSearchBar_TextChanged;
                txtSearchBar.GotFocus -= TxtSearchBar_GotFocus;
                timer.Tick -= Timer_Tick;

                listBoxMusicLib.ContextMenuOpening -= ListBox_ContextMenuOpening;
                listBoxSongs.ContextMenuOpening -= ListBox_ContextMenuOpening;
                listBoxSearchResults.ContextMenuOpening -= ListBox_ContextMenuOpening;
                listBoxAlbumSongs.ContextMenuOpening -= ListBox_ContextMenuOpening;

                listBoxSongs.MouseDoubleClick -= listBoxSongs_MouseDoubleClick;
                listViewAlbums.MouseDoubleClick -= listViewAlbums_MouseDoubleClick;
                btn_play.Click -= btn_play_Click;
                btn_shuffle.Click -= btn_shuffle_Click;
                btn_next.Click -= btn_next_Click;
                btn_prev.Click -= btn_prev_Click;
                listBoxSearchResults.MouseDoubleClick -= listBoxSearchResults_MouseDoubleClick;
                listViewAlbums.MouseDown -= listViewAlbums_MouseDown;
                btn_seekBack.Click -= btn_seekBack_Click;
                btn_seekForw.Click -= btn_seekForw_Click;
                btn_loop.Click -= btn_loop_Click;
            };


            // Initialize the connection in your constructor or initialization method
            this.connection = new SQLiteConnection("Data Source=tracking.sqlite");
            this.connection.Open();

            string createSongsTableQuery = @"
                CREATE TABLE IF NOT EXISTS songs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                length REAL NOT NULL
             );";

            string createSongPlaysTableQuery = @"
                CREATE TABLE IF NOT EXISTS song_plays (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                song_id INTEGER,
                play_time TEXT NOT NULL,
                play_length REAL NOT NULL,
                FOREIGN KEY (song_id) REFERENCES songs(id)
             );";

            using (var command = new SQLiteCommand(createSongsTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var command = new SQLiteCommand(createSongPlaysTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            connection.Close();

            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

            // Create a TextBoxStreamWriter
            writer = new TextBoxStreamWriter(txtConsole, "history.txt");
            // Set the console output to the TextBoxStreamWriter
            Console.SetOut(writer);

            string splashPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Splash", "minecraft splash text.txt");
            string[] splashTexts = System.IO.File.ReadAllLines(splashPath);

            Random rand = new Random();
            int i = rand.Next(0, splashTexts.Length);
            this.Title = "JSH Custom Music Player v1.0.3.1 - " + splashTexts[i];

            // Load songs from a folder
            string folderPath = @""; // Set the folder path here. May have to have a textbox pull from this, and i might need more audio sources for like, flac or other types of audio
            RestoreOriginalList(folderPath);

            //QUICK TEST OF FILTERS
            List<string> sortedSongs = musicLib.Select(song => new FileInfo(song))
                                 .OrderByDescending(fileInfo => fileInfo.CreationTime) // Use OrderByDescending or other filters here
                                 .Select(fileInfo => fileInfo.FullName)
                                 .ToList();

            musicLib.Clear();
            musicLib.AddRange(sortedSongs);
            listBoxMusicLib.Items.Clear();
            foreach (var song in sortedSongs)
            {
                listBoxMusicLib.Items.Add(System.IO.Path.GetFileNameWithoutExtension(song));
            }

            // Attach event handlers for MediaEnded and MediaOpened events
            mediaPlayer.MediaEnded += new EventHandler(Media_Ended);
            mediaPlayer.MediaOpened += new EventHandler(Media_Opened);

            // Set the timer interval
            timer.Interval = TimeSpan.FromMilliseconds(25);
            // Attach Tick event handler
            timer.Tick += Timer_Tick;

            string albumsPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums");
            string[] files = Directory.GetFiles(albumsPath, "*.txt");
            foreach (string file in files)
            {
                albums.Add(file);
                listViewAlbums.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file));
            }
            listViewAlbums.SelectionChanged += listViewAlbums_SelectionChanged;

            txtSearchBar.TextChanged += TxtSearchBar_TextChanged;
            txtSearchBar.GotFocus += TxtSearchBar_GotFocus;

            //List of music files and not just songs txt value
            List<MusicFile> musicFiles = new List<MusicFile>();

            foreach (string file in Directory.EnumerateFiles(folderPath, "*.*"))
            {
                if (System.IO.Path.GetExtension(file) == ".mp3" || System.IO.Path.GetExtension(file) == ".wav" || System.IO.Path.GetExtension(file) == ".ogg")
                {
                    var musicFile = new MusicFile
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(file),
                        Path = file,
                        DateAdded = System.IO.File.GetCreationTime(file)
                    };

                    musicFiles.Add(musicFile);
                }
            }

            //Initalize the menu
            reloadAddMenu();

            listBoxMusicLib.ContextMenuOpening += ListBox_ContextMenuOpening;
            listBoxSongs.ContextMenuOpening += ListBox_ContextMenuOpening;
            listBoxSearchResults.ContextMenuOpening += ListBox_ContextMenuOpening;
            listBoxAlbumSongs.ContextMenuOpening += ListBox_ContextMenuOpening;
        }

        private void UpdateUI(Action action)
        {
            Dispatcher.Invoke(action);
        }

        private void reloadAddMenu()
        {
            // Find the ContextMenu from the resources
            ContextMenu contextMenu = this.Resources["mainCtx"] as ContextMenu;

            // Find the "Add to..." MenuItem
            MenuItem addToMenuItem = null;
            foreach (object item in contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header.ToString() == "Add to...")
                {
                    addToMenuItem = menuItem;
                    break;
                }
            }

            if (addToMenuItem != null)
            {
                addToMenuItem.Items.Clear();
                // Add each album as a submenu item
                foreach (string album in albums)
                {
                    // Get the file name without the extension
                    string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(album);

                    MenuItem albumMenuItem = new MenuItem
                    {
                        Header = fileNameWithoutExtension,
                        Tag = album  // Store the full path in the Tag property
                    };
                    albumMenuItem.Click += AlbumMenuItem_Click; // Event handler for when an album is clicked
                    addToMenuItem.Items.Add(albumMenuItem);
                }
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            string newAlbumName = Microsoft.VisualBasic.Interaction.InputBox("Enter new album name", "New Album");
            if (!string.IsNullOrEmpty(newAlbumName))
            {
                // Unsubscribe from SelectionChanged event temporarily
                listViewAlbums.SelectionChanged -= listViewAlbums_SelectionChanged;

                string newAlbumPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", newAlbumName + ".txt");
                System.IO.File.Create(newAlbumPath).Close();

                albums.Clear();
                listViewAlbums.Items.Clear();

                string albumsPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums");
                string[] files = Directory.GetFiles(albumsPath, "*.txt");
                foreach (string file in files)
                {
                    albums.Add(file);
                    listViewAlbums.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                }

                // Re-subscribe to SelectionChanged event after updating listViewAlbums
                listViewAlbums.SelectionChanged += listViewAlbums_SelectionChanged;
                reloadAddMenu();
                Console.WriteLine($"Added the \"{newAlbumName}\" album at {DateTime.Now}");
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (listViewAlbums.SelectedIndex >= 0)
            {
                // Unsubscribe from SelectionChanged event temporarily
                listViewAlbums.SelectionChanged -= listViewAlbums_SelectionChanged;

                int selectedIndex = listViewAlbums.SelectedIndex;
                string oldName = listViewAlbums.SelectedItem.ToString();
                string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name", "Rename Album", oldName);
                if (!string.IsNullOrEmpty(newName) && oldName != newName)
                {
                    string oldPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", oldName + ".txt");
                    string newPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", newName + ".txt");
                    System.IO.File.Move(oldPath, newPath);

                    albums.Clear();
                    listViewAlbums.Items.Clear();

                    string albumsPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums");
                    string[] files = Directory.GetFiles(albumsPath, "*.txt");
                    foreach (string file in files)
                    {
                        albums.Add(file);
                        listViewAlbums.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file)); //take out the file extension
                    }
                }

                // Re-subscribe to SelectionChanged event after updating listViewAlbums
                listViewAlbums.SelectionChanged += listViewAlbums_SelectionChanged;
                reloadAddMenu();
                Console.WriteLine($"Renamed the \"{oldName}\" album to \"{newName}\" album at {DateTime.Now}");
            }
        }
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            string albumToRemove = listViewAlbums.SelectedItem.ToString();
            MessageBoxResult result = MessageBox.Show($"Are you sure you want to remove {albumToRemove}?", "Confirmation", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                // Unsubscribe from SelectionChanged event temporarily
                listViewAlbums.SelectionChanged -= listViewAlbums_SelectionChanged;

                string pathToRemove = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", albumToRemove + ".txt");
                System.IO.File.Delete(pathToRemove);

                albums.Clear();
                listViewAlbums.Items.Clear();

                string albumsPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums");
                string[] files = Directory.GetFiles(albumsPath, "*.txt");
                foreach (string file in files)
                {
                    albums.Add(file);
                    listViewAlbums.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file)); //take out the file extension
                }

                // Re-subscribe to SelectionChanged event after updating listViewAlbums
                listViewAlbums.SelectionChanged += listViewAlbums_SelectionChanged;
                reloadAddMenu();
                Console.WriteLine($"Removed the \"{albumToRemove}\" album at {DateTime.Now}");
            }
        }
        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {

            string oldName = listViewAlbums.SelectedItem.ToString();
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name for duplicate", "Duplicate Album", oldName);
            if (!string.IsNullOrEmpty(newName) && oldName != newName)
            {
                string oldPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", oldName + ".txt");
                string newPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", newName + ".txt");

                // Unsubscribe from SelectionChanged event temporarily
                listViewAlbums.SelectionChanged -= listViewAlbums_SelectionChanged;

                System.IO.File.Copy(oldPath, newPath);

                albums.Clear();
                listViewAlbums.Items.Clear();

                string albumsPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums");
                string[] files = Directory.GetFiles(albumsPath, "*.txt");
                foreach (string file in files)
                {
                    albums.Add(file);
                    listViewAlbums.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file)); 
                }
                // Re-subscribe to SelectionChanged event after updating listViewAlbums
                listViewAlbums.SelectionChanged += listViewAlbums_SelectionChanged;
                reloadAddMenu();
                Console.WriteLine($"Duplicated the \"{oldName}\" as the \"{newName}\" album at {DateTime.Now}");
            }
        }
        private void AlbumMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                string albumPath = menuItem.Tag.ToString();  // Get the full path from the Tag property

                // Read the album file
                string[] existingSongs = File.ReadAllLines(albumPath);

                // Check if the song already exists in the album
                if (existingSongs.Contains(selectedSong))
                {
                    MessageBox.Show("The song already exists in the album.");
                }
                else
                {
                    // Write the song path to the album file
                    using (StreamWriter sw = File.AppendText(albumPath))
                    {
                        sw.WriteLine(selectedSong);
                    }

                    if (listViewAlbums.SelectedItem != null)
                    {
                        string selectedItem = listViewAlbums.SelectedItem.ToString();
                    }
                    else
                    {
                        listViewAlbums.SelectedIndex = 0;
                    }

                    listBoxAlbumSongs.Items.Clear();
                    albumSongs.Clear();
                    albumSongs.TrimExcess();
                    string filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", albums[listViewAlbums.SelectedIndex]);
                    string[] lines = System.IO.File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        albumSongs.Add(line);
                        listBoxAlbumSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(line));
                    }
                    Console.WriteLine($"{System.IO.Path.GetFileNameWithoutExtension(selectedSong)} added to the \"{System.IO.Path.GetFileNameWithoutExtension(albumPath)}\" album at {DateTime.Now}");
                }
            }
        }
        private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Check if the albumSongTab is the currently selected tab
            if (playerTab.SelectedItem == albumSongTab)
            {
                MenuItem menuItem = sender as MenuItem;
                if (menuItem != null)
                {
                    // Get the selected album name from the album list view
                    string selectedAlbum = listViewAlbums.SelectedItem.ToString();

                    // Construct the full path to the album text file
                    string albumPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", selectedAlbum + ".txt");

                    // Read the album file into a list
                    List<string> albumSongs = new List<string>(File.ReadAllLines(albumPath));

                    string selectedSong = listBoxAlbumSongs.SelectedItem.ToString();

                    // Check if the index is valid
                    if (albumSongs.Any(songPath => System.IO.Path.GetFileNameWithoutExtension(songPath) == selectedSong))
                    {
                        // Remove the song by its name from the list
                        albumSongs.Remove(albumSongs.First(songPath => System.IO.Path.GetFileNameWithoutExtension(songPath) == selectedSong));


                        // Write the updated list back to the album file
                        File.WriteAllLines(albumPath, albumSongs.ToArray());

                        // Refresh the album song list view
                        string selectedItem = listViewAlbums.SelectedItem.ToString();
                        listBoxAlbumSongs.Items.Clear();
                        albumSongs.Clear();
                        albumSongs.TrimExcess();
                        string filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", albums[listViewAlbums.SelectedIndex]);
                        string[] lines = System.IO.File.ReadAllLines(filePath);
                        foreach (string line in lines)
                        {
                            albumSongs.Add(line);
                            listBoxAlbumSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(line));
                        }
                        Console.WriteLine($"Removed {selectedSong} from the \"{selectedAlbum}\" album at {DateTime.Now}");
                    }
                    else
                    {
                        MessageBox.Show("The selected song does not exist in the album.");
                    }
                }
            }
        }
        private void TxtSearchBar_GotFocus(object sender, EventArgs e)
        {
            if (playerTab.SelectedIndex != 1)
            {
                playerTab.SelectedIndex = 2; // Assuming the "Search Results" tab is at index 3
            }

        }
        private void TxtSearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Filter the songs based on the search text
            string searchText = txtSearchBar.Text.ToLower();

            if (playerTab.SelectedIndex == 1)
            {
                // Clear the search results
                searchResults.Clear();
                listBoxAlbumSongs.Items.Clear();

                foreach (string song in albumSongs)
                {
                    if (System.IO.Path.GetFileNameWithoutExtension(song).ToLower().Contains(searchText))
                    {
                        searchResults.Add(song);
                        listBoxAlbumSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(song));
                    }
                }

                lblNumOfRes.Content = "Results: " + searchResults.Count.ToString();
            }
            else
            {

                // Clear the search results
                searchResults.Clear();
                listBoxSearchResults.Items.Clear();

                foreach (string song in musicLib)
                {
                    if (System.IO.Path.GetFileNameWithoutExtension(song).ToLower().Contains(searchText))
                    {
                        searchResults.Add(song);
                        listBoxSearchResults.Items.Add(System.IO.Path.GetFileNameWithoutExtension(song));
                    }
                }

                lblNumOfRes.Content = "Results: " + searchResults.Count.ToString();
            }

        }
        private void listBoxMusicLib_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Clear the play queue
            listBoxSongs.Items.Clear();
            songs.Clear();
            songs.TrimExcess();
            albumSongs.Clear();
            albumSongs.TrimExcess();

            // Add all songs from the album to the play queue
            foreach (string song in musicLib)
            {
                songs.Add(song);
                listBoxSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(song));
            }

            playerTab.SelectedIndex = 3;

            // Play the selected song
            currentSongIndex = listBoxMusicLib.SelectedIndex;
            PlaySong(musicLib[currentSongIndex]);
        }
        private void listViewAlbums_SelectionChanged(object sender, SelectionChangedEventArgs e) //only gets updated once, never again when changing things
        {
            string selectedItem = listViewAlbums.SelectedItem.ToString();
            listBoxAlbumSongs.Items.Clear();
            albumSongs.Clear();
            albumSongs.TrimExcess();
            string filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", albums[listViewAlbums.SelectedIndex]);
            using (StreamReader sr = new StreamReader(filePath))
            {
                string[] lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    albumSongs.Add(line);
                    listBoxAlbumSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(line));
                }
            }
            playerTab.SelectedIndex = 1;
        }
        //if selected tab for album tab is clicked, reload the songs back into the album song list!
        private void playerTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabControl tabControl = sender as TabControl;
            TabItem selectedTab = tabControl.SelectedItem as TabItem;

            // Check if the selected tab is the one that contains the album songs
            if (selectedTab == albumSongTab)
            {
                try
                {
                    // Clear the list and trim excess capacity
                    albumSongs.Clear();
                    albumSongs.TrimExcess();

                    // Reload the songs into the albumSongs list
                    string filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", albums[listViewAlbums.SelectedIndex]);
                    string[] lines = System.IO.File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        albumSongs.Add(line);
                    }
                }
                catch (Exception ex)
                {
                    // Handle the exception here
                    //MessageBox.Show("An error occurred: " + ex.Message);
                }
            }
        }
        private void listBoxAlbumSongs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Clear the play queue
            listBoxSongs.Items.Clear();
            songs.Clear();
            songs.TrimExcess();

            // Add all songs from the album to the play queue
            foreach (string song in albumSongs)
            {
                songs.Add(song);
                listBoxSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(song));
            }

            playerTab.SelectedIndex = 3;

            // Play the selected song
            currentSongIndex = listBoxAlbumSongs.SelectedIndex;
            PlaySong(albumSongs[currentSongIndex]);
        }

        public void RestoreOriginalList(string folderPath) // temporary implementation when refreshing and initalizing list boxes
        {
            listBoxSongs.Items.Clear();
            listBoxMusicLib.Items.Clear();
            songs.Clear();
            songs.TrimExcess();

            foreach (string file in Directory.EnumerateFiles(folderPath, "*.*"))
            {
                // Check if the file is a song
                if (System.IO.Path.GetExtension(file) == ".mp3" || System.IO.Path.GetExtension(file) == ".wav" || System.IO.Path.GetExtension(file) == ".ogg")
                {
                    // Add song to the list and ListBox
                    songs.Add(file);
                    listBoxSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file));

                    musicLib.Add(file);
                    listBoxMusicLib.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                }
            }
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update the label with the current time of the song
            lblCurrentTime.Content = mediaPlayer.Position.ToString(@"mm\:ss");

            // Update the slider value to the current position of the song
            if (!isDragging)
            {
                seekSlider.Value = mediaPlayer.Position.TotalSeconds;
            }
        }
        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Update the media position when the slider value changes
            if (!isDragging && mediaPlayer.CanPause)
            {
                mediaPlayer.Position = TimeSpan.FromSeconds(seekSlider.Value);
            }
        }
        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            // Pause the media when the user starts dragging the thumb
            isDragging = true;
            mediaPlayer.Pause();
        }
        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            // Update the media position when the user stops dragging the thumb
            isDragging = false;
            mediaPlayer.Position = TimeSpan.FromSeconds(seekSlider.Value);
            Console.WriteLine($"Dragged slider to {TimeSpan.FromSeconds(seekSlider.Value).ToString()} at {DateTime.Now}");
            // Resume playing if the song was playing before the user started dragging
            if (wasPlaying == true)
                mediaPlayer.Play();
        }
        private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // This method allows the user to click anywhere on the slider to change the value
            var slider = (Slider)sender;
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            if (track == null) return;

            // Calculate the value based on the mouse position
            var value = track.ValueFromPoint(e.GetPosition(track));

            // Set the Slider value and position the Thumb
            slider.Value = value;
            // Begin a drag operation on the Thumb
            var thumb = track.Thumb;
            if (thumb != null)
            {
                // Capture the mouse and start the drag operation
                thumb.CaptureMouse();
                var args = new DragStartedEventArgs(e.GetPosition(thumb).X, e.GetPosition(thumb).Y);
                args.RoutedEvent = Thumb.DragStartedEvent;
                thumb.RaiseEvent(args);
            }

        }
        public class TextBoxStreamWriter : TextWriter
        {
            // TextBox to which the console output will be redirected
            System.Windows.Controls.TextBox _output = null;
            StreamWriter _fileWriter = null;

            public TextBoxStreamWriter(System.Windows.Controls.TextBox output, string filePath)
            {
                _output = output;
                _fileWriter = new StreamWriter(filePath, true); // true to append data to the file
            }

            public override void Write(char value)
            {
                // Append the value to the TextBox
                base.Write(value);
                _output.AppendText(value.ToString());

                // Write the value to the file
                _fileWriter.Write(value);
                _fileWriter.Flush(); // Flush to ensure data is written to the file
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    // Close the StreamWriter if it's not null
                    _fileWriter?.Close();
                }
            }

            public override Encoding Encoding
            {
                // Use UTF8 encoding
                get { return System.Text.Encoding.UTF8; }
            }
        }
        private void btn_play_Click(object sender, RoutedEventArgs e)
        {
            // Toggle between Play and Pause when the Play button is clicked
            Button button = sender as Button;

            if (wasPlaying)
            {
                PauseSong(songs[currentSongIndex]);
                wasPlaying = false;
                button.Content = "Play";
            }
            else
            {
                ResumeSong(songs[currentSongIndex]);
                wasPlaying = true;
                button.Content = "Pause";
            }
        }
        private void btn_shuffle_Click(object sender, RoutedEventArgs e)
        {
            // Assume currentSong is the song currently playing
            string currentSong = songs[currentSongIndex];

            // Remove the current song from the list
            songs.Remove(currentSong);

            // Shuffle the remaining songs
            songs = songs.OrderBy(x => random.Next()).ToList();

            // Insert the current song at the beginning of the list
            songs.Insert(0, currentSong);

            listBoxSongs.Items.Clear();
            foreach (string song in songs)
            {
                listBoxSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(song));
            }

            // Set the selected item to the current song
            listBoxSongs.SelectedItem = currentSong;

            currentSongIndex = 0;
            Console.WriteLine($"Shuffled songs at {DateTime.Now}");
        }


        private void btn_next_Click(object sender, RoutedEventArgs e)
        {
            // Play the next song when the Next button is clicked
            if (wasPlaying == true && loopBool == true)
            {
                PlaySong(songs[currentSongIndex]);
                Console.WriteLine($"Looping song {System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} at {DateTime.Now}");
            }
            else if (wasPlaying == true && loopBool == false)
            {
                currentSongIndex = (currentSongIndex + 1) % songs.Count;
                PlaySong(songs[currentSongIndex]);
                Console.WriteLine($"Playing next song {System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} at {DateTime.Now}");
            }
            else
            {
                Console.WriteLine($"{songs[currentSongIndex - 1]} played for {mediaPlayer.Position.TotalSeconds} seconds at {DateTime.Now}.");
                currentSongIndex = (currentSongIndex + 1) % songs.Count;
                if (mediaPlayer.Source == null || mediaPlayer.Source.AbsolutePath != songs[currentSongIndex])
                {
                    mediaPlayer.Open(new Uri(songs[currentSongIndex]));
                }
                Console.WriteLine($"Queued next song {System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} at {DateTime.Now}");

            }
        }

        private void btn_prev_Click(object sender, RoutedEventArgs e)
        {
            // Check if the song has been playing for more than 5 seconds
            if (wasPlaying == true && mediaPlayer.Position > TimeSpan.FromSeconds(5))
            {
                // If true, reset the timer to zero and restart the song
                mediaPlayer.Position = TimeSpan.Zero;
                Console.WriteLine($"Replaying {System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} at {DateTime.Now}");
            }
            else
            {
                // If the song was paused or has been playing for less than 5 seconds, go to the previous song
                currentSongIndex = (currentSongIndex - 1 + songs.Count) % songs.Count;

                // If the song was playing, play the previous song
                if (wasPlaying == true)
                {
                    PlaySong(songs[currentSongIndex]);
                    Console.WriteLine($"Playing previous song {System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} at {DateTime.Now}.");
                }
                // If the song was paused, queue the previous song but don't play it
                else
                {
                    Console.WriteLine($"{songs[currentSongIndex - 1]} played for {mediaPlayer.Position.TotalSeconds} seconds at {DateTime.Now}.");

                    if (mediaPlayer.Source == null || mediaPlayer.Source.AbsolutePath != songs[currentSongIndex])
                    {
                        mediaPlayer.Open(new Uri(songs[currentSongIndex]));
                    }
                    Console.WriteLine($"Queued previous song {System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} at {DateTime.Now}");
                }
            }
        }


        private void listBoxSongs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Play the selected song when a song in the ListBox is double-clicked
            currentSongIndex = listBoxSongs.SelectedIndex;
            PlaySong(songs[currentSongIndex]);
        }

        private void listViewAlbums_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void MediaPlayer_MediaOpened(object sender, EventArgs e)
        {
            // Check if the song is already in the songs table, if not, insert it
            string upsertSongQuery = @"
                INSERT INTO songs (name, length)
                VALUES (@name, @length)
                ON CONFLICT(name) DO UPDATE SET length = @length";

            connection.Open();

            using (var command = new SQLiteCommand(upsertSongQuery, connection))
            {
                command.Parameters.AddWithValue("@name", System.IO.Path.GetFileNameWithoutExtension(mediaPlayer.Source.AbsolutePath));
                command.Parameters.AddWithValue("@length", mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds);

                command.ExecuteNonQuery();
            }

            connection.Close();
        }

        private async void PlaySong(string songPath)
        {
            // Stop the timer
            timer.Stop();

            // Log the last song play to the database
            if (mediaPlayer.Source != null)
            {
                double lastSongLength = mediaPlayer.Position.TotalSeconds;

                Console.WriteLine($"{songName} played for {lastSongLength} seconds at {DateTime.Now}.");

                // Implement the database push here
                string insertSongPlayQuery = @"
                    INSERT INTO song_plays (song_id, play_time, play_length)
                    VALUES ((SELECT id FROM songs WHERE name = @name), @play_time, @play_length)";

                connection.Open();

                using (var command = new SQLiteCommand(insertSongPlayQuery, connection))
                {
                    command.Parameters.AddWithValue("@name", System.IO.Path.GetFileNameWithoutExtension(mediaPlayer.Source.AbsolutePath));
                    command.Parameters.AddWithValue("@play_time", DateTime.Now.ToString("o"));
                    command.Parameters.AddWithValue("@play_length", lastSongLength);

                    command.ExecuteNonQuery();
                }

                connection.Close();
            }


            // Close the current media
            mediaPlayer.Close();

            // Load and play the song asynchronously
            await Task.Run(() =>
            {
                UpdateUI(() =>
                {
                    // Play the song at the given path
                    if (mediaPlayer.Source == null || mediaPlayer.Source.AbsolutePath != songPath)
                    {
                        mediaPlayer.Open(new Uri(songPath));
                    }
                });
            });

            UpdateUI(() =>
            {
                seekSlider.Value = 0;
                mediaPlayer.Play();
                wasPlaying = true;
                btn_play.Content = "Pause";
                Console.WriteLine($"{System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} loaded at {DateTime.Now}");
                songName = System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex]);
                HighlightSongInListBox(currentSongIndex);

                // Reset and start the timer
                timer.Start();
            });

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void PauseSong(string songPath)
        {
            // Pause the song at the given path
            mediaPlayer.Pause();
            wasPlaying = false;
            btn_play.Content = "Play";
            Console.WriteLine($"{System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} paused at {DateTime.Now}");

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void ResumeSong(string songPath)
        {
            if (!wasPlaying)
            {
                mediaPlayer.Play();
                wasPlaying = true;
                btn_play.Content = "Pause";
                Console.WriteLine($"{System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} resumed at {DateTime.Now}");

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private void btn_shufflePlay_Click(object sender, RoutedEventArgs e)
        {
            TabControl tabControl = this.FindName("playerTab") as TabControl;
            TabItem selectedTab = tabControl.SelectedItem as TabItem;

            // Check if the selected tab is the one that contains the album songs
            if (selectedTab == albumSongTab)
            {
                songs.Clear();
                foreach (string song in albumSongs)
                {
                    songs.Add(song);
                }
            }
            if (selectedTab == musicLibTab) 
            {
                songs.Clear();
                foreach (string song in musicLib)
                {
                    songs.Add(song);
                }
            }

            // Shuffle the songs and play the first one when the Shuffle Play button is clicked
            songs = songs.OrderBy(x => random.Next()).ToList();
            listBoxSongs.Items.Clear();
            foreach (string song in songs)
            {
                listBoxSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(song));
            }
            playerTab.SelectedIndex = 3;
            currentSongIndex = 0;
            PlaySong(songs[currentSongIndex]);
            Console.WriteLine($"Shuffled playlist and played {System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} at {DateTime.Now}");
        }

        private void Media_Ended(object sender, EventArgs e)
        {
            // Play the next song when the current song ends
            Console.WriteLine($"{System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex])} ended at {DateTime.Now}");
            btn_next_Click(this, new RoutedEventArgs());
        }

        // Compresses album art to a single pixel to extract the most prominent colour and uses it for the background colour
        private System.Windows.Media.Color GetMostFrequentColor(BitmapImage bitmapImage)
        {
            var smallBitmap = new TransformedBitmap(bitmapImage, new ScaleTransform(1.0 / bitmapImage.PixelWidth, 1.0 / bitmapImage.PixelHeight, 0, 0));
            var pixels = new byte[4]; // 1 pixel with 4 bytes (RGBA)
            smallBitmap.CopyPixels(pixels, 4, 0);
            return System.Windows.Media.Color.FromRgb(pixels[2], pixels[1], pixels[0]); // Pixels are in BGRA order
        }
        private void Media_Opened(object sender, EventArgs e)
        {
            // Update the UI when the media is opened
            lblSongName.Content = System.IO.Path.GetFileNameWithoutExtension(songs[currentSongIndex]);
            lblSongLength.Content = mediaPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
            timer.Start();
            seekSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;

            // Create a TagLib.File object from the song file path
            var file = TagLib.File.Create(songs[currentSongIndex]);

            var pictures = file.Tag.Pictures;
            if (pictures.Length > 0)
            {
                var bin = (byte[])(pictures[0].Data.Data);
                using (var stream = new MemoryStream(bin))
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    // Set the source for the Image control
                    albumArt.Source = bitmapImage;

                    // Adjust the properties of the Image control
                    //albumArt.Width = bitmapImage.PixelWidth;
                    //albumArt.Height = bitmapImage.PixelHeight;
                    albumArt.Stretch = Stretch.Uniform;

                    // Get the most frequent color
                    System.Windows.Media.Color mostFrequentColor = GetMostFrequentColor(bitmapImage);

                    // Ensure the Background is a modifiable SolidColorBrush
                    this.Background = new SolidColorBrush(((SolidColorBrush)this.Background).Color);

                    // Create a ColorAnimation
                    ColorAnimation colorAnimation = new ColorAnimation
                    {
                        From = ((SolidColorBrush)this.Background).Color,
                        To = mostFrequentColor,
                        Duration = new Duration(TimeSpan.FromSeconds(1)),
                    };

                    // Apply the animation to the Background property of the window
                    this.Background.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
                }
            }
            else
            {
                // Clear the Image control if there's no image in the metadata
                albumArt.Source = null;
            }
        }
        private void btn_seekBack_Click(object sender, RoutedEventArgs e)
        {
            // Seek 10 seconds forward
            mediaPlayer.Position = mediaPlayer.Position.Subtract(TimeSpan.FromSeconds(5));
            Console.WriteLine($"Seek backwards 5 seconds at {DateTime.Now}");
        }

        private void btn_seekForw_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Position = mediaPlayer.Position.Add(TimeSpan.FromSeconds(5));
            Console.WriteLine($"Seek forwards 5 seconds at {DateTime.Now}");
        }
        private void HighlightSongInListBox(int songIndex)
        {
            // Unhighlight all songs
            for (int i = 0; i < listBoxSongs.Items.Count; i++)
            {
                ListBoxItem item = (ListBoxItem)listBoxSongs.ItemContainerGenerator.ContainerFromIndex(i);
                if (item != null)
                {
                    item.Background = System.Windows.Media.Brushes.White;
                }
            }

            // Highlight the current song
            listBoxSongs.ScrollIntoView(listBoxSongs.Items[songIndex]);
            ListBoxItem currentItem = (ListBoxItem)listBoxSongs.ItemContainerGenerator.ContainerFromIndex(songIndex);
            if (currentItem != null)
            {
                currentItem.Background = System.Windows.Media.Brushes.LightBlue;
            }
        }

        // Method to open the song in Audacity for quick song edits
        private void OpenInAudacity_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected song
            string songName = listBoxSongs.SelectedItem as string;

            if (songName != null)
            {
                int songIndex = songs.FindIndex(songPath => songPath.Contains(songName));
                string songPath = songs[songIndex];
                PauseSong(songPath);
                // Specify the location of the Audacity executable
                string audacityPath = @"C:\Program Files\Audacity\audacity.exe";
                Console.WriteLine($"Opened {songName} in audacity at {DateTime.Now}");

                // Create a new process
                Process process = new Process();
                process.StartInfo.FileName = audacityPath;
                process.StartInfo.Arguments = $"\"{songPath}\"";
                process.Start();
            }
        }

        private void AddSong_Click(object sender, RoutedEventArgs e)
        {
            // empty
        }

        private void listBoxSearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Clear the play queue
            listBoxSongs.Items.Clear();
            songs.Clear();
            songs.TrimExcess();

            // Add the selected song to the play queue
            songs.Add(searchResults[listBoxSearchResults.SelectedIndex]);
            listBoxSongs.Items.Add(System.IO.Path.GetFileNameWithoutExtension(searchResults[listBoxSearchResults.SelectedIndex]));

            playerTab.SelectedIndex = 2; // Switch to the "Play Queue" tab

            // Play the selected song
            currentSongIndex = 0;
            PlaySong(songs[currentSongIndex]);
        }

        private void ListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox != null && listBox.SelectedIndex >= 0)
            {
                // Get the index of the selected item
                int index = listBox.SelectedIndex;

                // Check which tab is currently selected
                switch (playerTab.SelectedItem)
                {
                    case TabItem item when item == musicLibTab:
                        // Use this index to get the file path from the musicLibList
                        selectedSong = musicLib[index];
                        break;

                    case TabItem item when item == albumSongTab:
                        // Use this index to get the file path from the albumSongList
                        reloadAddMenu();
                        selectedSong = albumSongs[index];
                        break;

                    case TabItem item when item == searchTab:
                        // Use this index to get the file path from the searchList
                        selectedSong = searchResults[index];
                        break;

                    case TabItem item when item == playTab:
                        // Use this index to get the file path from the playList
                        selectedSong = songs[index];
                        break;
                }
            }
        }

        private void listBoxAlbumSongs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // empty
        }

        private void listBoxSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // empty
        }

        private void listBoxSongs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // empty
        }

        private void listBoxMusicLib_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // empty
        }

        private void listViewAlbums_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border && listViewAlbums.SelectedItem == null)
            {
                string newAlbumName = Microsoft.VisualBasic.Interaction.InputBox("Enter new album name", "New Album");
                if (!string.IsNullOrEmpty(newAlbumName))
                {
                    string newAlbumPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Albums", newAlbumName, ".txt");
                    System.IO.File.Create(newAlbumPath).Close();
                    listViewAlbums.Items.Add(newAlbumName);
                    Console.WriteLine($"Created the \"{newAlbumName}\" album at {DateTime.Now}");
                }
            }
        }

        private void CopySongPath_Click(object sender, RoutedEventArgs e)
        {
            // Get the context menu and the item it's associated with
            MenuItem menuItem = sender as MenuItem;
            ContextMenu cm = menuItem.Parent as ContextMenu;
            ListBoxItem item = cm.PlacementTarget as ListBoxItem;

            // Get the song name
            string selectedSong = item.Content.ToString();

            // Determine which list box was right-clicked
            ListBox parentListBox = ItemsControl.ItemsControlFromItemContainer(item) as ListBox;

            // Use the corresponding song list
            List<string> songList;
            switch (parentListBox.Name)
            {
                case "listBoxMusicLib":
                    songList = musicLib;
                    break;
                case "listBoxAlbumSongs":
                    songList = albumSongs;
                    break;
                case "listBoxSearchResults":
                    songList = searchResults;
                    break;
                case "listBoxSongs":
                    songList = songs;
                    break;
                default:
                    throw new Exception("Unknown list box.");
            }

            // Find the song path in the song list
            string selectedSongPath = songList.FirstOrDefault(songPath => System.IO.Path.GetFileNameWithoutExtension(songPath) == selectedSong);

            // Check if the song path was found
            if (!string.IsNullOrEmpty(selectedSongPath))
            {
                // Copy the song path to the clipboard
                Clipboard.SetText(selectedSongPath);
                Console.WriteLine($"Copied {selectedSongPath} to clipboard at {DateTime.Now}");
            }
        }
        private void CopySongName_Click(object sender, RoutedEventArgs e)
        {
            // Get the context menu and the item it's associated with
            MenuItem menuItem = sender as MenuItem;
            ContextMenu cm = menuItem.Parent as ContextMenu;
            ListBoxItem item = cm.PlacementTarget as ListBoxItem;

            // Get the song name
            string selectedSong = item.Content.ToString();

            // Copy the song name to the clipboard
            Clipboard.SetText(selectedSong);
            Console.WriteLine($"Copied {selectedSong} to clipboard at {DateTime.Now}");
        }
        private void btn_loop_Click(object sender, RoutedEventArgs e)
        {
            if (loopBool == false)
            {
                loopBool = true;
                Console.WriteLine($"Enabled loop at {DateTime.Now}");
            }
            else if (loopBool == true)
            {
                loopBool = false;
                Console.WriteLine($"Disabled loop at {DateTime.Now}");
            }
        }

    }
}
