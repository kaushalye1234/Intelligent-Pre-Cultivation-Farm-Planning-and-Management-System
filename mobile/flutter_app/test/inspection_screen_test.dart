import 'package:agriassist_mobile/models/api_models.dart';
import 'package:agriassist_mobile/screens/inspection_screen.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

Widget inspectionHarness() {
  final state = AppState()
    ..user = const UserProfile(id: 'field-user', fullName: 'Field Officer', email: 'field@example.test', role: 2, isActive: true)
    ..fields = const [FieldOption(id: 'field-1', name: 'North Field')];

  return ChangeNotifierProvider.value(
    value: state,
    child: const MaterialApp(home: Scaffold(body: InspectionScreen())),
  );
}

void main() {
  testWidgets('shows field officer inspection workflow controls', (tester) async {
    await tester.pumpWidget(inspectionHarness());

    expect(find.text('Field inspection'), findsOneWidget);
    expect(find.text('Start inspection'), findsWidgets);
    expect(find.text('GPS'), findsOneWidget);
    expect(find.text('Camera'), findsOneWidget);
  });

  testWidgets('validates field selection before starting inspection', (tester) async {
    await tester.pumpWidget(inspectionHarness());

    await tester.tap(find.widgetWithText(FilledButton, 'Start inspection'));
    await tester.pump();

    expect(find.text('Select a field'), findsOneWidget);
  });
}
