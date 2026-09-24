import 'package:agriassist_mobile/models/api_models.dart';
import 'package:agriassist_mobile/screens/home_shell.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:agriassist_mobile/ui/agri_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

void main() {
  testWidgets('farmer journey fits a narrow phone and reaches the plan form', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(360, 640);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final state = AppState()
      ..user = const UserProfile(
        id: 'farmer-1',
        fullName: 'Taylor Farmer',
        email: 'taylor@example.test',
        role: 1,
        isActive: true,
      )
      ..cropPlans = const [
        CropPlanRecord(
          id: 'plan-1',
          farmId: 'farm-1',
          cropTypeId: 'crop-1',
          objective: 'Grow rice',
          status: 2,
          preferredStartDate: '2026-10-01',
          preferredEndDate: '2027-01-01',
          budget: 25000,
          createdAt: '2026-09-24T00:00:00Z',
        ),
      ]
      ..cropTypes = const [
        CropTypeOption(id: 'crop-1', name: 'Rice', isActive: true),
      ]
      ..planWorkflows = const {
        'plan-1': CropPlanningWorkflowStatus(
          workflowId: 'wf-1',
          cropPlanRequestId: 'plan-1',
          status: 8,
          currentStep: 'Officer review',
          warnings: [],
        ),
      }
      ..tasks = const [
        FarmTaskRecord(
          id: 'task-1',
          title: 'Prepare field',
          description: 'Check field readiness.',
          dueAt: '2026-10-01T00:00:00Z',
          status: 3,
        ),
      ];
    await tester.pumpWidget(
      ChangeNotifierProvider.value(
        value: state,
        child: MaterialApp(theme: AgriTheme.light(), home: const HomeShell()),
      ),
    );
    expect(tester.takeException(), isNull);

    await tester.tap(find.text('Plans'));
    await tester.pumpAndSettle();
    expect(find.text('Crop plans'), findsOneWidget);
    expect(tester.takeException(), isNull);

    await tester.tap(find.text('Tasks'));
    await tester.pumpAndSettle();
    expect(find.text('Tasks & schedules'), findsOneWidget);
    expect(tester.takeException(), isNull);

    await tester.tap(find.text('Home'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Create Crop Plan'));
    await tester.tap(find.text('Create Crop Plan'));
    await tester.pumpAndSettle();
    expect(find.text('New Crop Plan'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
