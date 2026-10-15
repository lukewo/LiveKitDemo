using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiveKitDemo.Services
{
    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
    }

    /// <summary>
    /// Platform-neutral view of a LiveKit room. Implemented per platform and
    /// resolved through Xamarin.Forms DependencyService.
    /// All events are raised on the UI thread.
    /// </summary>
    public interface ILiveKitService
    {
        ConnectionStatus Status { get; }

        /// <summary>Display names of everyone in the room, local participant first.</summary>
        IReadOnlyList<string> Participants { get; }

        bool IsMicrophoneEnabled { get; }
        bool IsCameraEnabled { get; }

        /// <summary>Local name/identity once connected, otherwise null.</summary>
        string LocalParticipantName { get; }

        /// <summary>True when your camera track is live and attached to the preview.</summary>
        bool HasLocalVideo { get; }

        /// <summary>True when a remote video track is attached and not muted.</summary>
        bool HasRemoteVideo { get; }

        /// <summary>
        /// Name of the remote participant shown in the main view (whose video is
        /// attached, or muted), or null if no remote participant has published video.
        /// </summary>
        string RemoteVideoParticipantName { get; }

        /// <summary>Number of remote participants in the room.</summary>
        int RemoteParticipantCount { get; }

        /// <summary>Status, participants, or mic/camera state changed.</summary>
        event EventHandler StateChanged;

        /// <summary>A background error the UI should show (e.g. unexpected disconnect).</summary>
        event EventHandler<string> ErrorOccurred;

        Task ConnectAsync(string serverUrl, string accessToken);
        Task DisconnectAsync();
        Task<bool> SetMicrophoneEnabledAsync(bool enabled);
        Task<bool> SetCameraEnabledAsync(bool enabled);
    }
}
