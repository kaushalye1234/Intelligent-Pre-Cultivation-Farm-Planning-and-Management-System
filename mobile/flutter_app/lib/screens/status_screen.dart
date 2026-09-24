import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';

enum _TaskView { upcoming, completed, other }

class StatusScreen extends StatefulWidget {
  const StatusScreen({super.key});

  @override
  State<StatusScreen> createState() => _StatusScreenState();
}

class _StatusScreenState extends State<StatusScreen> {
  _TaskView _view = _TaskView.upcoming;

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final tasks =
        state.tasks
            .where(
              (task) => switch (_view) {
                _TaskView.upcoming => task.status == 3,
                _TaskView.completed => task.status == 6,
                _TaskView.other => task.status != 3 && task.status != 6,
              },
            )
            .toList()
          ..sort((a, b) {
            final aDate = DateTime.tryParse(a.dueAt);
            final bDate = DateTime.tryParse(b.dueAt);
            if (aDate == null) return 1;
            if (bDate == null) return -1;
            return aDate.compareTo(bDate);
          });
    return RefreshIndicator(
      onRefresh: state.refresh,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        children: [
          const JourneyPageIntro(
            eyebrow: 'FARM WORK',
            title: 'Tasks & schedules',
            subtitle:
                'Stay on top of approved work, irrigation, and past decisions.',
          ),
          const SizedBox(height: 24),
          const JourneySectionHeading(
            title: 'Farm tasks',
            subtitle: 'Work linked to your farm plans.',
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final option in _TaskView.values)
                ChoiceChip(
                  label: Text(switch (option) {
                    _TaskView.upcoming => 'Upcoming',
                    _TaskView.completed => 'Completed',
                    _TaskView.other => 'Other',
                  }),
                  selected: _view == option,
                  onSelected: (_) => setState(() => _view = option),
                ),
            ],
          ),
          const SizedBox(height: 14),
          if (tasks.isEmpty)
            JourneyEmptyState(
              icon: Icons.task_alt_outlined,
              title: switch (_view) {
                _TaskView.upcoming => 'No upcoming tasks',
                _TaskView.completed => 'No completed tasks yet',
                _TaskView.other => 'No other tasks',
              },
              message: 'Tasks in this view will appear here when available.',
            ),
          for (final task in tasks) ...[
            _TaskCard(task: task),
            const SizedBox(height: 10),
          ],
          const SizedBox(height: 24),
          const JourneySectionHeading(
            title: 'Irrigation',
            subtitle: 'Scheduled watering for your fields.',
          ),
          const SizedBox(height: 12),
          if (state.irrigationSchedules.isEmpty)
            const JourneyEmptyState(
              icon: Icons.water_drop_outlined,
              title: 'No irrigation schedules',
              message:
                  'Irrigation schedules will be shown here when available.',
            ),
          for (final schedule in state.irrigationSchedules) ...[
            JourneyCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Icon(
                        Icons.water_drop_outlined,
                        color: AgriColors.forestLight,
                      ),
                      const SizedBox(width: 9),
                      Expanded(
                        child: Text(
                          journeyDate(schedule.scheduledAt, includeTime: true),
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      JourneyStatusPill(
                        schedule.statusLabel,
                        tone: schedule.status == 2 || schedule.status == 5
                            ? JourneyTone.success
                            : schedule.status == 3
                            ? JourneyTone.danger
                            : JourneyTone.warning,
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Text(
                    'Duration: ${schedule.durationMinutes} minutes',
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
            const SizedBox(height: 10),
          ],
          const SizedBox(height: 24),
          const JourneySectionHeading(
            title: 'Approval history',
            subtitle: 'A record of decisions on your plans.',
          ),
          const SizedBox(height: 12),
          if (state.approvalHistory.isEmpty)
            const JourneyEmptyState(
              icon: Icons.history_rounded,
              title: 'No decisions yet',
              message:
                  'Approval decisions will be recorded here when available.',
            ),
          for (final decision in state.approvalHistory) ...[
            JourneyCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          journeyDate(decision.createdAt),
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      JourneyStatusPill(
                        decision.decisionLabel,
                        tone: decision.decision == 1
                            ? JourneyTone.success
                            : decision.decision == 2
                            ? JourneyTone.danger
                            : JourneyTone.warning,
                      ),
                    ],
                  ),
                  if (decision.comment.isNotEmpty) ...[
                    const SizedBox(height: 9),
                    Text(
                      decision.comment,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }
}

class _TaskCard extends StatelessWidget {
  const _TaskCard({required this.task});

  final FarmTaskRecord task;

  @override
  Widget build(BuildContext context) {
    final due = DateTime.tryParse(task.dueAt)?.toLocal();
    final overdue =
        task.status == 3 && due != null && due.isBefore(DateTime.now());
    final tone = switch (task.status) {
      3 => JourneyTone.success,
      4 || 7 => JourneyTone.danger,
      2 || 5 => JourneyTone.warning,
      _ => JourneyTone.neutral,
    };
    return JourneyCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CircleAvatar(
                backgroundColor: overdue
                    ? AgriColors.amberSoft
                    : AgriColors.sage,
                child: Icon(
                  overdue
                      ? Icons.priority_high_rounded
                      : Icons.checklist_rounded,
                  color: overdue ? AgriColors.amber : AgriColors.forest,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  task.title,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
              JourneyStatusPill(task.statusLabel, tone: tone),
            ],
          ),
          if (task.description.isNotEmpty) ...[
            const SizedBox(height: 12),
            Text(
              task.description,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
          const SizedBox(height: 12),
          Row(
            children: [
              Icon(
                Icons.event_outlined,
                size: 18,
                color: overdue ? AgriColors.amber : AgriColors.muted,
              ),
              const SizedBox(width: 7),
              Text(
                (overdue ? 'Overdue - ' : 'Due ') + journeyDate(task.dueAt),
                style: Theme.of(context).textTheme.labelLarge?.copyWith(
                  color: overdue ? AgriColors.amber : AgriColors.forest,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
