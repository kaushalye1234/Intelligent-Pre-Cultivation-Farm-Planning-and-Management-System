import 'dart:async';
import 'dart:convert';

import 'package:agriassist_mobile/screens/planning_progress_screen.dart';
import 'package:agriassist_mobile/services/api_client.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

http.Response jsonResponse(Object body, int statusCode) => http.Response.bytes(
  utf8.encode(jsonEncode(body)),
  statusCode,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

Map<String, dynamic> planJson({
  required String statusCode,
  required String statusLabel,
  String overallStatusCode = 'in_progress',
  String overallStatusLabel = 'In Progress',
}) => {
  'id': 'plan-1',
  'farmId': 'farm-1',
  'cropTypeId': 'crop-1',
  'objective': 'Grow rice',
  'status': switch (statusCode) {
    'preliminary_plan_ready' => 3,
    'approved' => 4,
    'rejected' => 5,
    'cancelled' => 6,
    _ => 2,
  },
  'statusCode': statusCode,
  'statusLabel': statusLabel,
  'overallStatusCode': overallStatusCode,
  'overallStatusLabel': overallStatusLabel,
  'preferredStartDate': '2026-10-15',
  'preferredEndDate': '2027-02-15',
  'budget': 180000,
  'createdAt': '2026-10-02T00:00:00Z',
};

Map<String, dynamic> workflowJson({
  required String statusCode,
  required String statusLabel,
  int status = 3,
}) => {
  'workflowId': 'workflow-1',
  'cropPlanRequestId': 'plan-1',
  'status': status,
  'currentStep': 'CropPlanningCoordinatorAgent',
  'statusCode': statusCode,
  'statusLabel': statusLabel,
  'overallStatusCode': 'in_progress',
  'overallStatusLabel': 'In Progress',
  'createdAt': '2026-10-02T00:00:00Z',
  'steps': <dynamic>[],
  'warnings': <dynamic>[],
};

Map<String, dynamic> planningResultJson() => {
  'workflowId': 'workflow-1',
  'status': 'SafeFailure',
  'requiresHumanReview': true,
  'warnings': <dynamic>[],
  'referenceDataStatus': 'Unknown',
  'objectiveSummary': '',
  'steps': <dynamic>[],
};

Widget harness(AppState state) => ChangeNotifierProvider.value(
  value: state,
  child: const MaterialApp(home: PlanningProgressScreen(planId: 'plan-1')),
);

void main() {
  testWidgets(
    'farmer confirms cancellation and the terminal action disappears',
    (tester) async {
      var cancelled = false;
      final api = ApiClient(
        httpClient: MockClient((request) async {
          if (request.method == 'POST' &&
              request.url.path == '/api/crop-planning/requests/plan-1/cancel') {
            cancelled = true;
            expect(jsonDecode(request.body), <String, dynamic>{});
            return jsonResponse(
              planJson(
                statusCode: 'cancelled',
                statusLabel: 'Cancelled',
                overallStatusCode: 'cancelled',
                overallStatusLabel: 'Cancelled',
              ),
              200,
            );
          }
          if (request.url.path == '/api/crop-planning/requests/plan-1') {
            return jsonResponse(
              cancelled
                  ? planJson(
                      statusCode: 'cancelled',
                      statusLabel: 'Cancelled',
                      overallStatusCode: 'cancelled',
                      overallStatusLabel: 'Cancelled',
                    )
                  : planJson(statusCode: 'pending', statusLabel: 'Pending'),
              200,
            );
          }
          if (request.url.path == '/api/crop-plans/plan-1/workflow-status') {
            return jsonResponse({
              'error': {'message': 'Not found'},
            }, 404);
          }
          throw StateError(
            'Unexpected request ${request.method} ${request.url}',
          );
        }),
        baseUrl: 'http://localhost/api',
      );

      await tester.pumpWidget(harness(AppState(apiClient: api)));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('cancel-crop-plan')), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('cancel-crop-plan')));
      await tester.pumpAndSettle();
      expect(find.text('Cancel this crop plan?'), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey('confirm-cancel-crop-plan')));
      await tester.pumpAndSettle();

      expect(cancelled, isTrue);
      expect(find.text('Cancelled'), findsWidgets);
      expect(find.byKey(const ValueKey('cancel-crop-plan')), findsNothing);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets('pending polls and automatically observes Admin starting AI', (
    tester,
  ) async {
    var planReads = 0;
    final methods = <String>[];
    final api = ApiClient(
      httpClient: MockClient((request) async {
        methods.add(request.method);
        if (request.url.path == '/api/crop-planning/requests/plan-1') {
          planReads++;
          final plan = planReads == 1
              ? planJson(statusCode: 'pending', statusLabel: 'Pending')
              : planJson(statusCode: 'ai_planning', statusLabel: 'AI Planning');
          return jsonResponse(plan, 200);
        }
        if (request.url.path == '/api/crop-plans/plan-1/workflow-status') {
          if (planReads == 1) {
            return jsonResponse({
              'error': {'message': 'Not found'},
            }, 404);
          }
          return jsonResponse(
            workflowJson(statusCode: 'ai_planning', statusLabel: 'AI Planning'),
            200,
          );
        }
        if (request.url.path == '/api/crop-plans/plan-1/planning-result') {
          return jsonResponse(planningResultJson(), 200);
        }
        throw StateError('Unexpected request ${request.url}');
      }),
      baseUrl: 'http://localhost/api',
    );

    final state = AppState(apiClient: api);
    await tester.pumpWidget(harness(state));
    await tester.pumpAndSettle();
    expect(find.text('Pending'), findsWidgets);
    expect(planReads, 1);

    await tester.pump(const Duration(seconds: 9));
    await tester.pumpAndSettle();
    expect(find.text('AI Planning'), findsWidgets);
    expect(planReads, 2);
    expect(methods, everyElement('GET'));

    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets(
    'stable failure stops polling and Refresh status stays read-only',
    (tester) async {
      var planReads = 0;
      final methods = <String>[];
      final api = ApiClient(
        httpClient: MockClient((request) async {
          methods.add(request.method);
          if (request.url.path == '/api/crop-planning/requests/plan-1') {
            planReads++;
            final plan = planReads == 1
                ? planJson(
                    statusCode: 'ai_planning_failed',
                    statusLabel:
                        'AI Planning Failed \u2014 awaiting Admin retry',
                  )
                : planJson(
                    statusCode: 'ai_planning',
                    statusLabel: 'AI Planning',
                  );
            return jsonResponse(plan, 200);
          }
          if (request.url.path == '/api/crop-plans/plan-1/workflow-status') {
            return jsonResponse(
              workflowJson(
                statusCode: planReads == 1
                    ? 'ai_planning_failed'
                    : 'ai_planning',
                statusLabel: planReads == 1
                    ? 'AI Planning Failed \u2014 awaiting Admin retry'
                    : 'AI Planning',
                status: planReads == 1 ? 5 : 3,
              ),
              200,
            );
          }
          if (request.url.path == '/api/crop-plans/plan-1/planning-result') {
            return jsonResponse(planningResultJson(), 200);
          }
          throw StateError('Unexpected request ${request.url}');
        }),
        baseUrl: 'http://localhost/api',
      );

      planReads = 1;
      expect(
        (await api.cropPlanningWorkflowStatus('plan-1')).statusCode,
        'ai_planning_failed',
      );
      await api.cropPlanningResult('plan-1');
      planReads = 0;
      methods.clear();
      final state = AppState(apiClient: api);
      await tester.pumpWidget(harness(state));
      await tester.pumpAndSettle();
      expect(state.cropPlans.single.statusCode, 'ai_planning_failed');
      expect(
        find.text('AI Planning Failed \u2014 awaiting Admin retry'),
        findsWidgets,
      );
      expect(find.byKey(const ValueKey('refresh-plan-status')), findsOneWidget);

      await tester.pump(const Duration(seconds: 18));
      expect(planReads, 1);

      await tester.tap(find.byKey(const ValueKey('refresh-plan-status')));
      await tester.pumpAndSettle();
      expect(find.text('AI Planning'), findsWidgets);
      expect(planReads, 2);
      expect(methods, everyElement('GET'));

      await tester.pump(const Duration(seconds: 9));
      await tester.pumpAndSettle();
      expect(planReads, 3);

      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets('polling pauses in background and refreshes on resume', (
    tester,
  ) async {
    var planReads = 0;
    final api = ApiClient(
      httpClient: MockClient((request) async {
        if (request.url.path == '/api/crop-planning/requests/plan-1') {
          planReads++;
          return jsonResponse(
            planJson(statusCode: 'pending', statusLabel: 'Pending'),
            200,
          );
        }
        if (request.url.path == '/api/crop-plans/plan-1/workflow-status') {
          return jsonResponse({
            'error': {'message': 'Not found'},
          }, 404);
        }
        throw StateError('Unexpected request ${request.url}');
      }),
      baseUrl: 'http://localhost/api',
    );

    await tester.pumpWidget(harness(AppState(apiClient: api)));
    await tester.pumpAndSettle();
    expect(planReads, 1);

    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
    await tester.pump();
    await tester.pump(const Duration(seconds: 18));
    expect(planReads, 1);

    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await tester.pumpAndSettle();
    expect(planReads, 2);

    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('polling never overlaps an in-flight status refresh', (
    tester,
  ) async {
    var planReads = 0;
    final delayedPlan = Completer<http.Response>();
    final api = ApiClient(
      httpClient: MockClient((request) async {
        if (request.url.path == '/api/crop-planning/requests/plan-1') {
          planReads++;
          if (planReads == 2) return delayedPlan.future;
          return jsonResponse(
            planJson(statusCode: 'pending', statusLabel: 'Pending'),
            200,
          );
        }
        if (request.url.path == '/api/crop-plans/plan-1/workflow-status') {
          return jsonResponse({
            'error': {'message': 'Not found'},
          }, 404);
        }
        throw StateError('Unexpected request ${request.url}');
      }),
      baseUrl: 'http://localhost/api',
    );

    await tester.pumpWidget(harness(AppState(apiClient: api)));
    await tester.pumpAndSettle();
    expect(planReads, 1);

    await tester.pump(const Duration(seconds: 9));
    expect(planReads, 2);
    await tester.pump(const Duration(seconds: 18));
    expect(planReads, 2);

    delayedPlan.complete(
      jsonResponse(
        planJson(statusCode: 'pending', statusLabel: 'Pending'),
        200,
      ),
    );
    await tester.pumpAndSettle();

    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('final state stops polling and exposes no refresh action', (
    tester,
  ) async {
    var planReads = 0;
    final api = ApiClient(
      httpClient: MockClient((request) async {
        if (request.url.path == '/api/crop-planning/requests/plan-1') {
          planReads++;
          return jsonResponse(
            planJson(
              statusCode: 'approved',
              statusLabel: 'Approved',
              overallStatusCode: 'approved',
              overallStatusLabel: 'Approved',
            ),
            200,
          );
        }
        if (request.url.path == '/api/crop-plans/plan-1/workflow-status') {
          return jsonResponse({
            'error': {'message': 'Not found'},
          }, 404);
        }
        throw StateError('Unexpected request ${request.url}');
      }),
      baseUrl: 'http://localhost/api',
    );

    await tester.pumpWidget(harness(AppState(apiClient: api)));
    await tester.pumpAndSettle();
    expect(find.text('Approved'), findsWidgets);
    expect(find.byKey(const ValueKey('refresh-plan-status')), findsNothing);

    await tester.pump(const Duration(seconds: 18));
    expect(planReads, 1);

    await tester.pumpWidget(const SizedBox.shrink());
  });
}
