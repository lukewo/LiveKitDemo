using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiveKit.Xamarin;
using LiveKitDemo.Controls;
using LiveKitDemo.Droid.LiveKit;
using LiveKitDemo.Services;
using Xamarin.Essentials;
using SurfaceViewRenderer = Livekit.Org.Webrtc.SurfaceViewRenderer;
using VideoSource = LiveKitDemo.Controls.VideoSource;
using LkRoom = IO.Livekit.Android.Room.Room;
using RoomEvent = IO.Livekit.Android.Events.RoomEvent;
using Track = IO.Livekit.Android.Room.Track.Track;
using VideoTrack = IO.Livekit.Android.Room.Track.VideoTrack;
using TrackPublication = IO.Livekit.Android.Room.Track.TrackPublication;

[assembly: Xamarin.Forms.Dependency(typeof(LiveKitService))]

namespace LiveKitDemo.Droid.LiveKit
{
    /// <summary>
    /// Android ILiveKitService backed by the LiveKit Android SDK binding.
    ///
    /// Owns one Room for the app's lifetime. Video surfaces (VideoViewRenderer)
    /// register here, and this class attaches the local camera track and the first
    /// remote video track to them as tracks come and go.
    /// All mutable state is touched on the main thread only.
    /// </summary>
    public sealed class LiveKitService : ILiveKitService
    {
        LkRoom room;
        FlowSubscription eventSubscription;

        SurfaceViewRenderer localView, remoteView;
        VideoTrack localTrack, remoteTrack;

        // Publication + owner of remoteTrack, so we can report mute state and a name.
        TrackPublication remotePublication;
        string remoteParticipantName;

        List<string> participants = new List<string>();

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public IReadOnlyList<string> Participants => participants;
        public bool IsMicrophoneEnabled { get; private set; }
        public bool IsCameraEnabled { get; private set; }

        public string LocalParticipantName =>
            room != null && Status != ConnectionStatus.Disconnected ? DisplayName(room.LocalParticipant) : null;

        public bool HasLocalVideo => localTrack != null;

        public bool HasRemoteVideo => remoteTrack != null && remotePublication != null && !remotePublication.Muted;

        public string RemoteVideoParticipantName => remotePublication != null ? remoteParticipantName : null;

        public int RemoteParticipantCount =>
            room != null && Status != ConnectionStatus.Disconnected ? room.RemoteParticipants.Count : 0;

        public event EventHandler StateChanged;
        public event EventHandler<string> ErrorOccurred;

        /// <summary>Created on first use so the SDK loads only when needed.</summary>
        LkRoom Room
        {
            get
            {
                if (room == null)
                {
                    room = LiveKitExtensions.CreateRoom(Android.App.Application.Context);
                    eventSubscription = room.SubscribeEvents(e => MainThread.BeginInvokeOnMainThread(() => OnRoomEvent(e)));
                }
                return room;
            }
        }

        // ---------------------------------------------------------------- calls

        public async Task ConnectAsync(string serverUrl, string accessToken)
        {
            SetStatus(ConnectionStatus.Connecting);
            try
            {
                await Room.ConnectAsync(serverUrl, accessToken);
            }
            catch
            {
                SetStatus(ConnectionStatus.Disconnected);
                throw;
            }

            // Pick up anyone (and any video) that was already in the room.
            AttachRemote(FindRemoteVideo(except: null));
            SetStatus(ConnectionStatus.Connected);
        }

        public Task DisconnectAsync()
        {
            room?.Disconnect();   // not a suspend function
            ResetCallState();
            SetStatus(ConnectionStatus.Disconnected);
            return Task.CompletedTask;
        }

        public async Task<bool> SetMicrophoneEnabledAsync(bool enabled)
        {
            bool ok = await Room.LocalParticipant.SetMicrophoneEnabledAsync(enabled);
            IsMicrophoneEnabled = Room.LocalParticipant.IsMicrophoneEnabled;
            RaiseStateChanged();
            return ok;
        }

        public async Task<bool> SetCameraEnabledAsync(bool enabled)
        {
            bool ok = await Room.LocalParticipant.SetCameraEnabledAsync(enabled);
            IsCameraEnabled = Room.LocalParticipant.IsCameraEnabled;

            var publication = Room.LocalParticipant.GetTrackPublication(Track.Source.Camera);
            AttachLocal(IsCameraEnabled ? publication?.Track as VideoTrack : null);

            RaiseStateChanged();
            return ok;
        }

        // -------------------------------------------------------- video surfaces

        /// <summary>Called by VideoViewRenderer when its native view is created.</summary>
        internal void RegisterView(SurfaceViewRenderer view, VideoSource source)
        {
            Room.InitVideoRenderer(view);   // binds the view to the room's EGL context

            if (source == VideoSource.Local)
            {
                localView = view;
                view.SetMirror(true);       // selfie view
                localTrack?.AddRenderer(view);
            }
            else
            {
                remoteView = view;
                remoteTrack?.AddRenderer(view);
            }
        }

        /// <summary>Called by VideoViewRenderer before it releases its native view.</summary>
        internal void UnregisterView(SurfaceViewRenderer view)
        {
            if (view == localView)
            {
                localTrack?.RemoveRenderer(view);
                localView = null;
            }
            else if (view == remoteView)
            {
                remoteTrack?.RemoveRenderer(view);
                remoteView = null;
            }
        }

        void AttachLocal(VideoTrack track)
        {
            if (SameTrack(track, localTrack)) return;
            if (localTrack != null && localView != null)
            {
                localTrack.RemoveRenderer(localView);
                localView.ClearImage();   // don't leave the last frame frozen on screen
            }
            localTrack = track;
            if (localTrack != null && localView != null) localTrack.AddRenderer(localView);
        }

        /// <summary>A remote video track with the publication and participant it belongs to.</summary>
        struct RemoteVideo
        {
            public VideoTrack Track;
            public TrackPublication Publication;
            public string ParticipantName;
        }

        void AttachRemote(RemoteVideo? video)
        {
            var track = video?.Track;
            remotePublication = video?.Publication;
            remoteParticipantName = video?.ParticipantName;

            if (SameTrack(track, remoteTrack)) return;
            if (remoteTrack != null && remoteView != null)
            {
                remoteTrack.RemoveRenderer(remoteView);
                remoteView.ClearImage();
            }
            remoteTrack = track;
            if (remoteTrack != null && remoteView != null) remoteTrack.AddRenderer(remoteView);
        }

        /// <summary>First subscribed remote video track, skipping <paramref name="except"/>.</summary>
        RemoteVideo? FindRemoteVideo(Track except)
        {
            if (room == null) return null;
            foreach (var participant in room.RemoteParticipants.Values)
            {
                foreach (var publication in participant.TrackPublications.Values)
                {
                    if (publication.Track is VideoTrack video && !SameTrack(video, except))
                        return new RemoteVideo { Track = video, Publication = publication, ParticipantName = DisplayName(participant) };
                }
            }
            return null;
        }

        static bool SameTrack(Java.Lang.Object a, Java.Lang.Object b)
            => a == null ? b == null : b != null && Android.Runtime.JNIEnv.IsSameObject(a.Handle, b.Handle);

        // ---------------------------------------------------------------- events

        void OnRoomEvent(RoomEvent e)
        {
            switch (e)
            {
                case RoomEvent.TrackSubscribed subscribed:
                    if (remoteTrack == null && subscribed.Track is VideoTrack video)
                        AttachRemote(new RemoteVideo
                        {
                            Track = video,
                            Publication = subscribed.Publication,
                            ParticipantName = DisplayName(subscribed.Participant),
                        });
                    break;

                case RoomEvent.TrackUnsubscribed unsubscribed:
                    if (SameTrack(unsubscribed.Track, remoteTrack))
                        AttachRemote(FindRemoteVideo(except: unsubscribed.Track));
                    break;

                case RoomEvent.TrackMuted _:
                case RoomEvent.TrackUnmuted _:
                    // A remote camera turned off/on keeps its track subscribed but muted.
                    // HasRemoteVideo reads the publication's Muted flag; just refresh.
                    if (!HasRemoteVideo) remoteView?.ClearImage();
                    break;

                case RoomEvent.ParticipantConnected _:
                case RoomEvent.ParticipantDisconnected _:
                    break;   // participant list refreshed below

                case RoomEvent.Reconnecting _:
                    SetStatus(ConnectionStatus.Reconnecting);
                    return;

                case RoomEvent.Reconnected _:
                    SetStatus(ConnectionStatus.Connected);
                    return;

                case RoomEvent.Disconnected disconnected:
                    bool wasConnected = Status != ConnectionStatus.Disconnected;
                    ResetCallState();
                    SetStatus(ConnectionStatus.Disconnected);
                    // Only surface server-side/unexpected disconnects, not our own Disconnect().
                    if (wasConnected && disconnected.Error != null)
                        ErrorOccurred?.Invoke(this, "Disconnected: " + disconnected.Error.Message);
                    return;

                default:
                    return;   // ignore events the demo doesn't display
            }

            RefreshParticipants();
            RaiseStateChanged();
        }

        // ----------------------------------------------------------------- state

        void ResetCallState()
        {
            AttachLocal(null);
            AttachRemote(null);
            IsMicrophoneEnabled = false;
            IsCameraEnabled = false;
            participants = new List<string>();
        }

        void RefreshParticipants()
        {
            var list = new List<string>();
            if (room != null && Status != ConnectionStatus.Disconnected)
            {
                list.Add(DisplayName(room.LocalParticipant) + " (you)");
                list.AddRange(room.RemoteParticipants.Values.Select(DisplayName).OrderBy(n => n));
            }
            participants = list;
        }

        static string DisplayName(IO.Livekit.Android.Room.Participant.Participant p)
            => string.IsNullOrEmpty(p.Name) ? p.GetIdentity() : p.Name;

        void SetStatus(ConnectionStatus status)
        {
            Status = status;
            RefreshParticipants();
            RaiseStateChanged();
        }

        void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
