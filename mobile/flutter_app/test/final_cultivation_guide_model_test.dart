import 'package:agriassist_mobile/models/api_models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('farmer approved plan parses its cultivation guide', () {
    final detail = FarmerApprovedPlan.fromJson({
      'contractVersion': 1,
      'cropPlanRequestId': 'request-1',
      'approvedTasks': [],
      'approvedIrrigationSchedules': [],
      'finalGuide': {
        'contractVersion': 1,
        'workflowId': 'workflow-1',
        'weeklyGuidance': ['Check field drainage.'],
        'currentStageExplanation': 'Early growth.',
        'monthlyGuidance': [
          {
            'month': '2026-11',
            'summary': 'Support crop growth.',
            'fieldAdvice': ['Keep channels clear.'],
            'weatherAdvice': ['Monitor rainfall.'],
          },
        ],
        'risks': [],
        'harvestPreparation': [],
        'whyThisPlan': 'Based on approved crop and field evidence.',
        'approvedActivities': [
          {'title': 'Prepare the field', 'scheduledAt': '2026-10-19T08:00:00Z'},
        ],
      },
    });

    expect(detail.finalGuide, isNotNull);
    expect(detail.finalGuide?.weeklyGuidance, ['Check field drainage.']);
    expect(detail.finalGuide?.monthlyGuidance.single.month, '2026-11');
    expect(
      detail.finalGuide?.approvedActivities.single.title,
      'Prepare the field',
    );
  });

  test('legacy approved workflow detail reports guide unavailable without failing parsing', () {
    final detail = FarmerApprovedPlan.fromJson({
      'contractVersion': 1,
      'cropPlanRequestId': 'request-1',
      'approvedTasks': [],
      'approvedIrrigationSchedules': [],
    });

    expect(detail.finalGuide, isNull);
  });
}
