import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class StatusScreen extends StatelessWidget {
  const StatusScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return RefreshIndicator(
      onRefresh: state.refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('My farm status', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 16),
          _Section(title: 'Tasks', icon: Icons.task_alt, children: [
            if (state.tasks.isEmpty) const Text('No tasks found.')
            else ...state.tasks.map((task) => ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(task.title),
                  subtitle: Text('${task.statusLabel} • Due ${_date(task.dueAt)}\n${task.description}'),
                  isThreeLine: true,
                )),
          ]),
          _Section(title: 'Irrigation', icon: Icons.water_drop_outlined, children: [
            if (state.irrigationSchedules.isEmpty) const Text('No irrigation schedules found.')
            else ...state.irrigationSchedules.map((schedule) => ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text('${schedule.durationMinutes} minute schedule'),
                  subtitle: Text('${schedule.statusLabel} • ${_date(schedule.scheduledAt)}\n${schedule.notes}'),
                  isThreeLine: true,
                )),
          ]),
          _Section(title: 'Approval history', icon: Icons.history, children: [
            if (state.approvalHistory.isEmpty) const Text('No approval decisions found.')
            else ...state.approvalHistory.map((decision) => ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(decision.decisionLabel),
                  subtitle: Text('${_date(decision.createdAt)}\n${decision.comment}'),
                  isThreeLine: true,
                )),
          ]),
        ],
      ),
    );
  }

  static String _date(String value) {
    final parsed = DateTime.tryParse(value);
    return parsed == null ? value : '${parsed.toLocal()}'.split('.').first;
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.icon, required this.children});

  final String title;
  final IconData icon;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Card(
        margin: const EdgeInsets.only(bottom: 12),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [Icon(icon), const SizedBox(width: 8), Text(title, style: Theme.of(context).textTheme.titleMedium)]),
            const SizedBox(height: 8),
            ...children,
          ]),
        ),
      );
}
