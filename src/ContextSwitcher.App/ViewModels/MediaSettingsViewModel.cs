using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// Profile Setup's Media section. Apple Music's playlists are offered as a dropdown read from Music
/// itself; Spotify will not list playlists to a script, so it takes a pasted Share link instead of
/// a hand-built <c>spotify:</c> URI.
/// </summary>
public sealed class MediaSettingsViewModel : ViewModelBase
{
    private readonly ISystemCatalog catalog;

    private Choice<MediaPlayerKind> selectedPlayer;
    private string playlist;
    private bool autoPlay;
    private IReadOnlyList<string> musicPlaylists = [];
    private bool musicPlaylistsLoaded;
    private bool isLoadingMusicPlaylists;
    private string musicStatus = string.Empty;

    public MediaSettingsViewModel(ISystemCatalog catalog, MediaConfig media)
    {
        this.catalog = catalog;
        this.selectedPlayer = this.Players.First(choice => choice.Value == media.Player);
        this.playlist = media.Playlist;
        this.autoPlay = media.AutoPlay;
        this.RefreshMusicPlaylists([]);

        this.LoadMusicPlaylistsCommand = new RelayCommand(() => _ = this.LoadMusicPlaylistsAsync(startMusicIfNeeded: true));

        if (this.IsAppleMusic)
        {
            // Only if Music is already open: reading playlists starts it otherwise.
            _ = this.LoadMusicPlaylistsAsync(startMusicIfNeeded: false);
        }
    }

    public IReadOnlyList<Choice<MediaPlayerKind>> Players { get; } =
    [
        new(MediaPlayerKind.None, "None"),
        new(MediaPlayerKind.AppleMusic, "Apple Music"),
        new(MediaPlayerKind.Spotify, "Spotify")
    ];

    public Choice<MediaPlayerKind> SelectedPlayer
    {
        get => this.selectedPlayer;
        set
        {
            if (value is null || !this.SetProperty(ref this.selectedPlayer, value))
            {
                return;
            }

            // A Spotify link means nothing to Music and a Music playlist name nothing to Spotify.
            this.Playlist = string.Empty;
            this.RefreshMusicPlaylists(this.musicPlaylistsLoaded ? this.musicPlaylists : []);
            this.OnPropertyChanged(nameof(this.Player));
            this.OnPropertyChanged(nameof(this.HasPlayer));
            this.OnPropertyChanged(nameof(this.IsAppleMusic));
            this.OnPropertyChanged(nameof(this.IsSpotify));
            this.OnPropertyChanged(nameof(this.CanLoadMusicPlaylists));

            if (this.IsAppleMusic && !this.musicPlaylistsLoaded)
            {
                _ = this.LoadMusicPlaylistsAsync(startMusicIfNeeded: false);
            }
        }
    }

    public MediaPlayerKind Player => this.SelectedPlayer.Value;

    public bool HasPlayer => this.Player != MediaPlayerKind.None;

    public bool IsAppleMusic => this.Player == MediaPlayerKind.AppleMusic;

    public bool IsSpotify => this.Player == MediaPlayerKind.Spotify;

    /// <summary>
    /// A Music playlist name, or for Spotify whatever was pasted - turned into a <c>spotify:</c>
    /// URI when saved.
    /// </summary>
    public string Playlist
    {
        get => this.playlist;
        set
        {
            // The playlist dropdown writes null while its list is being replaced; that is not a choice.
            if (value is not null)
            {
                this.SetProperty(ref this.playlist, value);
            }
        }
    }

    public bool AutoPlay
    {
        get => this.autoPlay;
        set => this.SetProperty(ref this.autoPlay, value);
    }

    /// <summary>The Apple Music dropdown: Music's playlists, plus the saved one if Music no longer has it.</summary>
    public IReadOnlyList<string> MusicPlaylists
    {
        get => this.musicPlaylists;
        private set => this.SetProperty(ref this.musicPlaylists, value);
    }

    /// <summary>Music was not open, so its playlists have not been read; the user can ask for them.</summary>
    public bool CanLoadMusicPlaylists => this.IsAppleMusic && !this.musicPlaylistsLoaded && !this.IsLoadingMusicPlaylists;

    public bool IsLoadingMusicPlaylists
    {
        get => this.isLoadingMusicPlaylists;
        private set
        {
            if (this.SetProperty(ref this.isLoadingMusicPlaylists, value))
            {
                this.OnPropertyChanged(nameof(this.CanLoadMusicPlaylists));
            }
        }
    }

    /// <summary>Why the playlist list is empty, when it is; empty otherwise.</summary>
    public string MusicStatus
    {
        get => this.musicStatus;
        private set
        {
            if (this.SetProperty(ref this.musicStatus, value))
            {
                this.OnPropertyChanged(nameof(this.HasMusicStatus));
            }
        }
    }

    public bool HasMusicStatus => this.MusicStatus.Length > 0;

    /// <summary>Opens Music if needed and reads its playlists.</summary>
    public RelayCommand LoadMusicPlaylistsCommand { get; }

    public MediaConfig ToConfig() => new()
    {
        Player = this.Player,
        Playlist = this.IsSpotify ? SpotifyLink.ToUri(this.Playlist) : this.Playlist.Trim(),
        AutoPlay = this.AutoPlay
    };

    /// <summary>Reads Music's playlists. Never throws.</summary>
    public async Task LoadMusicPlaylistsAsync(bool startMusicIfNeeded)
    {
        this.IsLoadingMusicPlaylists = true;
        IReadOnlyList<string>? playlists;
        try
        {
            playlists = await this.catalog.GetMusicPlaylistsAsync(startMusicIfNeeded, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception)
        {
            playlists = null;
        }
        finally
        {
            this.IsLoadingMusicPlaylists = false;
        }

        if (playlists is not null)
        {
            this.musicPlaylistsLoaded = true;
            this.MusicStatus = playlists.Count == 0 ? "Music has no playlists yet." : string.Empty;
            this.RefreshMusicPlaylists(playlists);
        }
        else if (startMusicIfNeeded)
        {
            this.MusicStatus = "Couldn't read playlists from Music. Allow Context Switcher to control Music in System Settings → Privacy & Security → Automation.";
        }

        this.OnPropertyChanged(nameof(this.CanLoadMusicPlaylists));
    }

    private void RefreshMusicPlaylists(IReadOnlyList<string> playlists)
    {
        // Keeps the chosen entry when the list is replaced: the dropdown would otherwise show blank.
        string chosen = this.Playlist;
        this.MusicPlaylists = this.IsAppleMusic && chosen.Length > 0 && !playlists.Contains(chosen)
            ? [chosen, .. playlists]
            : playlists;
        this.OnPropertyChanged(nameof(this.Playlist));
    }
}
