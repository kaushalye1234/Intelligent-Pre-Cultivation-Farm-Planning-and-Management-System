import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';

class PlanningProgressScreen extends StatelessWidget {
  const PlanningProgressScreen({super.key, required this.planId});

  final String planId;

  Future<void> _refresh(AppState state) async {
    await state.refresh();
    if (state.lastCropPlanRequestId == planId) {
      await state.refreshLastCropPlanningWorkflow();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final plan = state.cropPlans.where((item) => item.id == planId).firstOrNull;
    final workflow =
        state.planWorkflows[planId] ??
        (state.lastCropPlanRequestId == planId
            ? state.lastWorkflowStatus
            : null);
    final latest = state.lastCropPlanRequestId == planId;
    final start = latest ? state.lastWorkflowStart : null;
    final result = latest ? state.lastPlanningResult : null;
    final crop = plan == null
        ? null
        : state.cropTypes
              .where((item) => item.id == plan.cropTypeId)
              .firstOrNull;
    final warnings = <String>{
      ...?workflow?.warnings,
      ...?start?.warnings,
      ...?result?.warnings,
    }.toList();
    final status =
        workflow?.statusLabel ??
        start?.status ??
        plan?.statusLabel ??
        'Status unavailable';
    final currentStep = workflow?.currentStep.isNotEmpty == true
        ? workflow!.currentStep
        : 'Awaiting a workflow update';

    return Scaffold(
      appBar: AppBar(title: const Text('Plan progress')),
      body: RefreshIndicator(
        onRefresh: () => _refresh(state),
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
          children: [
            JourneyPageIntro(
              eyebrow: 'CROP PLANNING',
              title: crop?.name ?? 'Your crop plan',
              subtitle:
                  'Follow each stage as your plan is reviewed and prepared.',
            ),
            const SizedBox(height: 20),
            JourneyCard(
              color: AgriColors.sage,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const JourneyEyebrow('CURRENT STAGE'),
                  const SizedBox(height: 10),
                  Text(
                    currentStep,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 9),
                  JourneyStatusPill(
                    status,
                    tone: _workflowTone(workflow?.status),
                  ),
                  if (plan != null) ...[
                    const SizedBox(height: 14),
                    Text(
                      'Requested planting: ${journeyDate(plan.preferredStartDate)}',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 24),
            const JourneySectionHeading(
              title: 'Planning timeline',
              subtitle: 'Stages reported by your planning workflow.',
            ),
            const SizedBox(height: 14),
            if (workflow?.steps.isEmpty != false)
              const JourneyEmptyState(
                icon: Icons.account_tree_outlined,
                title: 'Detailed stages are on their way',
                message:
                    'The workflow has not reported its individual steps yet. Pull down to refresh.',
              )
            else
              JourneyCard(
                child: Column(
                  children: [
                    for (var index = 0; index < workflow!.steps.length; index++)
                      _TimelineStep(
                        step: workflow.steps[index],
                        showLine: index < workflow.steps.length - 1,
                      ),
                  ],
                ),
              ),
            if (result?.objectiveSummary.isNotEmpty == true) ...[
              const SizedBox(height: 22),
              const JourneySectionHeading(title: 'Planning summary'),
              const SizedBox(height: 12),
              JourneyCard(
                child: Text(
                  result!.objectiveSummary,
                  style: Theme.of(context).textTheme.bodyLarge,
                ),
              ),
            ],
            if (warnings.isNotEmpty) ...[
              const SizedBox(height: 22),
              const JourneySectionHeading(title: 'Warnings'),
              const SizedBox(height: 12),
              for (final warning in warnings) ...[
                JourneyNotice(message: warning),
                const SizedBox(height: 8),
              ],
            ],
            const SizedBox(height: 22),
            JourneyCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const JourneyEyebrow('WHAT HAPPENS NEXT'),
                  const SizedBox(height: 9),
                  Text(
                    _nextMessage(workflow?.status),
                    style: Theme.of(context).textTheme.bodyLarge,
                  ),
                  if (workflow?.status == 8) ...[
                    const SizedBox(height: 12),
                    const JourneyStatusPill(
                      'Human approval required',
                      tone: JourneyTone.warning,
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  JourneyTone _workflowTone(int? status) => switch (status) {
    4 => JourneyTone.success,
    5 || 9 => JourneyTone.danger,
    7 || 8 || 10 || 11 => JourneyTone.warning,
    _ => JourneyTone.neutral,
  };

  String _nextMessage(int? status) => switch (status) {
    7 =>
      'A candidate plan is ready for officer review. Final tasks and irrigation are not available until approval.',
    8 =>
      'An officer will review the candidate plan. Final tasks and irrigation require explicit approval.',
    9 =>
      'This workflow was rejected. Review any warnings and decisions shown in your plan history.',
    10 =>
      'Revisions were requested. The planning workflow will show updated stages when available.',
    11 =>
      'Required information is missing. The workflow is waiting for review or additional data.',
    5 =>
      'The workflow stopped. Review the warnings and check again for an updated status.',
    4 =>
      'This workflow has completed. Your Plans area will show final details if the plan is approved.',
    _ =>
      'The next stage will appear here when the planning workflow reports an update.',
  };
}

class _TimelineStep extends StatelessWidget {
  const _TimelineStep({required this.step, required this.showLine});

  final CropPlanningStepStatus step;
  final bool showLine;

  @override
  Widget build(BuildContext context) {
    final tone = switch (step.status) {
      3 => JourneyTone.success,
      4 => JourneyTone.danger,
      2 => JourneyTone.warning,
      _ => JourneyTone.neutral,
    };
    final color = switch (step.status) {
      3 => AgriColors.forest,
      4 => AgriColors.danger,
      2 => AgriColors.amber,
      _ => AgriColors.muted,
    };
    final label = switch (step.status) {
      1 => 'Pending',
      2 => 'Running',
      3 => 'Completed',
      4 => 'Failed',
      5 => 'Skipped',
      _ => 'Unknown',
    };
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Column(
          children: [
            CircleAvatar(
              radius: 16,
              backgroundColor: tone == JourneyTone.success
                  ? AgriColors.sage
                  : AgriColors.canvas,
              child: Icon(
                step.status == 3 ? Icons.check_rounded : Icons.circle,
                size: step.status == 3 ? 18 : 10,
                color: color,
              ),
            ),
            if (showLine)
              Container(width: 2, height: 34, color: AgriColors.border),
          ],
        ),
        const SizedBox(width: 13),
        Expanded(
          child: Padding(
            padding: const EdgeInsets.only(top: 4, bottom: 15),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    step.stepName,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                const SizedBox(width: 8),
                JourneyStatusPill(label, tone: tone),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
