import 'dart:convert';

import 'package:agriassist_mobile/models/api_models.dart';
import 'package:agriassist_mobile/screens/crop_plan_list.dart';
import 'package:agriassist_mobile/services/api_client.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

CropPlanRecord plan(String id, int status) => CropPlanRecord(
  id: id,
  farmId: 'farm-1',
  fieldId: 'field-1',
  cropTypeId: 'crop-1',
  objective: 'Grow $id',
  status: status,
  preferredStartDate: '2026-10-01',
  preferredEndDate: '2027-01-01',
  budget: 25000,
  createdAt: '2026-09-24T00:00:00Z',
);

void main() {
  testWidgets('filters plans and opens only the approved workflow detail', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1000, 1500);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final api = ApiClient(
      httpClient: MockClient((request) async {
        expect(request.url.path, '/api/task-approval/workflows/wf-approved');
        return http.Response(
          jsonEncode({
            'workflow': {'id': 'wf-approved', 'status': 4},
            'decisions': [
              {'decision': 1, 'createdAt': '2026-09-24T09:00:00Z'},
            ],
            'steps': [
              {
                'agentName': 'CropFieldAnalysisAgent',
                'status': 3,
                'output': {
                  'fieldCondition': {'summary': 'Soil is ready.'},
                },
              },
              {
                'agentName': 'WeatherResourceAgent',
                'status': 3,
                'output': {'weatherSummary': 'Rain is expected.'},
              },
            ],
          }),
          200,
        );
      }),
      baseUrl: 'http://localhost/api',
    );
    final state = AppState(apiClient: api)
      ..cropPlans = [
        plan('pending', 2),
        plan('approved', 4),
        plan('rejected', 5),
      ]
      ..cropTypes = const [
        CropTypeOption(id: 'crop-1', name: 'Rice', isActive: true),
      ]
      ..fields = const [
        FieldOption(
          id: 'field-1',
          farmId: 'farm-1',
          name: 'North field',
          isActive: true,
        ),
      ]
      ..planWorkflows = const {
        'approved': CropPlanningWorkflowStatus(
          workflowId: 'wf-approved',
          cropPlanRequestId: 'approved',
          status: 4,
          currentStep: 'Approved',
          warnings: [],
        ),
      }
      ..tasks = const [
        FarmTaskRecord(
          id: 'task-1',
          title: 'Linked task',
          description: 'Prepare field',
          dueAt: '2026-10-01T00:00:00Z',
          status: 3,
          generatedByWorkflowId: 'wf-approved',
        ),
        FarmTaskRecord(
          id: 'task-2',
          title: 'Other task',
          description: '',
          dueAt: '',
          status: 3,
          generatedByWorkflowId: 'wf-other',
        ),
      ]
      ..irrigationSchedules = const [
        IrrigationScheduleRecord(
          id: 'schedule-1',
          fieldId: 'field-1',
          scheduledAt: '2026-10-02T00:00:00Z',
          durationMinutes: 30,
          notes: 'Morning',
          status: 2,
          generatedByWorkflowId: 'wf-approved',
        ),
      ];

    await tester.pumpWidget(
      ChangeNotifierProvider.value(
        value: state,
        child: const MaterialApp(
          home: Scaffold(body: SingleChildScrollView(child: CropPlanList())),
        ),
      ),
    );

    expect(find.text('Grow pending'), findsOneWidget);
    expect(find.text('Grow rejected'), findsOneWidget);
    await tester.tap(find.text('Approved').first);
    await tester.pumpAndSettle();
    expect(find.text('Grow pending'), findsNothing);
    expect(find.text('Grow rejected'), findsNothing);
    expect(find.text('Grow approved'), findsOneWidget);

    await tester.tap(find.text('View final plan →'));
    await tester.pumpAndSettle();
    expect(find.text('Soil is ready.'), findsOneWidget);
    expect(find.text('Rain is expected.'), findsOneWidget);
    expect(find.text('Linked task'), findsOneWidget);
    expect(find.text('Other task'), findsNothing);
    expect(find.text('2026-10-02 · 30 minutes'), findsOneWidget);
  });
}
