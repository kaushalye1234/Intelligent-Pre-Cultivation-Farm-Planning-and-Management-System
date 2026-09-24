import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';

class ApprovedCropPlanScreen extends StatelessWidget {
  const ApprovedCropPlanScreen({
    super.key,
    required this.plan,
    required this.workflowId,
  });

  final CropPlanRecord plan;
  final String workflowId;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return Scaffold(
      appBar: AppBar(title: const Text('Final crop plan')),
      body: FutureBuilder<ApprovedWorkflowDetail>(
        future: state.approvedWorkflowDetail(workflowId),
        builder: (context, snapshot) {
          if (snapshot.hasError) {
            return ListView(
              padding: const EdgeInsets.all(20),
              children: [
                const JourneyEmptyState(
                  icon: Icons.cloud_off_outlined,
                  title: 'Could not load this plan',
                  message:
                      'Return to Plans, refresh, and try opening the approved plan again.',
                ),
              ],
            );
          }
          if (!snapshot.hasData) {
            return const Center(child: CircularProgressIndicator());
          }
          final detail = snapshot.data!;
          if (detail.status != 4 ||
              detail.approvedAt == null ||
              detail.workflowId != workflowId) {
            return ListView(
              padding: const EdgeInsets.all(20),
              children: [
                const JourneyEmptyState(
                  icon: Icons.lock_clock_outlined,
                  title: 'Final details are not available yet',
                  message:
                      'Approved plan details will appear here when the workflow reports them.',
                ),
              ],
            );
          }

          final crop = state.cropTypes
              .where((item) => item.id == plan.cropTypeId)
              .firstOrNull;
          final variety = state.cropVarieties
              .where((item) => item.id == plan.cropVarietyId)
              .firstOrNull;
          final field = state.fields
              .where((item) => item.id == plan.fieldId)
              .firstOrNull;
          final farm = state.farms
              .where((item) => item.id == plan.farmId)
              .firstOrNull;
          final workflow = state.planWorkflows[plan.id];
          final tasks = state.tasks
              .where((item) => item.generatedByWorkflowId == workflowId)
              .toList();
          final schedules = state.irrigationSchedules
              .where((item) => item.generatedByWorkflowId == workflowId)
              .toList();
          final title = [
            crop?.name ?? 'Crop',
            if (variety != null) variety.name,
          ].join(' - ');

          return ListView(
            padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
            children: [
              JourneyPageIntro(
                eyebrow: 'APPROVED CROP PLAN',
                title: title,
                subtitle: 'A clear view of your approved growing plan.',
              ),
              const SizedBox(height: 18),
              JourneyCard(
                color: AgriColors.sage,
                child: Row(
                  children: [
                    const CircleAvatar(
                      backgroundColor: AgriColors.surface,
                      child: Icon(
                        Icons.verified_rounded,
                        color: AgriColors.forest,
                      ),
                    ),
                    const SizedBox(width: 13),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Ready to follow',
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                          const SizedBox(height: 3),
                          Text(
                            'Approved ${journeyDate(detail.approvedAt!)}',
                            style: Theme.of(context).textTheme.bodyMedium,
                          ),
                        ],
                      ),
                    ),
                    const JourneyStatusPill(
                      'Approved',
                      tone: JourneyTone.success,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),
              const JourneySectionHeading(title: 'Plan Overview'),
              const SizedBox(height: 12),
              JourneyCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    JourneyInfoRow(
                      label: 'Farm',
                      value: farm?.name ?? 'Unknown',
                      icon: Icons.landscape_outlined,
                    ),
                    JourneyInfoRow(
                      label: 'Field',
                      value: field?.name ?? 'Unknown',
                      icon: Icons.grid_view_outlined,
                    ),
                    JourneyInfoRow(
                      label: 'Season',
                      value: switch (plan.cultivationSeason) {
                        1 => 'Maha',
                        2 => 'Yala',
                        3 => 'Other / Off-season',
                        _ => 'Not sure',
                      },
                      icon: Icons.wb_sunny_outlined,
                    ),
                    JourneyInfoRow(
                      label: 'Planting',
                      value: journeyDate(plan.preferredStartDate),
                      icon: Icons.event_outlined,
                    ),
                    JourneyInfoRow(
                      label: 'Expected end',
                      value: journeyDate(plan.preferredEndDate),
                      icon: Icons.event_available_outlined,
                    ),
                    JourneyInfoRow(
                      label: 'Budget',
                      value: 'LKR ${plan.budget}',
                      icon: Icons.payments_outlined,
                    ),
                    if (plan.objective.isNotEmpty) ...[
                      const Divider(height: 26),
                      Text(
                        'Objective',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        plan.objective,
                        style: Theme.of(context).textTheme.bodyLarge,
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 24),
              const JourneySectionHeading(title: 'Field Insights'),
              const SizedBox(height: 12),
              JourneyCard(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.terrain_outlined,
                      color: AgriColors.forestLight,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        detail.fieldSummary ??
                            'No field insight was included in this approved plan.',
                        style: Theme.of(context).textTheme.bodyLarge,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),
              const JourneySectionHeading(
                title: 'Weather / Environmental Insights',
              ),
              const SizedBox(height: 12),
              JourneyCard(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.cloud_outlined,
                      color: AgriColors.forestLight,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        detail.weatherSummary ??
                            'No weather insight was included in this approved plan.',
                        style: Theme.of(context).textTheme.bodyLarge,
                      ),
                    ),
                  ],
                ),
              ),
              if (detail.warnings.isNotEmpty) ...[
                const SizedBox(height: 24),
                const JourneySectionHeading(title: 'Warnings'),
                const SizedBox(height: 12),
                for (final warning in detail.warnings) ...[
                  JourneyNotice(message: warning),
                  const SizedBox(height: 8),
                ],
              ],
              const SizedBox(height: 24),
              const JourneySectionHeading(title: 'Recommendations'),
              const SizedBox(height: 12),
              if (detail.recommendations.isEmpty)
                const JourneyEmptyState(
                  icon: Icons.lightbulb_outline_rounded,
                  title: 'No recommendations listed',
                  message:
                      'This approved plan did not include additional recommendations.',
                ),
              for (final recommendation in detail.recommendations) ...[
                JourneyCard(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(
                        Icons.check_circle_outline_rounded,
                        color: AgriColors.forest,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          recommendation,
                          style: Theme.of(context).textTheme.bodyLarge,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 9),
              ],
              const SizedBox(height: 24),
              const JourneySectionHeading(
                title: 'Tasks',
                subtitle: 'Approved work linked to this plan.',
              ),
              const SizedBox(height: 12),
              if (tasks.isEmpty)
                const JourneyEmptyState(
                  icon: Icons.task_alt_outlined,
                  title: 'No linked tasks available',
                  message:
                      'Tasks for this workflow will be shown here when available.',
                ),
              for (final task in tasks) ...[
                JourneyCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              task.title,
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                          ),
                          JourneyStatusPill(
                            task.statusLabel,
                            tone: task.status == 3 || task.status == 6
                                ? JourneyTone.success
                                : JourneyTone.warning,
                          ),
                        ],
                      ),
                      if (task.description.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        Text(
                          task.description,
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                      ],
                      const SizedBox(height: 9),
                      Text(
                        'Due ${journeyDate(task.dueAt)}',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 9),
              ],
              const SizedBox(height: 24),
              const JourneySectionHeading(
                title: 'Irrigation',
                subtitle: 'Schedules linked to this plan.',
              ),
              const SizedBox(height: 12),
              if (schedules.isEmpty)
                const JourneyEmptyState(
                  icon: Icons.water_drop_outlined,
                  title: 'No linked irrigation schedule',
                  message:
                      'A schedule for this workflow will be shown here when available.',
                ),
              for (final schedule in schedules) ...[
                JourneyCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          const Icon(
                            Icons.water_drop_outlined,
                            color: AgriColors.forest,
                          ),
                          const SizedBox(width: 9),
                          Expanded(
                            child: Text(
                              journeyDate(
                                schedule.scheduledAt,
                                includeTime: true,
                              ),
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                          ),
                          JourneyStatusPill(
                            schedule.statusLabel,
                            tone: schedule.status == 2 || schedule.status == 5
                                ? JourneyTone.success
                                : JourneyTone.warning,
                          ),
                        ],
                      ),
                      const SizedBox(height: 9),
                      Text(
                        '${schedule.durationMinutes} minutes',
                        style: Theme.of(context).textTheme.bodyLarge,
                      ),
                      if (schedule.notes.isNotEmpty) ...[
                        const SizedBox(height: 5),
                        Text(
                          schedule.notes,
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: 9),
              ],
              const SizedBox(height: 24),
              const JourneySectionHeading(title: 'Approval / Workflow'),
              const SizedBox(height: 12),
              JourneyCard(
                child: Column(
                  children: [
                    const JourneyInfoRow(
                      label: 'Decision',
                      value: 'Approved',
                      icon: Icons.verified_outlined,
                    ),
                    JourneyInfoRow(
                      label: 'Approved on',
                      value: journeyDate(detail.approvedAt!),
                      icon: Icons.event_available_outlined,
                    ),
                    JourneyInfoRow(
                      label: 'Workflow status',
                      value: workflow?.status == 4
                          ? workflow!.statusLabel
                          : 'Completed',
                      icon: Icons.account_tree_outlined,
                    ),
                    if (workflow?.currentStep.isNotEmpty == true)
                      JourneyInfoRow(
                        label: 'Latest stage',
                        value: workflow!.currentStep,
                      ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
