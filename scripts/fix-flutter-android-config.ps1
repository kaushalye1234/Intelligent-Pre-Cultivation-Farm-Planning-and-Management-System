$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "..\mobile\flutter_app\android\app\build.gradle.kts"
$content = [System.IO.File]::ReadAllText($path)
$content = $content.Replace('    namespace = "com.example.agriassist_mobile"', '    namespace = "com.agriassist.mobile"')
$content = $content.Replace('    compileSdk = flutter.compileSdkVersion', '    compileSdk = 37')
$content = $content.Replace('        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).' + "`r`n" + '        applicationId = "com.example.agriassist_mobile"', '        applicationId = "com.agriassist.mobile"')
$content = $content.Replace('            // TODO: Add your own signing config for the release build.' + "`r`n" + '            // Signing with the debug keys for now, so `flutter run --release` works.' + "`r`n" + '            signingConfig = signingConfigs.getByName("debug")', '            signingConfig = signingConfigs.getByName("debug")')
[System.IO.File]::WriteAllText($path, $content, $encoding)
