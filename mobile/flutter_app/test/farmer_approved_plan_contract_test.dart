import 'dart:convert';
import 'dart:io';

import 'package:agriassist_mobile/models/api_models.dart';
import 'package:flutter_test/flutter_test.dart';

File fixture(String name) {
  var directory = Directory.current;
  while (true) {
    final candidate = File(
      '${directory.path}${Platform.pathSeparator}docs${Platform.pathSeparator}ai-usage${Platform.pathSeparator}fixtures${Platform.pathSeparator}member2${Platform.pathSeparator}$name',
    );
    if (candidate.existsSync()) return candidate;
    final parent = directory.parent;
    if (parent.path == directory.path) {
      throw StateError('Could not locate shared Member 2 fixture $name.');
    }
    directory = parent;
  }
}

FarmerApprovedPlan readPlan(String name) => FarmerApprovedPlan.fromJson(
  jsonDecode(fixture(name).readAsStringSync()) as Map<String, dynamic>,
);

void main() {
  test('legacy approved-plan fixture remains valid without crop health', () {
    final plan = readPlan('farmer-approved-plan.legacy.valid.json');

    expect(plan.contractVersion, 1);
    expect(plan.cropHealth, isNull);
    expect(plan.fieldSummary, isNotEmpty);
  });

  test('approved crop-health fixture exposes only farmer-safe fields', () {
    final raw =
        jsonDecode(
              fixture(
                'farmer-approved-plan.crop-health.valid.json',
              ).readAsStringSync(),
            )
            as Map<String, dynamic>;
    final plan = FarmerApprovedPlan.fromJson(raw);

    expect(plan.cropHealth?.possibleConcern, contains('may indicate'));
    expect(plan.cropHealth?.approvedPrePlantingActions, isNotEmpty);
    for (final forbidden in const [
      'analysisId',
      'reviewId',
      'fingerprint',
      'provider',
      'model',
      'sha256',
      'workflow',
    ]) {
      expect(raw.containsKey(forbidden), isFalse);
      expect(
        raw['cropHealth'] is Map &&
            (raw['cropHealth'] as Map).containsKey(forbidden),
        isFalse,
      );
    }
  });

  test(
    'unsupported approved-plan fixture is identifiable by contract version',
    () {
      final plan = readPlan('farmer-approved-plan.invalid-version.json');
      expect(plan.contractVersion, isNot(1));
    },
  );
}
