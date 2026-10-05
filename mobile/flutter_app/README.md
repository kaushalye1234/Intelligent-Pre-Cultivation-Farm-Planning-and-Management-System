# agriassist_mobile

A new Flutter project.

## Build against the deployed API

From the repository root on Windows:

```powershell
./scripts/build-render-mobile.ps1 -Target apk
```

The script builds a release APK with `AGRIASSIST_API_BASE_URL` set to
`https://agriassist-api-sl97.onrender.com/api`. Use `-Target web` or
`-Target appbundle` for those outputs, or pass `-ApiBaseUrl` for another HTTPS
deployment. Existing APKs must be rebuilt and reinstalled to use this setting.
An ordinary local Flutter build still uses the Android emulator API by default.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Learn Flutter](https://docs.flutter.dev/get-started/learn-flutter)
- [Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Flutter learning resources](https://docs.flutter.dev/reference/learning-resources)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.
