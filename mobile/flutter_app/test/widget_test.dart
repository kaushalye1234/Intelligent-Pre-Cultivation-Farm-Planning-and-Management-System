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

    expect(find.text('AgriAssist'), findsOneWidget);
    expect(find.byType(TextFormField), findsNWidgets(2));
  });

  testWidgets('validates empty password', (tester) async {
    await tester.pumpWidget(loginHarness());

    await tester.ensureVisible(find.text('Sign in'));
    await tester.tap(find.text('Sign in'));
    await tester.pump();

    expect(find.text('Password is required'), findsOneWidget);
  });
}
