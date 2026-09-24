import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({
    super.key,
    required this.onCreatePlan,
    required this.onOpenPlans,
    required this.onOpenTasks,
  });

  final VoidCallback onCreatePlan;
  final VoidCallback onOpenPlans;
  final VoidCallback onOpenTasks;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final firstName = state.user?.fullName.split(' ').first ?? 'Farmer';
    final plans = [...state.cropPlans]
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
    final currentPlan =
        plans
            .where(
              (plan) =>
                  plan.status != 4 && plan.status != 5 && plan.status != 6,
            )
            .firstOrNull ??
        plans.firstOrNull;
    final workflow = currentPlan == null
        ? null
        : state.planWorkflows[currentPlan.id];
    final crop = currentPlan == null
        ? null
        : state.cropTypes
              .where((item) => item.id == currentPlan.cropTypeId)
              .firstOrNull;
    final upcomingTasks = state.tasks.where((task) => task.status == 3).toList()
      ..sort((a, b) {
        final aDate = DateTime.tryParse(a.dueAt);
        final bDate = DateTime.tryParse(b.dueAt);
        if (aDate == null) return 1;
        if (bDate == null) return -1;
        return aDate.compareTo(bDate);
      });
    final nextTask = upcomingTasks.firstOrNull;
    final nextDue = DateTime.tryParse(nextTask?.dueAt ?? '')?.toLocal();
    final nextTaskOverdue = nextDue != null && nextDue.isBefore(DateTime.now());
    final warnings = <String>[
      if (state.summary.openCropIssues > 0)
        '${state.summary.openCropIssues} open crop issues need attention.',
      if (state.summary.lowStockResources > 0)
        '${state.summary.lowStockResources} resources are low in stock.',
      if (nextTaskOverdue) 'An approved task is overdue.',
      ...?workflow?.warnings,
    ];
    final metrics = <(String, int, IconData)>[
      ('Farms', state.summary.activeFarms, Icons.landscape_outlined),
      ('Crop plans', state.summary.activeCropPlans, Icons.spa_outlined),
      ('Pending tasks', state.summary.pendingTasks, Icons.task_alt_outlined),
      ('Approvals', state.summary.pendingApprovals, Icons.verified_outlined),
      (
        'Open issues',
        state.summary.openCropIssues,
        Icons.report_problem_outlined,
      ),
      (
        'Low stock',
        state.summary.lowStockResources,
        Icons.inventory_2_outlined,
      ),
    ];

    return RefreshIndicator(
      onRefresh: state.refresh,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        children: [
          JourneyPageIntro(
            eyebrow: 'YOUR FARM TODAY',
            title: 'Good to see you, $firstName',
            subtitle: 'A clear view of what matters next across your farm.',
          ),
          const SizedBox(height: 24),
          JourneyCard(
            color: AgriColors.forest,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const JourneyEyebrow(
                  'YOUR NEXT GROWING SEASON',
                  color: Color(0xFFBED8C3),
                ),
                const SizedBox(height: 10),
                Text(
                  'Plan with confidence.',
                  style: Theme.of(
                    context,
                  ).textTheme.headlineSmall?.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 8),
                const Text(
                  'Bring your field, crop, dates and goals together in one guided plan.',
                  style: TextStyle(color: Color(0xFFE1EDE3), height: 1.45),
                ),
                const SizedBox(height: 20),
                FilledButton.icon(
                  onPressed: onCreatePlan,
                  style: FilledButton.styleFrom(
                    backgroundColor: Colors.white,
                    foregroundColor: AgriColors.forest,
                  ),
                  icon: const Icon(Icons.add_rounded),
                  label: const Text('Create Crop Plan'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 22),
          JourneySectionHeading(
            title: 'Next action',
            subtitle: 'The closest task on your horizon.',
            trailing: TextButton(
              onPressed: onOpenTasks,
              child: const Text('All tasks'),
            ),
          ),
          const SizedBox(height: 12),
          if (nextTask == null)
            const JourneyEmptyState(
              icon: Icons.task_alt_rounded,
              title: 'No approved tasks yet',
              message:
                  'Your approved farm tasks will appear here when they are available.',
            )
          else
            JourneyCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const CircleAvatar(
                        backgroundColor: AgriColors.sage,
                        child: Icon(
                          Icons.checklist_rounded,
                          color: AgriColors.forest,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          nextTask.title,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      const JourneyStatusPill(
                        'Approved',
                        tone: JourneyTone.success,
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Text(
                    nextTask.description,
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 12),
                  Text(
                    (nextTaskOverdue ? 'Overdue - ' : 'Due ') +
                        journeyDate(nextTask.dueAt),
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
                      color: nextTaskOverdue
                          ? AgriColors.amber
                          : AgriColors.forest,
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 22),
          JourneySectionHeading(
            title: 'Current plan',
            subtitle: 'Follow the progress of your latest plan.',
            trailing: TextButton(
              onPressed: onOpenPlans,
              child: const Text('All plans'),
            ),
          ),
          const SizedBox(height: 12),
          if (currentPlan == null)
            const JourneyEmptyState(
              icon: Icons.spa_outlined,
              title: 'Your plans start here',
              message:
                  'Create a crop plan to track every planning stage in one place.',
            )
          else
            JourneyCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          crop?.name ?? 'Crop plan',
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                      ),
                      JourneyStatusPill(
                        workflow?.statusLabel ?? currentPlan.statusLabel,
                        tone: _planTone(currentPlan),
                      ),
                    ],
                  ),
                  const SizedBox(height: 9),
                  Text(
                    'Planting ${journeyDate(currentPlan.preferredStartDate)}',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  if (workflow?.currentStep.isNotEmpty == true) ...[
                    const SizedBox(height: 10),
                    Text(
                      'Current stage: ${workflow!.currentStep}',
                      style: Theme.of(context).textTheme.bodyLarge,
                    ),
                  ],
                  const SizedBox(height: 12),
                  TextButton.icon(
                    onPressed: onOpenPlans,
                    icon: const Icon(Icons.arrow_forward_rounded),
                    label: const Text('View plan progress'),
                  ),
                ],
              ),
            ),
          if (warnings.isNotEmpty) ...[
            const SizedBox(height: 22),
            const JourneySectionHeading(title: 'Needs attention'),
            const SizedBox(height: 12),
            for (final warning in warnings) ...[
              JourneyNotice(message: warning),
              const SizedBox(height: 8),
            ],
          ],
          const SizedBox(height: 22),
          const JourneySectionHeading(
            title: 'Farm at a glance',
            subtitle: 'A quick overview of your activity.',
          ),
          const SizedBox(height: 12),
          GridView.builder(
            itemCount: metrics.length,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 2,
              crossAxisSpacing: 10,
              mainAxisSpacing: 10,
              childAspectRatio: 1.48,
            ),
            itemBuilder: (context, index) {
              final metric = metrics[index];
              return JourneyCard(
                padding: const EdgeInsets.all(15),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Icon(metric.$3, size: 22, color: AgriColors.forestLight),
                    Text(
                      metric.$2.toString(),
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    Text(
                      metric.$1,
                      style: Theme.of(context).textTheme.bodyMedium,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              );
            },
          ),
        ],
      ),
    );
  }

  JourneyTone _planTone(CropPlanRecord plan) => switch (plan.status) {
    4 => JourneyTone.success,
    5 => JourneyTone.danger,
    6 => JourneyTone.neutral,
    _ => JourneyTone.warning,
  };
}
