import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';

class ApprovedCropPlanScreen extends StatelessWidget {
  const ApprovedCropPlanScreen({super.key, required this.plan});

  final CropPlanRecord plan;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return Scaffold(
      appBar: AppBar(title: const Text('Final crop plan')),
      body: FutureBuilder<FarmerApprovedPlan>(
        future: state.farmerApprovedPlan(plan.id),
        builder: (context, snapshot) {
          if (snapshot.hasError) {
            return ListView(
              padding: const EdgeInsets.all(20),
              children: [
                const JourneyEmptyState(
                  icon: Icons.cloud_off_outlined,
                  title: 'Could not load this plan',
                  message: 'Return to Plans, refresh, and try opening the approved plan again.',
                ),
              ],
            );
          }
          if (!snapshot.hasData) {
            return const Center(child: CircularProgressIndicator());
          }
          final detail = snapshot.data!;
          if (detail.contractVersion != 1 ||
              detail.approvedAt.isEmpty ||
              detail.cropPlanRequestId != plan.id) {
            return ListView(
              padding: const EdgeInsets.all(20),
              children: [
                const JourneyEmptyState(
                  icon: Icons.lock_clock_outlined,
                  title: 'Final details are not available yet',
                  message: 'Approved plan details will appear here when the workflow reports them.',
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
          final tasks = detail.approvedTasks;
          final schedules = detail.approvedIrrigationSchedules;
          final title = [
            detail.cropName.isNotEmpty ? detail.cropName : crop?.name ?? 'Crop',
            if (detail.varietyName != null)
              detail.varietyName!
            else if (variety != null)
              variety.name,
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
                            'Approved ${journeyDate(detail.approvedAt)}',
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
              if (detail.finalGuide != null) ...[
                const SizedBox(height: 24),
                _FinalGuideAdviceSection(
                  title: 'What to do this week',
                  advice: detail.finalGuide!.weeklyGuidance,
                  icon: Icons.today_outlined,
                ),
                if (detail.finalGuide!.currentStageExplanation != null) ...[
                  const SizedBox(height: 18),
                  const JourneySectionHeading(title: 'Current growth stage'),
                  const SizedBox(height: 10),
                  JourneyCard(
                    child: Text(detail.finalGuide!.currentStageExplanation!),
                  ),
                ],
                if (detail.finalGuide!.approvedActivities.isNotEmpty) ...[
                  const SizedBox(height: 20),
                  const JourneySectionHeading(
                    title: 'Officer-approved activities',
                  ),
                  const SizedBox(height: 10),
                  for (final activity in detail.finalGuide!.approvedActivities)
                    JourneyCard(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            activity.title,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                          if (activity.scheduledAt != null)
                            Text(journeyDate(activity.scheduledAt!)),
                          if (activity.quantity != null)
                            Text(
                              '${activity.quantity} ${activity.unit ?? ''}'
                                  .trim(),
                            ),
                          if (activity.durationMinutes != null)
                            Text(
                              'Duration: ${activity.durationMinutes} minutes',
                            ),
                        ],
                      ),
                    ),
                ],
                if (detail.finalGuide!.monthlyGuidance.isNotEmpty) ...[
                  const SizedBox(height: 20),
                  const JourneySectionHeading(
                    title: 'Monthly cultivation guide',
                  ),
                  const SizedBox(height: 10),
                  for (final month in detail.finalGuide!.monthlyGuidance)
                    JourneyCard(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            month.month,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                          Text(month.summary),
                          for (final advice in [
                            ...month.fieldAdvice,
                            ...month.weatherAdvice,
                          ])
                            Text('• $advice'),
                        ],
                      ),
                    ),
                ],
                if (detail.finalGuide!.risks.isNotEmpty)
                  _FinalGuideAdviceSection(
                    title: 'Risks to watch',
                    advice: detail.finalGuide!.risks,
                    icon: Icons.warning_amber_rounded,
                  ),
                if (detail.finalGuide!.harvestPreparation.isNotEmpty)
                  _FinalGuideAdviceSection(
                    title: 'Harvest preparation',
                    advice: detail.finalGuide!.harvestPreparation,
                    icon: Icons.agriculture_outlined,
                  ),
                const JourneySectionHeading(title: 'Why this plan'),
                JourneyCard(child: Text(detail.finalGuide!.whyThisPlan)),
              ],
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
                        detail.fieldSummary ?? 'No field insight was included in this approved plan.',
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
                        detail.weatherSummary ?? 'No weather insight was included in this approved plan.',
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
                  message: 'This approved plan did not include additional recommendations.',
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
              if (detail.cropHealth != null) ...[
                const SizedBox(height: 24),
                const JourneySectionHeading(
                  title: 'Crop Health Guidance',
                  subtitle:
                      'Reviewed by field staff and approved for this plan.',
                ),
                const SizedBox(height: 12),
                JourneyCard(
                  color: AgriColors.sage,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Crop Health Observation',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 6),
                      Text(detail.cropHealth!.cropHealthObservation),
                      const Divider(height: 26),
                      Text(
                        'Possible Concern',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 6),
                      Text(detail.cropHealth!.possibleConcern),
                      const SizedBox(height: 8),
                      JourneyNotice(
                        message: detail.cropHealth!.uncertaintyGuidance,
                      ),
                      if (detail
                          .cropHealth!
                          .approvedPrePlantingActions
                          .isNotEmpty) ...[
                        const Divider(height: 26),
                        Text(
                          'Before Planting',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: 6),
                        for (final action
                            in detail.cropHealth!.approvedPrePlantingActions)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 5),
                            child: Text('• $action'),
                          ),
                      ],
                      if (detail
                          .cropHealth!
                          .approvedMonitoringActions
                          .isNotEmpty) ...[
                        const Divider(height: 26),
                        Text(
                          'During Early Growth',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: 6),
                        for (final action
                            in detail.cropHealth!.approvedMonitoringActions)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 5),
                            child: Text('• $action'),
                          ),
                      ],
                      if (detail.cropHealth!.escalationGuidance?.isNotEmpty ==
                          true) ...[
                        const Divider(height: 26),
                        Text(
                          'When to Ask for Help',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: 6),
                        Text(detail.cropHealth!.escalationGuidance!),
                      ],
                      const Divider(height: 26),
                      Text(
                        'Why This Is Recommended',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 6),
                      Text(detail.cropHealth!.whyThisIsRecommended),
                    ],
                  ),
                ),
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
                  message: 'Tasks for this workflow will be shown here when available.',
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
                            task.status,
                            tone: JourneyTone.success,
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
                  message: 'A schedule for this workflow will be shown here when available.',
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
                            schedule.status,
                            tone: JourneyTone.success,
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
              const JourneySectionHeading(title: 'Approval'),
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
                      value: journeyDate(detail.approvedAt),
                      icon: Icons.event_available_outlined,
                    ),
                    const JourneyInfoRow(
                      label: 'Plan source',
                      value: 'Approved farmer plan',
                      icon: Icons.verified_user_outlined,
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

class _FinalGuideAdviceSection extends StatelessWidget {
  const _FinalGuideAdviceSection({
    required this.title,
    required this.advice,
    required this.icon,
  });

  final String title;
  final List<String> advice;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    if (advice.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: 18),
        JourneySectionHeading(title: title),
        const SizedBox(height: 10),
        for (final item in advice)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: JourneyCard(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(icon, color: AgriColors.forestLight),
                  const SizedBox(width: 12),
                  Expanded(child: Text(item)),
                ],
              ),
            ),
          ),
      ],
    );
  }
}
