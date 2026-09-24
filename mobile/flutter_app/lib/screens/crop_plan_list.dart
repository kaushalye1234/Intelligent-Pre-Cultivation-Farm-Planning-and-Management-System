import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';
import 'approved_crop_plan_screen.dart';
import 'crop_plan_screen.dart';
import 'planning_progress_screen.dart';

export 'approved_crop_plan_screen.dart' show ApprovedCropPlanScreen;

enum _PlanFilter { all, inProgress, approved, rejected }

class PlansHomeScreen extends StatelessWidget {
  const PlansHomeScreen({super.key});

  void _createPlan(BuildContext context) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => Scaffold(
          appBar: AppBar(title: const Text('New crop plan')),
          body: const CropPlanScreen(),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return RefreshIndicator(
      onRefresh: () async {
        await state.refresh();
        await state.refreshLastCropPlanningWorkflow();
      },
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        children: [
          const JourneyPageIntro(
            eyebrow: 'YOUR GROWING JOURNEY',
            title: 'Crop plans',
            subtitle:
                'See what is growing, what is in review, and what comes next.',
          ),
          const SizedBox(height: 20),
          JourneyCard(
            color: AgriColors.sage,
            child: Row(
              children: [
                const CircleAvatar(
                  backgroundColor: AgriColors.surface,
                  child: Icon(Icons.add_rounded, color: AgriColors.forest),
                ),
                const SizedBox(width: 13),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Planning a new crop?',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 3),
                      Text(
                        'Start with a few clear steps.',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
                IconButton(
                  tooltip: 'Create Crop Plan',
                  onPressed: () => _createPlan(context),
                  icon: const Icon(Icons.arrow_forward_rounded),
                ),
              ],
            ),
          ),
          const SizedBox(height: 25),
          const CropPlanList(),
        ],
      ),
    );
  }
}

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
        const JourneySectionHeading(
          title: 'My Crop Plans',
          subtitle: 'Follow each request from planning to decision.',
        ),
        const SizedBox(height: 13),
        Wrap(
          spacing: 8,
          runSpacing: 8,
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
        const SizedBox(height: 14),
        if (plans.isEmpty)
          const JourneyEmptyState(
            icon: Icons.spa_outlined,
            title: 'No crop plans in this view',
            message: 'Plans will appear here when they match this status.',
          ),
        for (final plan in plans) ...[
          _PlanCard(plan: plan, workflow: state.planWorkflows[plan.id]),
          const SizedBox(height: 11),
        ],
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
    final canOpenFinal = plan.status == 4 && workflow?.status == 4;
    final completed =
        workflow?.steps.where((step) => step.status == 3).length ?? 0;
    final total = workflow?.steps.length ?? 0;
    final title = [
      crop?.name ?? 'Crop',
      if (variety != null) variety.name,
    ].join(' - ');
    final status = workflow?.statusLabel ?? plan.statusLabel;
    final tone = workflow != null
        ? switch (workflow!.status) {
            4 => JourneyTone.success,
            5 || 9 => JourneyTone.danger,
            7 || 8 || 10 || 11 => JourneyTone.warning,
            _ => JourneyTone.neutral,
          }
        : switch (plan.status) {
            4 => JourneyTone.success,
            5 => JourneyTone.danger,
            6 => JourneyTone.neutral,
            _ => JourneyTone.warning,
          };

    void openDetail() {
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => canOpenFinal
              ? ApprovedCropPlanScreen(
                  plan: plan,
                  workflowId: workflow!.workflowId,
                )
              : PlanningProgressScreen(planId: plan.id),
        ),
      );
    }

    return JourneyCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const CircleAvatar(
                backgroundColor: AgriColors.sage,
                child: Icon(Icons.spa_outlined, color: AgriColors.forest),
              ),
              const SizedBox(width: 11),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: Theme.of(context).textTheme.titleLarge),
                    const SizedBox(height: 3),
                    Text(
                      [
                        farm?.name ?? 'Farm',
                        field?.name ?? 'Field',
                      ].join(' / '),
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 13),
          JourneyStatusPill(status, tone: tone),
          const SizedBox(height: 12),
          Row(
            children: [
              const Icon(
                Icons.event_outlined,
                size: 18,
                color: AgriColors.muted,
              ),
              const SizedBox(width: 7),
              Expanded(
                child: Text(
                  'Planting ${journeyDate(plan.preferredStartDate)}',
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
              ),
            ],
          ),
          if (plan.objective.isNotEmpty) ...[
            const SizedBox(height: 9),
            Text(
              plan.objective,
              style: Theme.of(context).textTheme.bodyLarge,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ],
          if (workflow?.currentStep.isNotEmpty == true) ...[
            const SizedBox(height: 10),
            Text(
              'Current stage: ${workflow!.currentStep}',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
          if (total > 0) ...[
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(6),
              child: LinearProgressIndicator(
                value: completed / total,
                minHeight: 5,
                backgroundColor: AgriColors.border,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              '$completed of $total stages completed',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
          if (workflow?.warnings.isNotEmpty == true) ...[
            const SizedBox(height: 10),
            Text(
              '${workflow!.warnings.length} workflow warning(s)',
              style: Theme.of(
                context,
              ).textTheme.bodyMedium?.copyWith(color: AgriColors.amber),
            ),
          ],
          const SizedBox(height: 12),
          const Divider(),
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: openDetail,
              icon: const Icon(Icons.arrow_forward_rounded),
              label: Text(canOpenFinal ? 'View final plan' : 'View progress'),
            ),
          ),
        ],
      ),
    );
  }
}
