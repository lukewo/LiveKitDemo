using System;
using System.Threading;
using System.Threading.Tasks;
using IO.Livekit.Android;
using IO.Livekit.Android.Events;
using IO.Livekit.Android.Room.Participant;
using LkRoom = IO.Livekit.Android.Room.Room;

namespace LiveKit.Xamarin
{
    /// <summary>
    /// Task-based wrappers over LiveKit's suspend functions, plus event subscription.
    /// </summary>
    public static class LiveKitExtensions
    {
        /// <summary>Creates a Room with default options (LiveKit.create(context)).</summary>
        public static LkRoom CreateRoom(Android.Content.Context context)
            => global::IO.Livekit.Android.LiveKit.Instance.Create(context.ApplicationContext, new RoomOptions(), new LiveKitOverrides());

        /// <summary>suspend fun Room.connect(url, token, options = ConnectOptions())</summary>
        public static Task ConnectAsync(this LkRoom room, string url, string token,
            ConnectOptions options = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("Server URL is required.", nameof(url));
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Access token is required.", nameof(token));
            options = options ?? new ConnectOptions();
            return Suspend.InvokeUnitAsync(c => room.Connect(url, token, options, c), cancellationToken);
        }

        /// <summary>suspend fun LocalParticipant.setCameraEnabled(enabled): Boolean</summary>
        public static async Task<bool> SetCameraEnabledAsync(this LocalParticipant participant, bool enabled)
        {
            var result = await Suspend.InvokeAsync(c => participant.SetCameraEnabled(enabled, c));
            return result is Java.Lang.Boolean b && b.BooleanValue();
        }

        /// <summary>suspend fun LocalParticipant.setMicrophoneEnabled(enabled): Boolean</summary>
        public static async Task<bool> SetMicrophoneEnabledAsync(this LocalParticipant participant, bool enabled)
        {
            var result = await Suspend.InvokeAsync(c => participant.SetMicrophoneEnabled(enabled, c));
            return result is Java.Lang.Boolean b && b.BooleanValue();
        }

        /// <summary>
        /// Subscribes to room.events. The callback runs on a LiveKit background thread;
        /// marshal to the UI thread before touching views. Dispose to unsubscribe.
        /// </summary>
        public static FlowSubscription SubscribeEvents(this LkRoom room, Action<RoomEvent> onEvent)
            => FlowSubscription.Collect(room.Events.Events, value =>
            {
                if (value is RoomEvent e) onEvent(e);
            });
    }
}
