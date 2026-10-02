import 'dart:convert';

import 'package:agriassist_mobile/services/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test(
    'farmer submission sends selected crop context to the request endpoint',
    () async {
      late http.Request captured;
      final client = ApiClient(
        httpClient: MockClient((request) async {
          captured = request;
          return http.Response(jsonEncode({'id': 'plan-1'}), 200);
        }),
        baseUrl: 'https://api.example.test/api',
      );

      final id = await client.createPreliminaryCropPlan(
        farmId: 'farm-1',
        fieldId: 'field-1',
        cropTypeId: 'crop-1',
        cropVarietyId: 'variety-1',
        cultivationSeason: 1,
        previousCropTypeId: 'crop-2',
        previousKnownProblems: ['PreviousFlooding'],
        preferredStartDate: '2026-10-15',
        preferredEndDate: '2027-02-15',
        budget: 180000,
        objective: 'Grow the selected rice variety.',
      );

      expect(id, 'plan-1');
      expect(captured.url.path, '/api/crop-planning/requests');
      final body = jsonDecode(captured.body) as Map<String, dynamic>;
      expect(body['farmId'], 'farm-1');
      expect(body['fieldId'], 'field-1');
      expect(body['cropTypeId'], 'crop-1');
      expect(body['cropVarietyId'], 'variety-1');
      expect(body['cultivationSeason'], 1);
      expect(body['previousCropTypeId'], 'crop-2');
      expect(body['previousKnownProblems'], ['PreviousFlooding']);
      expect(body['preferredStartDate'], '2026-10-15');
      expect(body['preferredEndDate'], '2027-02-15');
      expect(body['budget'], 180000);
    },
  );

  test(
    'single crop-plan refresh parses the authoritative lifecycle projection',
    () async {
      final client = ApiClient(
        httpClient: MockClient((request) async {
          expect(request.method, 'GET');
          expect(request.url.path, '/api/crop-planning/requests/plan-1');
          return http.Response(
            jsonEncode({
              'id': 'plan-1',
              'farmId': 'farm-1',
              'cropTypeId': 'crop-1',
              'objective': 'Grow rice',
              'status': 3,
              'statusCode': 'preliminary_plan_ready',
              'statusLabel': 'Preliminary Plan Ready',
              'overallStatusCode': 'in_progress',
              'overallStatusLabel': 'In Progress',
              'preferredStartDate': '2026-10-15',
              'preferredEndDate': '2027-02-15',
              'budget': 180000,
              'createdAt': '2026-10-02T00:00:00Z',
            }),
            200,
          );
        }),
        baseUrl: 'https://api.example.test/api',
      );

      final plan = await client.cropPlanRequest('plan-1');

      expect(plan.statusCode, 'preliminary_plan_ready');
      expect(plan.statusLabel, 'Preliminary Plan Ready');
      expect(plan.overallStatusCode, 'in_progress');
      expect(plan.overallStatusLabel, 'In Progress');
      expect(plan.isLifecycleStable, isTrue);
    },
  );
}
