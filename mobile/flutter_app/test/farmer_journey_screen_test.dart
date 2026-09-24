import 'package:agriassist_mobile/models/api_models.dart';
import 'package:agriassist_mobile/screens/planning_progress_screen.dart';
import 'package:agriassist_mobile/screens/status_screen.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

void main() {
  testWidgets(
    'planning progress shows reported stages and required officer approval',
    (tester) async {
      tester.view.physicalSize = const Size(900, 1200);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final state = AppState()
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
            warnings: ['Check water availability'],
            steps: [
              CropPlanningStepStatus(stepName: 'Field analysis', status: 3),
              CropPlanningStepStatus(stepName: 'Officer review', status: 1),
            ],
          ),
        };
      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: state,
          child: const MaterialApp(
            home: PlanningProgressScreen(planId: 'plan-1'),
          ),
        ),
      );

      expect(find.text('Officer review'), findsWidgets);
      expect(find.text('Field analysis'), findsOneWidget);
      expect(find.text('Awaiting officer approval'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('Check water availability'),
        300,
      );
      expect(find.text('Check water availability'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('Human approval required'),
        300,
      );
      expect(find.text('Human approval required'), findsOneWidget);
    },
  );

  testWidgets('task view separates approved and completed work', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(900, 1100);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final state = AppState()
      ..tasks = const [
        FarmTaskRecord(
          id: 'task-1',
          title: 'Prepare field',
          description: '',
          dueAt: '2026-10-01T00:00:00Z',
          status: 3,
        ),
        FarmTaskRecord(
          id: 'task-2',
          title: 'Inspect crop',
          description: '',
          dueAt: '2026-09-01T00:00:00Z',
          status: 6,
        ),
      ];
    await tester.pumpWidget(
      ChangeNotifierProvider.value(
        value: state,
        child: const MaterialApp(home: Scaffold(body: StatusScreen())),
      ),
    );

    expect(find.text('Prepare field'), findsOneWidget);
    expect(find.text('Inspect crop'), findsNothing);
    await tester.tap(find.text('Completed').first);
    await tester.pumpAndSettle();
    expect(find.text('Inspect crop'), findsOneWidget);
    expect(find.text('Prepare field'), findsNothing);
  });
}
