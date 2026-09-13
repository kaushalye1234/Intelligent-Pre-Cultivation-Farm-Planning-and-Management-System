import 'package:agriassist_mobile/screens/crop_plan_screen.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

Widget cropPlanHarness() {
  return ChangeNotifierProvider(
    create: (_) => AppState(),
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
  testWidgets('shows AI crop planning form and empty workflow state', (tester) async {
    useTallTestViewport(tester);
    await tester.pumpWidget(cropPlanHarness());

    expect(find.text('AI crop planning'), findsOneWidget);
    expect(find.text('Submit and start AI planning'), findsOneWidget);
    expect(find.text('No AI workflow started'), findsOneWidget);
  });

  testWidgets('validates required crop planning fields', (tester) async {
    useTallTestViewport(tester);
    await tester.pumpWidget(cropPlanHarness());

    await tester.tap(find.text('Submit and start AI planning'));
    await tester.pump();

    expect(find.text('Farm is required'), findsOneWidget);
    expect(find.text('Crop type is required'), findsOneWidget);
    expect(find.text('Required'), findsAtLeastNWidgets(3));
  });
}


