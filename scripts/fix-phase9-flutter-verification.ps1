$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$appRoot = Join-Path $PSScriptRoot "..\mobile\flutter_app"
$libRoot = Join-Path $appRoot "lib"

foreach ($file in @("screens\crop_plan_screen.dart", "screens\resources_screen.dart")) {
    $path = Join-Path $libRoot $file
    $content = [System.IO.File]::ReadAllText($path)
    $content = $content.Replace("value: _farmId,", "initialValue: _farmId,")
    $content = $content.Replace("value: _fieldId,", "initialValue: _fieldId,")
    $content = $content.Replace("value: _cropTypeId,", "initialValue: _cropTypeId,")
    $content = $content.Replace("value: _stockId,", "initialValue: _stockId,")
    [System.IO.File]::WriteAllText($path, $content, $encoding)
}

[System.IO.File]::WriteAllText((Join-Path $appRoot "test\widget_test.dart"), @'
import 'package:agriassist_mobile/screens/login_screen.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

Widget loginHarness() {
  return ChangeNotifierProvider(
    create: (_) => AppState(),
    child: const MaterialApp(home: LoginScreen()),
  );
}

void main() {
  testWidgets('shows mobile login form', (tester) async {
    await tester.pumpWidget(loginHarness());

    expect(find.text('AgriAssist Mobile'), findsOneWidget);
    expect(find.byType(TextFormField), findsNWidgets(2));
  });

  testWidgets('validates empty password', (tester) async {
    await tester.pumpWidget(loginHarness());

    await tester.tap(find.text('Sign in'));
    await tester.pump();

    expect(find.text('Password is required'), findsOneWidget);
  });
}
'@, $encoding)
