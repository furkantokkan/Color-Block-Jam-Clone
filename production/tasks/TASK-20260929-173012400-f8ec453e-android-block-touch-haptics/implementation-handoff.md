# Android block-touch haptics — implementation handoff

- `AndroidVibratorHaptics` now uses amplitude 120/255 for the 40 ms Android `Light` pulse (previously 41/255). Other haptic styles are unchanged. API 22–25 still uses the existing duration-only `Vibrator.vibrate(long)` path; no phone-specific preprocessor define was added.
- `HapticsTests` now asserts that BlockTouch maps to `Light` and that the Vibration setting suppresses BlockTouch when off.
- Verification: Roslyn compile check passed for the two changed C# files (`COMPILE_OK`; 4 assemblies compiled). Unity 2022.3.62f2 EditMode `HapticsTests`: 7/7 passed (MCP test job `1e5f553d4d3048009b87203660354947`).
- Vivo Y36 and pre-API-26 device behavior remain unverified: no device is connected, and the contract requires exact approval before an Android player build.

Verification inputs reviewed: `Assets/ColorBlockJam/Runtime/Haptics/HapticFeedback.cs`, `Assets/ColorBlockJam/Runtime/Haptics/HapticStyle.cs`, `Assets/ColorBlockJam/Runtime/Haptics/HapticStyles.cs`, `Assets/ColorBlockJam/Runtime/Haptics/HapticsService.cs`, `Assets/ColorBlockJam/Runtime/Haptics/IHapticOutput.cs`, `Assets/ColorBlockJam/Runtime/Haptics/IVibrationToggle.cs`, `Assets/ColorBlockJam/Runtime/Player/PlayerVibrationToggle.cs`, and `Assets/ColorBlockJam/Tests/UI/EditMode/SettingsPopupFakes.cs`.
