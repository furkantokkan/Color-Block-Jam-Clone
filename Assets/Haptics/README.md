# Haptics

Small Unity haptics library for Android and iOS. It has no game, DI framework,
reactive framework or third-party native library dependency.

## Install

Copy this entire folder into a Unity project's `Assets` folder, including every
`.meta`, `Runtime/Android` and `Runtime/iOS` file. Reference the `Haptics` assembly
from any assembly definition that uses it. The folder is self-contained and can
be the root of a separate GitHub repository.

The current integration targets Unity 2022.3. Android's manifest merges the
normal `VIBRATE` permission automatically. The iOS native source imports only
for iOS; keep its `.meta` with the source.

## Use

```csharp
using Haptics;

// Create on Unity's main thread and retain for the owner's lifetime.
var output = new DeviceHapticOutput();
output.Play(HapticStyle.Light);
output.Play(HapticStyle.Success);
output.Stop();

// Dispose on the main thread when the owner ends.
output.Dispose();
```

`IHapticOutput` exposes `Play(HapticStyle)` and `Stop()`, so consumers can inject
a recording output for tests. `DeviceHapticOutput` also implements `IDisposable`.
The caller owns enable/disable settings, game-event mappings and DI registration.
No setting storage, subscription, component, Update loop or global singleton is
started by this library.

Styles: `None`, `Selection`, `Light`, `Medium`, `Rigid`, `Heavy`, `Success` and
`Failure`. Enum values are part of the C#/iOS boundary and must stay aligned.
`None`, unknown styles and calls after disposal are silent.

Android prebuilds one-shot effects and caches JNI references at construction;
playback reuses its argument array. API 26+ uses waveforms; older devices use
the first pulse duration. Devices without a vibrator stay silent. iOS uses
UIKit feedback generators; its one-shot feedback cannot be cancelled by `Stop`.
The Editor and other platforms stay silent. `Dispose` is idempotent, stops
Android playback and releases its JNI references.

## Tests

`Tests/EditMode` verifies the standalone assembly, native style values and
output lifecycle. Native device verification is separate from Editor tests;
physical feedback and native linking must be checked in an Android/iOS build.
