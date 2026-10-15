using Android.Content;
using LiveKitDemo.Controls;
using LiveKitDemo.Droid.LiveKit;
using LiveKitDemo.Services;
using RendererCommon = Livekit.Org.Webrtc.RendererCommon;
using SurfaceViewRenderer = Livekit.Org.Webrtc.SurfaceViewRenderer;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

[assembly: ExportRenderer(typeof(VideoView), typeof(VideoViewRenderer))]

namespace LiveKitDemo.Droid.LiveKit
{
    /// <summary>Hosts a WebRTC SurfaceViewRenderer for a Forms VideoView.</summary>
    public class VideoViewRenderer : ViewRenderer<VideoView, SurfaceViewRenderer>
    {
        public VideoViewRenderer(Context context) : base(context) { }

        static LiveKitService Service => (LiveKitService)DependencyService.Get<ILiveKitService>();

        protected override void OnElementChanged(ElementChangedEventArgs<VideoView> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement != null && Control == null)
            {
                bool isLocal = e.NewElement.Source == VideoSource.Local;
                var surface = new SurfaceViewRenderer(Context);
                surface.SetEnableHardwareScaler(true);

                if (isLocal)
                {
                    // Small square picture-in-picture: crop to fill rather than letterbox,
                    // and draw above the remote SurfaceView it overlaps.
                    surface.SetScalingType(RendererCommon.ScalingType.ScaleAspectFill);
                    surface.SetZOrderMediaOverlay(true);
                }
                else
                {
                    // Main view: show the whole remote frame.
                    surface.SetScalingType(RendererCommon.ScalingType.ScaleAspectFit);
                }

                SetNativeControl(surface);
                Service.RegisterView(surface, e.NewElement.Source);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Control != null)
            {
                Service.UnregisterView(Control);
                Control.Release();   // frees the EGL surface
            }
            base.Dispose(disposing);
        }
    }
}
