import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';

class PlanningProgressScreen extends StatefulWidget {
  const PlanningProgressScreen({super.key, required this.planId});

  final String planId;

  @override
  State<PlanningProgressScreen> createState() => _PlanningProgressScreenState();
}

class _PlanningProgressScreenState extends State<PlanningProgressScreen>
    with WidgetsBindingObserver {
  static const _pollInterval = Duration(seconds: 9);

  Timer? _pollTimer;
  bool _refreshInFlight = false;
  bool _isCancelling = false;
  bool _isForeground = true;
  String? _refreshWarning;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _refreshStatus();
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _isForeground = true;
      _refreshStatus();
      return;
    }
    _isForeground = false;
    _pollTimer?.cancel();
    _pollTimer = null;
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _pollTimer?.cancel();
    super.dispose();
  }

  Future<void> _refreshStatus() async {
    if (_refreshInFlight || !mounted) return;
    setState(() => _refreshInFlight = true);
    final state = context.read<AppState>();
    final succeeded = await state.refreshCropPlanProgress(widget.planId);
    if (!mounted) return;
    setState(() {
      _refreshInFlight = false;
      _refreshWarning = succeeded
          ? null
          : 'The latest status could not be loaded. Your last known status is still shown.';
    });
    _syncPolling(_planFrom(state));
  }

  CropPlanRecord? _planFrom(AppState state) =>
      state.cropPlans.where((item) => item.id == widget.planId).firstOrNull;

  void _syncPolling(CropPlanRecord? plan) {
    _pollTimer?.cancel();
    _pollTimer = null;
    if (!_isForeground || plan?.isLifecycleActive != true) return;
    _pollTimer = Timer.periodic(_pollInterval, (_) => _refreshStatus());
  }

  Future<void> _cancelPlan(CropPlanRecord plan) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Cancel this crop plan?'),
        content: const Text(
          'Your planning history, evidence, and decisions will be kept. '
          'You can create a new crop plan afterward.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Keep plan'),
          ),
          FilledButton(
            key: const ValueKey('confirm-cancel-crop-plan'),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            style: FilledButton.styleFrom(backgroundColor: AgriColors.danger),
            child: const Text('Cancel plan'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _isCancelling = true);
    final state = context.read<AppState>();
    final succeeded = await state.cancelCropPlanRequest(plan.id);
    if (!mounted) return;
    setState(() => _isCancelling = false);
    _syncPolling(_planFrom(state));
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          succeeded
              ? 'Crop plan cancelled. You can now create a new plan.'
              : state.error ?? 'The crop plan could not be cancelled.',
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final plan = _planFrom(state);
    final workflow =
        state.planWorkflows[widget.planId] ??
        (state.lastCropPlanRequestId == widget.planId
            ? state.lastWorkflowStatus
            : null);
    final latest = state.lastCropPlanRequestId == widget.planId;
    final result = latest ? state.lastPlanningResult : null;
    final crop = plan == null
        ? null
        : state.cropTypes
              .where((item) => item.id == plan.cropTypeId)
              .firstOrNull;
    final warnings = <String>{
      ...?workflow?.warnings,
      ...?result?.warnings,
    }.toList();
    final status = plan?.statusLabel ?? 'Loading plan status';
    final overallStatus = plan?.overallStatusLabel ?? 'Loading';
    final currentStep = workflow?.currentStep.isNotEmpty == true
        ? workflow!.currentStep
        : 'Awaiting a workflow update';

    return Scaffold(
      appBar: AppBar(title: const Text('Plan progress')),
      body: RefreshIndicator(
        onRefresh: _refreshStatus,
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
                  Text(status, style: Theme.of(context).textTheme.titleLarge),
                  const SizedBox(height: 9),
                  JourneyStatusPill(
                    status,
                    tone: _statusTone(plan?.statusCode),
                  ),
                  if (workflow != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      'Workflow step: $currentStep',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
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
            const SizedBox(height: 12),
            JourneyCard(
              child: Row(
                children: [
                  const Expanded(child: JourneyEyebrow('OVERALL WORKFLOW')),
                  JourneyStatusPill(
                    overallStatus,
                    tone: _statusTone(plan?.overallStatusCode),
                  ),
                ],
              ),
            ),
            if (_refreshWarning != null) ...[
              const SizedBox(height: 12),
              JourneyNotice(message: _refreshWarning!),
            ],
            if (plan?.isLifecycleStable == true) ...[
              const SizedBox(height: 12),
              Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  key: const ValueKey('refresh-plan-status'),
                  onPressed: _refreshInFlight ? null : _refreshStatus,
                  icon: _refreshInFlight
                      ? const SizedBox.square(
                          dimension: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.refresh_rounded),
                  label: Text(
                    _refreshInFlight ? 'Refreshing...' : 'Refresh status',
                  ),
                ),
              ),
            ],
            if (plan?.canCancel == true) ...[
              const SizedBox(height: 12),
              Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  key: const ValueKey('cancel-crop-plan'),
                  onPressed: _isCancelling ? null : () => _cancelPlan(plan!),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AgriColors.danger,
                  ),
                  icon: _isCancelling
                      ? const SizedBox.square(
                          dimension: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.cancel_outlined),
                  label: Text(_isCancelling ? 'Cancelling...' : 'Cancel plan'),
                ),
              ),
            ],
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
                    _nextMessage(plan?.statusCode),
                    style: Theme.of(context).textTheme.bodyLarge,
                  ),
                  if (workflow?.status == 8) ...[
                    const SizedBox(height: 12),
                    const JourneyStatusPill(
                      'Human approval required',
                      tone: JourneyTone.warning,
                    ),
                  ],
                  if (workflow?.status == 12) ...[
                    const SizedBox(height: 12),
                    const JourneyStatusPill(
                      'No farm work approved yet',
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

  JourneyTone _statusTone(String? statusCode) => switch (statusCode) {
    'approved' ||
    'preliminary_plan_ready' ||
    'candidate_ready' => JourneyTone.success,
    'rejected' ||
    'cancelled' ||
    'ai_planning_failed' ||
    'field_analysis_failed' ||
    'weather_resource_analysis_failed' ||
    'scheduling_validation_failed' ||
    'workflow_failed' => JourneyTone.danger,
    'awaiting_approval' ||
    'revision_requested' ||
    'waiting_for_required_data' ||
    'candidate_blocked' => JourneyTone.warning,
    _ => JourneyTone.neutral,
  };

  String _nextMessage(String? statusCode) => switch (statusCode) {
    'pending' =>
      'Your request is waiting for the Crop Planning Admin to start AI planning.',
    'ai_planning' =>
      'Member 1 crop planning is running. This page will update automatically.',
    'ai_planning_failed' =>
      'The Admin can retry AI planning using this same crop-plan request.',
    'preliminary_plan_ready' =>
      'Your preliminary crop plan is ready. Later field, weather, resource, and approval stages are still required.',
    'candidate_ready' =>
      'A candidate plan is ready for officer review. Final tasks and irrigation are not available until approval.',
    'awaiting_approval' =>
      'An officer will review the candidate plan. Final tasks and irrigation require explicit approval.',
    'rejected' =>
      'This workflow was rejected. Review any warnings and decisions shown in your plan history.',
    'revision_requested' =>
      'Revisions were requested. The planning workflow will show updated stages when available.',
    'waiting_for_required_data' =>
      'Required information is missing. The workflow is waiting for review or additional data.',
    'candidate_blocked' =>
      'The proposed schedule is blocked by verified evidence. No tasks, irrigation, or resource reservations have been approved. An officer must start a new workflow after the evidence changes.',
    'field_analysis_failed' ||
    'weather_resource_analysis_failed' ||
    'scheduling_validation_failed' ||
    'workflow_failed' =>
      'The workflow stopped. Review the warnings and check again for an updated status.',
    'approved' =>
      'Your workflow is approved. Open the final plan from your Plans area.',
    'cancelled' => 'This crop-plan request was cancelled.',
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
