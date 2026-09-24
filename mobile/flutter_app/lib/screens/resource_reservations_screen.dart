import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/resource_state.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';
import 'resource_inventory_screen.dart' show formatQuantity;

class ResourceReservationsScreen extends StatelessWidget {
  const ResourceReservationsScreen({super.key});

  static const _filters = <(String, int?)>[
    ('All', null),
    ('Reserved', ReservationRecord.statusActive),
    ('Released', ReservationRecord.statusReleased),
    ('Cancelled', ReservationRecord.statusCancelled),
  ];

  @override
  Widget build(BuildContext context) {
    final state = context.watch<ResourceState>();
    return RefreshIndicator(
      onRefresh: state.loadReservations,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        children: [
          const JourneyPageIntro(
            eyebrow: 'RESERVATIONS',
            title: 'Reserved resources',
            subtitle: 'Review stock held for planned work.',
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final (label, status) in _filters)
                ChoiceChip(
                  label: Text(label),
                  selected: state.reservationStatus == status,
                  onSelected: (_) => state.setReservationStatus(status),
                ),
            ],
          ),
          const SizedBox(height: 14),
          if (state.reservationsError != null && state.reservations.isEmpty)
            JourneyEmptyState(
              icon: Icons.cloud_off_outlined,
              title: 'Reservations unavailable',
              message: state.reservationsError!,
              action: FilledButton.icon(
                onPressed: state.loadReservations,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('Try again'),
              ),
            )
          else if (state.reservationsLoading && state.reservations.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (state.reservations.isEmpty)
            const JourneyEmptyState(
              icon: Icons.bookmark_border_rounded,
              title: 'No reservations',
              message: 'Reservations in this view will appear here.',
            )
          else ...[
            for (final reservation in state.reservations) ...[
              _ReservationCard(reservation: reservation),
              const SizedBox(height: 10),
            ],
            if (state.reservationsError != null)
              JourneyNotice(
                message: state.reservationsError!,
                tone: JourneyTone.danger,
              ),
            if (state.reservationsHasMore)
              Center(
                child: state.reservationsLoading
                    ? const Padding(
                        padding: EdgeInsets.all(12),
                        child: CircularProgressIndicator(),
                      )
                    : OutlinedButton(
                        onPressed: state.loadMoreReservations,
                        child: const Text('Load more'),
                      ),
              ),
          ],
        ],
      ),
    );
  }
}

JourneyTone _statusTone(ReservationRecord reservation) => switch (reservation.status) {
  ReservationRecord.statusActive => JourneyTone.warning,
  ReservationRecord.statusReleased => JourneyTone.success,
  _ => JourneyTone.neutral,
};

class _ReservationCard extends StatelessWidget {
  const _ReservationCard({required this.reservation});

  final ReservationRecord reservation;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: () => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        showDragHandle: true,
        builder: (sheetContext) => ChangeNotifierProvider<ResourceState>.value(
          value: context.read<ResourceState>(),
          child: ReservationDetailSheet(reservation: reservation),
        ),
      ),
      child: JourneyCard(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    reservation.resourceName,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${formatQuantity(reservation.quantity)} ${reservation.unit} · ${reservation.purpose}',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                ],
              ),
            ),
            const SizedBox(width: 10),
            JourneyStatusPill(
              reservation.statusLabel,
              tone: _statusTone(reservation),
            ),
          ],
        ),
      ),
    );
  }
}

class ReservationDetailSheet extends StatefulWidget {
  const ReservationDetailSheet({super.key, required this.reservation});

  final ReservationRecord reservation;

  @override
  State<ReservationDetailSheet> createState() => _ReservationDetailSheetState();
}

class _ReservationDetailSheetState extends State<ReservationDetailSheet> {
  bool _working = false;
  String? _error;

  Future<void> _confirm({
    required String title,
    required String message,
    required String actionLabel,
    required Future<String?> Function(ResourceState) action,
    required String successMessage,
  }) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Keep reservation'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(actionLabel),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() {
      _working = true;
      _error = null;
    });
    final state = context.read<ResourceState>();
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    final error = await action(state);
    if (!mounted) return;
    if (error == null) {
      navigator.pop();
      messenger.showSnackBar(SnackBar(content: Text(successMessage)));
    } else {
      setState(() {
        _working = false;
        _error = error;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final reservation = widget.reservation;
    final canManage = context.read<ResourceState>().canManage;
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    reservation.resourceName,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ),
                JourneyStatusPill(
                  reservation.statusLabel,
                  tone: _statusTone(reservation),
                ),
              ],
            ),
            const SizedBox(height: 8),
            JourneyInfoRow(
              label: 'Quantity',
              value: '${formatQuantity(reservation.quantity)} ${reservation.unit}',
              icon: Icons.inventory_2_outlined,
            ),
            JourneyInfoRow(label: 'Purpose', value: reservation.purpose),
            JourneyInfoRow(
              label: 'Reserved on',
              value: journeyDate(reservation.createdAt, includeTime: true),
            ),
            if (reservation.releasedAt != null)
              JourneyInfoRow(
                label: 'Closed on',
                value: journeyDate(reservation.releasedAt!, includeTime: true),
              ),
            JourneyInfoRow(
              label: 'Requested by',
              value: reservation.requestedByUserId.length > 8
                  ? reservation.requestedByUserId.substring(0, 8)
                  : reservation.requestedByUserId,
            ),
            if (_error != null) ...[
              const SizedBox(height: 10),
              JourneyNotice(message: _error!, tone: JourneyTone.danger),
            ],
            // Only Resource Officers see mutation actions, and only for active reservations.
            if (canManage && reservation.isActive) ...[
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: _working
                    ? null
                    : () => _confirm(
                        title: 'Release reservation?',
                        message:
                            'The reserved quantity returns to available stock.',
                        actionLabel: 'Release',
                        action: (state) =>
                            state.releaseReservation(reservation.id),
                        successMessage: 'Reservation released.',
                      ),
                icon: const Icon(Icons.lock_open_rounded),
                label: const Text('Release'),
              ),
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: _working
                    ? null
                    : () => _confirm(
                        title: 'Cancel reservation?',
                        message:
                            'The reservation is cancelled and its quantity returns to available stock.',
                        actionLabel: 'Cancel reservation',
                        action: (state) =>
                            state.cancelReservation(reservation.id),
                        successMessage: 'Reservation cancelled.',
                      ),
                icon: const Icon(Icons.cancel_outlined),
                label: const Text('Cancel reservation'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
