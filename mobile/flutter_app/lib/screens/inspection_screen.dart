import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';

class InspectionScreen extends StatefulWidget {
  const InspectionScreen({super.key});

  @override
  State<InspectionScreen> createState() => _InspectionScreenState();
}

class _InspectionScreenState extends State<InspectionScreen> {
  final _startKey = GlobalKey<FormState>();
  final _observationKey = GlobalKey<FormState>();
  final _issueKey = GlobalKey<FormState>();
  final _summaryController = TextEditingController(text: 'Field inspection started from mobile.');
  final _observationTypeController = TextEditingController(text: 'Field note');
  final _observationNotesController = TextEditingController();
  final _issueTitleController = TextEditingController();
  final _issueDescriptionController = TextEditingController();
  String? _fieldId;
  int _severity = 2;

  @override
  void dispose() {
    _summaryController.dispose();
    _observationTypeController.dispose();
    _observationNotesController.dispose();
    _issueTitleController.dispose();
    _issueDescriptionController.dispose();
    super.dispose();
  }

  Future<void> _startInspection(AppState state) async {
    if (!_startKey.currentState!.validate()) return;
    await state.startInspection(fieldId: _fieldId!, summary: _summaryController.text.trim());
  }

  Future<void> _addObservation(AppState state) async {
    if (!_observationKey.currentState!.validate()) return;
    await state.addObservation(observationType: _observationTypeController.text.trim(), notes: _observationNotesController.text.trim());
    _observationNotesController.clear();
  }

  Future<void> _reportIssue(AppState state) async {
    if (!_issueKey.currentState!.validate()) return;
    await state.reportCropIssue(title: _issueTitleController.text.trim(), description: _issueDescriptionController.text.trim(), severity: _severity);
    _issueTitleController.clear();
    _issueDescriptionController.clear();
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    InspectionRecord? activeInspection;
    for (final inspection in state.inspections) {
      if (inspection.id == state.activeInspectionId) {
        activeInspection = inspection;
        break;
      }
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Field inspection', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        Text('Capture GPS, attach image evidence, record observations, report crop issues and submit through ASP.NET.'),
        const SizedBox(height: 12),
        if (!state.isFieldOfficer)
          Card(
            color: Theme.of(context).colorScheme.errorContainer,
            child: const Padding(
              padding: EdgeInsets.all(12),
              child: Text('A Field Officer, Agricultural Officer or Admin role is required to submit inspections.'),
            ),
          ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Form(
              key: _startKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Start inspection', style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _fieldId,
                    decoration: const InputDecoration(labelText: 'Field'),
                    items: state.fields.map((field) => DropdownMenuItem(value: field.id, child: Text(field.name))).toList(),
                    onChanged: (value) => setState(() => _fieldId = value),
                    validator: (value) => value == null || value.isEmpty ? 'Select a field' : null,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _summaryController,
                    decoration: const InputDecoration(labelText: 'Inspection summary'),
                    minLines: 2,
                    maxLines: 3,
                    validator: (value) => value == null || value.trim().isEmpty ? 'Summary is required' : null,
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      OutlinedButton.icon(onPressed: state.captureLocation, icon: const Icon(Icons.my_location), label: const Text('GPS')),
                      OutlinedButton.icon(onPressed: state.captureInspectionPhoto, icon: const Icon(Icons.add_a_photo_outlined), label: const Text('Camera')),
                      OutlinedButton.icon(onPressed: state.pickInspectionPhoto, icon: const Icon(Icons.photo_library_outlined), label: const Text('Gallery')),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(state.lastPosition == null ? 'No GPS captured' : 'GPS: ${state.lastPosition!.latitude}, ${state.lastPosition!.longitude}'),
                  Text(state.lastPhotoName == null ? 'No image selected' : 'Image: ${state.lastPhotoName}'),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: state.isBusy || !state.isFieldOfficer ? null : () => _startInspection(state),
                    icon: const Icon(Icons.play_arrow),
                    label: const Text('Start inspection'),
                  ),
                ],
              ),
            ),
          ),
        ),
        Card(
          child: ListTile(
            leading: const Icon(Icons.assignment_turned_in_outlined),
            title: Text(activeInspection == null ? 'No active inspection' : activeInspection.summary),
            subtitle: Text(activeInspection == null ? 'Start or select one below' : activeInspection.statusLabel),
            trailing: FilledButton(
              onPressed: state.activeInspectionId == null || state.isBusy ? null : state.submitActiveInspection,
              child: const Text('Submit'),
            ),
          ),
        ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Form(
              key: _observationKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Observation', style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 12),
                  TextFormField(controller: _observationTypeController, decoration: const InputDecoration(labelText: 'Type')),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _observationNotesController,
                    decoration: const InputDecoration(labelText: 'Notes'),
                    minLines: 2,
                    maxLines: 4,
                    validator: (value) => value == null || value.trim().isEmpty ? 'Notes are required' : null,
                  ),
                  const SizedBox(height: 12),
                  FilledButton(onPressed: state.activeInspectionId == null || state.isBusy ? null : () => _addObservation(state), child: const Text('Add observation')),
                ],
              ),
            ),
          ),
        ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Form(
              key: _issueKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Crop issue', style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 12),
                  TextFormField(controller: _issueTitleController, decoration: const InputDecoration(labelText: 'Title'), validator: (value) => value == null || value.trim().isEmpty ? 'Title is required' : null),
                  const SizedBox(height: 12),
                  TextFormField(controller: _issueDescriptionController, decoration: const InputDecoration(labelText: 'Description'), minLines: 2, maxLines: 4, validator: (value) => value == null || value.trim().isEmpty ? 'Description is required' : null),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<int>(
                    initialValue: _severity,
                    decoration: const InputDecoration(labelText: 'Severity'),
                    items: const [
                      DropdownMenuItem(value: 1, child: Text('Low')),
                      DropdownMenuItem(value: 2, child: Text('Medium')),
                      DropdownMenuItem(value: 3, child: Text('High')),
                      DropdownMenuItem(value: 4, child: Text('Critical')),
                    ],
                    onChanged: (value) => setState(() => _severity = value ?? 2),
                  ),
                  const SizedBox(height: 12),
                  FilledButton(onPressed: state.activeInspectionId == null || state.isBusy ? null : () => _reportIssue(state), child: const Text('Report issue')),
                ],
              ),
            ),
          ),
        ),
        Text('History', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        if (state.activeInspectionHistory.isEmpty)
          const Card(child: ListTile(title: Text('No history for active inspection')))
        else
          ...state.activeInspectionHistory.map((event) => Card(
                child: ListTile(
                  leading: const Icon(Icons.history),
                  title: Text(event.eventType),
                  subtitle: Text(event.summary),
                ),
              )),
        Text('Recent inspections', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        ...state.inspections.take(8).map((inspection) => Card(
              child: ListTile(
                title: Text(inspection.summary.isEmpty ? inspection.id : inspection.summary),
                subtitle: Text(inspection.statusLabel),
                selected: inspection.id == state.activeInspectionId,
                onTap: () => state.selectInspection(inspection.id),
              ),
            )),
      ],
    );
  }
}


