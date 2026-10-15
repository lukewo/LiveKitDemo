using System;
using System.Threading.Tasks;
using LiveKitDemo.Services;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace LiveKitDemo
{
    public partial class MainPage : ContentPage
    {
        const string UrlPreferenceKey = "livekit_server_url";

        readonly ILiveKitService liveKit;

        // Set while we push service state into the switches, so their Toggled
        // handlers don't treat it as a user action.
        bool syncingSwitches;
        bool busy;

        public MainPage()
        {
            InitializeComponent();

            liveKit = DependencyService.Get<ILiveKitService>();
            liveKit.StateChanged += (s, e) => UpdateUi();
            liveKit.ErrorOccurred += async (s, message) => await DisplayAlert("LiveKit", message, "OK");

            UrlEntry.Text = Preferences.Get(UrlPreferenceKey, DemoConfig.ServerUrl);
            RoomEntry.Text = Preferences.Get(RoomPreferenceKey, DemoConfig.DefaultRoom);
            NameEntry.Text = Preferences.Get(NamePreferenceKey, DefaultName());
            UpdateUi();
        }

        const string RoomPreferenceKey = "livekit_room";
        const string NamePreferenceKey = "livekit_name";

        static bool CanGenerateTokens => !string.IsNullOrWhiteSpace(DemoConfig.ApiSecret);

        /// <summary>e.g. "Pixel 7-4821", unique enough that two devices don't collide.</summary>
        static string DefaultName()
            => $"{DeviceInfo.Model}-{new Random().Next(1000, 9999)}";

        async void OnConnectClicked(object sender, EventArgs e)
        {
            string url = UrlEntry.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                await DisplayAlert("LiveKit", "Enter a server URL.", "OK");
                return;
            }
            if (!url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("LiveKit", "The server URL must start with wss:// (or ws:// for a local dev server).", "OK");
                return;
            }

            string token = TokenEntry.Text?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                token = await TryGenerateToken(NameEntry.Text);
                if (token == null) return;
            }

            Preferences.Set(UrlPreferenceKey, url);
            Preferences.Set(RoomPreferenceKey, RoomEntry.Text?.Trim() ?? "");
            Preferences.Set(NamePreferenceKey, NameEntry.Text?.Trim() ?? "");
            await RunBusy(() => liveKit.ConnectAsync(url, token), "Couldn't connect");
        }

        /// <summary>
        /// Copies a token for a different identity in the same room, for joining
        /// from https://meet.livekit.io (Custom tab) as the other side of the call.
        /// </summary>
        async void OnCopyGuestTokenClicked(object sender, EventArgs e)
        {
            string token = await TryGenerateToken("browser-" + new Random().Next(1000, 9999));
            if (token == null) return;

            await Clipboard.SetTextAsync(token);
            await DisplayAlert("Token copied",
                $"Open meet.livekit.io → Custom, use\n\n{UrlEntry.Text?.Trim()}\n\nand paste the token. " +
                $"It joins room \"{RoomEntry.Text?.Trim()}\".", "OK");
        }

        /// <summary>Generates a token from DemoConfig's key/secret, or explains why it can't.</summary>
        async Task<string> TryGenerateToken(string identity)
        {
            string room = RoomEntry.Text?.Trim();
            identity = identity?.Trim();

            if (!CanGenerateTokens)
            {
                await DisplayAlert("LiveKit",
                    "No access token entered, and DemoConfig.ApiSecret is empty. " +
                    "Paste a token, or set the API secret in DemoConfig.cs.", "OK");
                return null;
            }
            if (string.IsNullOrEmpty(room) || string.IsNullOrEmpty(identity))
            {
                await DisplayAlert("LiveKit", "Enter a room and your name.", "OK");
                return null;
            }

            return LiveKitTokenGenerator.Create(DemoConfig.ApiKey, DemoConfig.ApiSecret,
                room, identity, identity, DemoConfig.TokenLifetime);
        }

        async void OnDisconnectClicked(object sender, EventArgs e)
            => await RunBusy(() => liveKit.DisconnectAsync(), "Couldn't disconnect");

        async void OnMicToggled(object sender, ToggledEventArgs e)
        {
            if (syncingSwitches) return;
            if (e.Value && !await EnsurePermission<Permissions.Microphone>("microphone"))
            {
                UpdateUi();
                return;
            }
            await RunBusy(() => liveKit.SetMicrophoneEnabledAsync(e.Value), "Couldn't change the microphone");
        }

        async void OnCameraToggled(object sender, ToggledEventArgs e)
        {
            if (syncingSwitches) return;
            if (e.Value && !await EnsurePermission<Permissions.Camera>("camera"))
            {
                UpdateUi();
                return;
            }
            await RunBusy(() => liveKit.SetCameraEnabledAsync(e.Value), "Couldn't change the camera");
        }

        async Task<bool> EnsurePermission<T>(string what) where T : Permissions.BasePermission, new()
        {
            var status = await Permissions.CheckStatusAsync<T>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<T>();
            if (status == PermissionStatus.Granted)
                return true;

            await DisplayAlert("Permission needed", $"Allow {what} access in Settings to use it in a call.", "OK");
            return false;
        }

        async Task RunBusy(Func<Task> action, string failureTitle)
        {
            busy = true;
            UpdateUi();
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                await DisplayAlert(failureTitle, ex.Message, "OK");
            }
            finally
            {
                busy = false;
                UpdateUi();
            }
        }

        void UpdateUi()
        {
            var status = liveKit.Status;
            bool connected = status == ConnectionStatus.Connected;
            bool idle = status == ConnectionStatus.Disconnected;

            StatusLabel.Text = status.ToString();
            ConnectButton.IsEnabled = idle && !busy;
            DisconnectButton.IsEnabled = !idle && !busy;
            UrlEntry.IsEnabled = TokenEntry.IsEnabled = RoomEntry.IsEnabled = NameEntry.IsEnabled = idle && !busy;
            CopyGuestTokenButton.IsEnabled = CanGenerateTokens;

            syncingSwitches = true;
            MicSwitch.IsToggled = liveKit.IsMicrophoneEnabled;
            CameraSwitch.IsToggled = liveKit.IsCameraEnabled;
            MicSwitch.IsEnabled = CameraSwitch.IsEnabled = connected && !busy;
            syncingSwitches = false;

            var people = liveKit.Participants;
            ParticipantsLabel.Text = people.Count == 0
                ? "Participants: none"
                : $"Participants ({people.Count}): {string.Join(", ", people)}";

            UpdateVideoPlaceholders(status);
        }

        /// <summary>Shows a video feed when it has frames, otherwise a placeholder.</summary>
        void UpdateVideoPlaceholders(ConnectionStatus status)
        {
            // ---- your preview (bottom-right)
            bool hasLocal = liveKit.HasLocalVideo;
            LocalVideo.IsVisible = hasLocal;
            LocalPlaceholder.IsVisible = !hasLocal;
            LocalInitialLabel.Text = Initial(liveKit.LocalParticipantName) ?? "You";

            // ---- remote (main area)
            bool hasRemote = liveKit.HasRemoteVideo;
            RemoteVideo.IsVisible = hasRemote;
            RemotePlaceholder.IsVisible = !hasRemote;
            if (hasRemote) return;

            string name = liveKit.RemoteVideoParticipantName;   // set when their video is muted
            string title, detail = null;

            switch (status)
            {
                case ConnectionStatus.Disconnected:
                    title = "Not connected";
                    detail = CanGenerateTokens
                        ? "Tap Connect to join the room."
                        : "Enter an access token, then tap Connect.";
                    break;
                case ConnectionStatus.Connecting:
                    title = "Connecting…";
                    break;
                case ConnectionStatus.Reconnecting:
                    title = "Reconnecting…";
                    detail = "Video will resume when the connection is back.";
                    break;
                default:
                    int others = liveKit.RemoteParticipantCount;
                    if (others == 0)
                    {
                        title = "Waiting for others to join";
                        detail = "You're the only one here.";
                    }
                    else if (name != null)
                    {
                        title = name;
                        detail = "Camera is off";
                    }
                    else
                    {
                        title = others == 1 ? "1 other participant" : $"{others} other participants";
                        detail = "No one is sharing video yet.";
                    }
                    break;
            }

            RemoteInitialLabel.Text = Initial(name);
            RemoteAvatar.IsVisible = name != null;
            RemotePlaceholderTitle.Text = title;
            RemotePlaceholderDetail.Text = detail;
            RemotePlaceholderDetail.IsVisible = detail != null;
        }

        static string Initial(string name)
            => string.IsNullOrWhiteSpace(name) ? null : name.Trim().Substring(0, 1).ToUpperInvariant();
    }
}
