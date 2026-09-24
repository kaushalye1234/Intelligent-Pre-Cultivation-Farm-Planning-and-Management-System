import 'package:agriassist_mobile/screens/crop_plan_screen.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:agriassist_mobile/models/api_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

Widget cropPlanHarness([AppState? state]) {
  return ChangeNotifierProvider(
    create: (_) => state ?? AppState(),
    child: const MaterialApp(home: Scaffold(body: CropPlanScreen())),
  );
}

void useTallTestViewport(WidgetTester tester) {
  tester.view.physicalSize = const Size(1000, 1200);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

void main() {
  testWidgets('shows AI crop planning form and empty workflow state', (
    tester,
  ) async {
    useTallTestViewport(tester);
    await tester.pumpWidget(cropPlanHarness());

    expect(find.text('Create crop plan'), findsOneWidget);
    expect(find.text('Start AI crop planning'), findsOneWidget);
    expect(find.text('No AI workflow started'), findsOneWidget);
  });

  testWidgets('validates required crop planning fields', (tester) async {
    useTallTestViewport(tester);
    await tester.pumpWidget(cropPlanHarness());

    await tester.ensureVisible(find.text('Start AI crop planning'));
    await tester.tap(find.text('Start AI crop planning'));
    await tester.pump();

    expect(find.text('Farm is required'), findsOneWidget);
    expect(find.text('Field is required'), findsOneWidget);
    expect(find.text('Crop is required'), findsOneWidget);
    expect(find.text('Required'), findsAtLeastNWidgets(3));
  });

  testWidgets('farm selection loads location and limits the field list', (
    tester,
  ) async {
    useTallTestViewport(tester);
    final state = AppState()
      ..farms = const [
        FarmOption(id: 'farm-1', name: 'Green Valley', location: 'Kurunegala'),
        FarmOption(id: 'farm-2', name: 'Hill Farm', location: 'Kandy'),
      ]
      ..fields = const [
        FieldOption(
          id: 'field-1',
          farmId: 'farm-1',
          name: 'North field',
          isActive: true,
        ),
        FieldOption(
          id: 'field-2',
          farmId: 'farm-2',
          name: 'Hill field',
          isActive: true,
        ),
      ];
    await tester.pumpWidget(cropPlanHarness(state));

    await tester.tap(find.byKey(const ValueKey('plan-farm')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Green Valley').last);
    await tester.pumpAndSettle();

    expect(find.text('Kurunegala'), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('plan-field-farm-1')));
    await tester.pumpAndSettle();
    expect(find.text('North field'), findsWidgets);
    expect(find.text('Hill field'), findsNothing);
  });
}
