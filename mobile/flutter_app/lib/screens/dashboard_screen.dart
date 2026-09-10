import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final summary = state.summary;
    final metrics = [
      ('Farms', summary.activeFarms, Icons.grass),
      ('Crop plans', summary.activeCropPlans, Icons.spa),
      ('Open issues', summary.openCropIssues, Icons.report_problem_outlined),
      ('Low stock', summary.lowStockResources, Icons.inventory_2_outlined),
      ('Pending tasks', summary.pendingTasks, Icons.task_alt),
      ('Approvals', summary.pendingApprovals, Icons.verified_outlined),
    ];

    return RefreshIndicator(
      onRefresh: state.refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Farmer dashboard', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              for (final metric in metrics)
                SizedBox(
                  width: 160,
                  child: Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(metric.$3, color: Theme.of(context).colorScheme.primary),
                          const SizedBox(height: 16),
                          Text(metric.$1),
                          Text('${metric.$2}', style: Theme.of(context).textTheme.headlineMedium),
                        ],
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}