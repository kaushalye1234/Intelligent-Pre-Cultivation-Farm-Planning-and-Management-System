import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';

enum _PlanFilter { all, inProgress, approved, rejected }

class CropPlanList extends StatefulWidget {
  const CropPlanList({super.key});

  @override
  State<CropPlanList> createState() => _CropPlanListState();
}

class _CropPlanListState extends State<CropPlanList> {
  _PlanFilter filter = _PlanFilter.all;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final plans =
        state.cropPlans
            .where(
              (plan) => switch (filter) {
                _PlanFilter.all => true,
                _PlanFilter.inProgress =>
                  plan.status != 4 && plan.status != 5 && plan.status != 6,
                _PlanFilter.approved => plan.status == 4,
                _PlanFilter.rejected => plan.status == 5,
              },
            )
            .toList()
          ..sort((a, b) => b.createdAt.compareTo(a.createdAt));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('My Crop Plans', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          children: [
            for (final option in _PlanFilter.values)
              ChoiceChip(
                label: Text(switch (option) {
                  _PlanFilter.all => 'All',
                  _PlanFilter.inProgress => 'In Progress',
                  _PlanFilter.approved => 'Approved',
                  _PlanFilter.rejected => 'Rejected',
                }),
                selected: filter == option,
                onSelected: (_) => setState(() => filter = option),
              ),
          ],
        ),
        if (plans.isEmpty)
          const Card(
            child: ListTile(title: Text('No crop plans in this view')),
          ),
        for (final plan in plans)
          _PlanCard(plan: plan, workflow: state.planWorkflows[plan.id]),
      ],
    );
  }
}

class _PlanCard extends StatelessWidget {
  const _PlanCard({required this.plan, required this.workflow});

  final CropPlanRecord plan;
  final CropPlanningWorkflowStatus? workflow;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
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
    final canOpen = plan.status == 4 && workflow?.status == 4;
    final title = [
      crop?.name ?? 'Crop',
      if (variety != null) variety.name,
    ].join(' · ');

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(title),
              subtitle: Text(
                '${farm?.name ?? 'Farm'} · ${field?.name ?? 'Field'} · ${plan.preferredStartDate}',
              ),
              trailing: Chip(label: Text(plan.statusLabel)),
              onTap: canOpen
                  ? () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => ApprovedCropPlanScreen(
                          plan: plan,
                          workflowId: workflow!.workflowId,
                        ),
                      ),
                    )
                  : null,
            ),
            if (plan.objective.isNotEmpty) Text(plan.objective),
            if (workflow != null) ...[
              const SizedBox(height: 8),
              Text('Workflow: ${workflow!.statusLabel}'),
              if (workflow!.currentStep.isNotEmpty)
                Text('Current step: ${workflow!.currentStep}'),
              if (workflow!.steps.isNotEmpty)
                Wrap(
                  spacing: 6,
                  children: [
                    for (final step in workflow!.steps)
                      Chip(
                        label: Text(
                          '${step.stepName}: ${_stepStatus(step.status)}',
                        ),
                      ),
                  ],
                ),
            ] else
              const Text('Workflow has not started'),
            if (canOpen)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: () => Navigator.of(context).push(
                    MaterialPageRoute<void>(
                      builder: (_) => ApprovedCropPlanScreen(
                        plan: plan,
                        workflowId: workflow!.workflowId,
                      ),
                    ),
                  ),
                  child: const Text('View final plan →'),
                ),
              ),
          ],
        ),
      ),
    );
  }

  String _stepStatus(int status) => switch (status) {
    1 => 'Pending',
    2 => 'Running',
    3 => 'Completed',
    4 => 'Failed',
    5 => 'Skipped',
    _ => 'Unknown',
  };
}

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
            return const Center(
              child: Text(
                'Could not load this approved plan. Pull to refresh Plans and try again.',
              ),
            );
          }
          if (!snapshot.hasData) {
            return const Center(child: CircularProgressIndicator());
          }
          final detail = snapshot.data!;
          if (detail.status != 4 ||
              detail.approvedAt == null ||
              detail.workflowId != workflowId) {
            return const Center(
              child: Text('Approved plan details are not available yet.'),
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
          final tasks = state.tasks
              .where((item) => item.generatedByWorkflowId == workflowId)
              .toList();
          final schedules = state.irrigationSchedules
              .where((item) => item.generatedByWorkflowId == workflowId)
              .toList();

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text(
                [
                  crop?.name ?? 'Crop',
                  if (variety != null) variety.name,
                ].join(' · '),
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 8),
              Text('Farm: ${farm?.name ?? 'Unknown'}'),
              Text('Field: ${field?.name ?? 'Unknown'}'),
              Text(
                'Season: ${switch (plan.cultivationSeason) {
                  1 => 'Maha',
                  2 => 'Yala',
                  3 => 'Other / Off-season',
                  _ => 'Not sure',
                }}',
              ),
              Text('Planting: ${plan.preferredStartDate}'),
              Text('Expected end: ${plan.preferredEndDate}'),
              Text('Budget: LKR ${plan.budget}'),
              Text('Approved: ${detail.approvedAt!.split('T').first}'),
              const SizedBox(height: 12),
              Text('Objective', style: Theme.of(context).textTheme.titleMedium),
              Text(plan.objective),
              if (detail.fieldSummary != null) ...[
                const SizedBox(height: 12),
                Text(
                  'Field analysis',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                Text(detail.fieldSummary!),
              ],
              if (detail.weatherSummary != null) ...[
                const SizedBox(height: 12),
                Text(
                  'Weather and resources',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                Text(detail.weatherSummary!),
              ],
              if (detail.warnings.isNotEmpty) ...[
                const SizedBox(height: 12),
                Text(
                  'Warnings',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                for (final warning in detail.warnings) Text('• $warning'),
              ],
              if (detail.recommendations.isNotEmpty) ...[
                const SizedBox(height: 12),
                Text(
                  'Recommendations',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                for (final recommendation in detail.recommendations)
                  Text('• $recommendation'),
              ],
              const SizedBox(height: 16),
              Text(
                'Approved tasks',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              if (tasks.isEmpty)
                const Text('No workflow-linked tasks are available.'),
              for (final task in tasks)
                Card(
                  child: ListTile(
                    title: Text(task.title),
                    subtitle: Text(
                      '${task.description}\nDue: ${task.dueAt.split('T').first}',
                    ),
                    isThreeLine: true,
                  ),
                ),
              const SizedBox(height: 12),
              Text(
                'Irrigation schedule',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              if (schedules.isEmpty)
                const Text(
                  'No workflow-linked irrigation schedule is available.',
                ),
              for (final schedule in schedules)
                Card(
                  child: ListTile(
                    title: Text(
                      '${schedule.scheduledAt.split('T').first} · ${schedule.durationMinutes} minutes',
                    ),
                    subtitle: Text(schedule.notes),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}
