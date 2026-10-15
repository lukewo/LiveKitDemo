using Xamarin.Forms;

namespace LiveKitDemo.Controls
{
    public enum VideoSource
    {
        /// <summary>This device's camera.</summary>
        Local,

        /// <summary>The first remote participant's video.</summary>
        Remote,
    }

    /// <summary>
    /// Placeholder for a native LiveKit video surface. Rendered on Android by
    /// VideoViewRenderer, which hosts a WebRTC SurfaceViewRenderer.
    /// </summary>
    public class VideoView : View
    {
        public static readonly BindableProperty SourceProperty = BindableProperty.Create(
            nameof(Source), typeof(VideoSource), typeof(VideoView), VideoSource.Remote);

        public VideoSource Source
        {
            get => (VideoSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }
    }
}
