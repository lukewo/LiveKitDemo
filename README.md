# LiveKit Android SDK on Xamarin

A Xamarin.Forms video-call sample that uses the [LiveKit Android SDK](https://github.com/livekit/client-sdk-android) through a classic Xamarin.Android binding library. Tested end to end on a physical Android device against LiveKit Cloud.

- **LiveKit Android SDK** 2.29.0 (WebRTC 144.7559.14), bound directly from the AAR.
- **Kotlin types** come from Microsoft's official Xamarin.Kotlin.StdLib 1.9.23 and Xamarin.KotlinX.Coroutines 1.8.0.1 packages. No third-party NuGets.
- **App:** connect, publish mic and camera, full-size remote video with a picture-in-picture self view, participant list, and placeholders when there's no video.

## Projects

| Path | What it is |
|---|---|
| `LiveKitBindings/` | Binding library. `Additions/` turns Kotlin suspend functions and Flows into C# `Task`s and callbacks. |
| `LiveKitDemo/LiveKitDemo/` | Shared Xamarin.Forms UI, `ILiveKitService`, demo token generator |
| `LiveKitDemo/LiveKitDemo.Android/` | Android `ILiveKitService` implementation and the video renderer |
| `tools/` | SDK downloader, jar preparation, build, token check and browser-join scripts |

## Requirements

- Visual Studio 2022 with the Xamarin (Mobile development with .NET) workload
- JDK 17
- Android SDK platform 33 and build-tools 36. Xamarin's bundled D8 crashes on JNA, so the app uses build-tools 36's D8.

## Run it

1. **Credentials:** copy `LiveKitDemo/LiveKitDemo/DemoConfig.Local.cs.example` to `DemoConfig.Local.cs` and fill in your LiveKit API key and secret. The file is git-ignored. Without it, the app still builds, and you paste an access token in the app instead.
2. **Server URL:** set `ServerUrl` in `DemoConfig.cs` to your LiveKit project URL.
3. **Run:** open `LiveKitDemo.sln`, set `LiveKitDemo.Android` as the startup project, and run on a device or emulator. Tap **Connect**.
4. **Second participant:** run `tools\join-browser.ps1` from your PC. It opens LiveKit Meet, joined to the same room.

> **Demo only:** the app makes access tokens on the device from the API secret. A real app must get tokens from a backend.

## Updating the LiveKit SDK

1. Set `livekitVersion` in `tools/fetch-livekit/build.gradle`, then run `tools\fetch-livekit\gradlew fetch`. This downloads into `LiveKitBindings/Jars/maven`, which is git-ignored.
2. Run `tools\prepare-jars.ps1`. It regenerates `Jars/bind`, `Jars/embed` and `Jars/native`.
3. Rebuild `LiveKitBindings`, then check `obj/Debug/java-resolution-report.log` and `Transforms/Metadata.xml` for types that got dropped.

## Notes

- Classic Xamarin reached end of support in May 2024. The Kotlin NuGets are frozen at their last MonoAndroid versions, so newer LiveKit releases may not bind on this stack. For production, use .NET for Android or MAUI.
- Video renders into Android `SurfaceView`s, which can't be drawn over. That's why outlines and placeholders are done with padding and by hiding the video view.
