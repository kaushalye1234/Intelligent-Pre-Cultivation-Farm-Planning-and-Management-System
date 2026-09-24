import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class CropPlanScreen extends StatefulWidget {
  const CropPlanScreen({super.key});

  @override
  State<CropPlanScreen> createState() => _CropPlanScreenState();
}

class _CropPlanScreenState extends State<CropPlanScreen> {
  final _formKey = GlobalKey<FormState>();
  final _startController = TextEditingController();
  final _endController = TextEditingController();
  final _budgetController = TextEditingController();
  final _objectiveController = TextEditingController();
  String? _farmId;
  String? _fieldId;
  String? _cropTypeId;
  String? _varietyId;
  String? _previousCropId;
  int _season = 0;
  final Set<String> _previousProblems = {};

  static const _problemOptions = <String, String>{
    'PreviousFlooding': 'Previous flooding',
    'PreviousWaterShortage': 'Previous water shortage',
    'PreviousPestIssue': 'Previous pest issue',
    'PreviousDiseaseIssue': 'Previous disease issue',
    'PreviousSoilProblem': 'Previous soil problem',
    'Other': 'Other',
    'NoneKnown': 'None known',
  };

  @override
  void dispose() {
    _startController.dispose();
    _endController.dispose();
    _budgetController.dispose();
    _objectiveController.dispose();
    super.dispose();
  }

  Future<void> _pickDate(TextEditingController controller) async {
    final today = DateTime.now();
    final initial = DateTime.tryParse(controller.text) ?? today;
    final picked = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(today.year - 1),
      lastDate: DateTime(today.year + 4),
    );
    if (picked == null) return;
    controller.text = picked.toIso8601String().split('T').first;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate() ||
        _farmId == null ||
        _fieldId == null ||
        _cropTypeId == null) {
      return;
    }

    await context.read<AppState>().createAndStartAiCropPlan(
      farmId: _farmId!,
      fieldId: _fieldId!,
      cropTypeId: _cropTypeId!,
      cropVarietyId: _varietyId,
      cultivationSeason: _season,
      previousCropTypeId:
          _previousCropId == 'none' || _previousCropId == 'unknown'
          ? null
          : _previousCropId,
      previousKnownProblems: _previousProblems.toList(),
      startDate: _startController.text,
      endDate: _endController.text,
      budget: num.parse(_budgetController.text),
      objective: _objectiveController.text.trim(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final fields = state.fields
        .where((field) => field.farmId == _farmId && field.isActive)
        .toList();
    final crops = state.cropTypes.where((crop) => crop.isActive).toList();
    final varieties = state.cropVarieties
        .where(
          (variety) => variety.cropTypeId == _cropTypeId && variety.isActive,
        )
        .toList();
    final selectedFarm = state.farms
        .where((farm) => farm.id == _farmId)
        .firstOrNull;

    return RefreshIndicator(
      onRefresh: () async {
        await state.refresh();
        await state.refreshLastCropPlanningWorkflow();
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Create crop plan',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 4),
          const Text(
            'Tell us what you want to grow. Field officers and later workflow stages will review the current conditions.',
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Form(
                key: _formKey,
                child: Column(
                  children: [
                    DropdownButtonFormField<String>(
                      key: const ValueKey('plan-farm'),
                      initialValue: _farmId,
                      decoration: const InputDecoration(labelText: 'Farm'),
                      items: state.farms
                          .map(
                            (farm) => DropdownMenuItem(
                              value: farm.id,
                              child: Text(farm.name),
                            ),
                          )
                          .toList(),
                      onChanged: (value) => setState(() {
                        _farmId = value;
                        _fieldId = null;
                      }),
                      validator: (value) =>
                          value == null ? 'Farm is required' : null,
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      key: ValueKey('plan-field-$_farmId'),
                      initialValue: _fieldId,
                      decoration: const InputDecoration(labelText: 'Field'),
                      items: fields
                          .map(
                            (field) => DropdownMenuItem(
                              value: field.id,
                              child: Text(field.name),
                            ),
                          )
                          .toList(),
                      onChanged: (value) => setState(() => _fieldId = value),
                      validator: (value) =>
                          value == null ? 'Field is required' : null,
                    ),
                    const SizedBox(height: 12),
                    InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'Location',
                        helperText: 'Automatically loaded from the farm',
                      ),
                      child: Text(selectedFarm?.location ?? 'Select a farm'),
                    ),
                    const SizedBox(height: 12),
                    FormField<String>(
                      initialValue: _cropTypeId,
                      validator: (value) =>
                          value == null ? 'Crop is required' : null,
                      builder: (field) => Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          DropdownMenu<String>(
                            key: const ValueKey('plan-crop'),
                            label: const Text('Crop'),
                            enableFilter: true,
                            requestFocusOnTap: true,
                            initialSelection: _cropTypeId,
                            dropdownMenuEntries: crops
                                .map(
                                  (crop) => DropdownMenuEntry(
                                    value: crop.id,
                                    label: crop.name,
                                  ),
                                )
                                .toList(),
                            onSelected: (value) {
                              field.didChange(value);
                              setState(() {
                                _cropTypeId = value;
                                _varietyId = null;
                              });
                            },
                          ),
                          if (field.hasError)
                            Text(
                              field.errorText!,
                              style: TextStyle(
                                color: Theme.of(context).colorScheme.error,
                              ),
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 12),
                    DropdownMenu<String>(
                      key: ValueKey('plan-variety-$_cropTypeId'),
                      label: const Text('Crop variety'),
                      enableFilter: true,
                      requestFocusOnTap: true,
                      initialSelection: _varietyId ?? '',
                      dropdownMenuEntries: [
                        const DropdownMenuEntry(value: '', label: 'Not sure'),
                        ...varieties.map(
                          (variety) => DropdownMenuEntry(
                            value: variety.id,
                            label: variety.name,
                          ),
                        ),
                      ],
                      onSelected: (value) => setState(
                        () =>
                            _varietyId = value?.isEmpty == true ? null : value,
                      ),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<int>(
                      initialValue: _season,
                      decoration: const InputDecoration(
                        labelText: 'Cultivation season',
                      ),
                      items: const [
                        DropdownMenuItem(value: 1, child: Text('Maha')),
                        DropdownMenuItem(value: 2, child: Text('Yala')),
                        DropdownMenuItem(
                          value: 3,
                          child: Text('Other / Off-season'),
                        ),
                        DropdownMenuItem(value: 0, child: Text('Not sure')),
                      ],
                      onChanged: (value) =>
                          setState(() => _season = value ?? 0),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _startController,
                      readOnly: true,
                      decoration: const InputDecoration(
                        labelText: 'Preferred planting date',
                        prefixIcon: Icon(Icons.event_outlined),
                      ),
                      validator: _required,
                      onTap: () => _pickDate(_startController),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _endController,
                      readOnly: true,
                      decoration: const InputDecoration(
                        labelText: 'Expected harvest / end date',
                        prefixIcon: Icon(Icons.event_available_outlined),
                      ),
                      validator: _endDate,
                      onTap: () => _pickDate(_endController),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _budgetController,
                      decoration: const InputDecoration(
                        labelText: 'Budget (LKR)',
                        prefixIcon: Icon(Icons.payments_outlined),
                      ),
                      keyboardType: TextInputType.number,
                      validator: _budget,
                    ),
                    const SizedBox(height: 12),
                    DropdownMenu<String>(
                      key: const ValueKey('plan-previous-crop'),
                      label: const Text('Previous crop (optional)'),
                      enableFilter: true,
                      requestFocusOnTap: true,
                      initialSelection: _previousCropId ?? 'unknown',
                      dropdownMenuEntries: [
                        const DropdownMenuEntry(
                          value: 'unknown',
                          label: 'Unknown',
                        ),
                        const DropdownMenuEntry(
                          value: 'none',
                          label: 'No previous crop',
                        ),
                        ...crops.map(
                          (crop) => DropdownMenuEntry(
                            value: crop.id,
                            label: crop.name,
                          ),
                        ),
                      ],
                      onSelected: (value) =>
                          setState(() => _previousCropId = value),
                    ),
                    const SizedBox(height: 12),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        'Previous known problems (optional)',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                    ),
                    Wrap(
                      spacing: 8,
                      children: [
                        for (final entry in _problemOptions.entries)
                          FilterChip(
                            label: Text(entry.value),
                            selected: _previousProblems.contains(entry.key),
                            onSelected: (selected) => setState(() {
                              if (selected) {
                                if (entry.key == 'NoneKnown') {
                                  _previousProblems.clear();
                                } else {
                                  _previousProblems.remove('NoneKnown');
                                }
                                _previousProblems.add(entry.key);
                              } else {
                                _previousProblems.remove(entry.key);
                              }
                            }),
                          ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _objectiveController,
                      decoration: const InputDecoration(
                        labelText: 'Objective',
                        prefixIcon: Icon(Icons.flag_outlined),
                      ),
                      maxLength: 500,
                      minLines: 2,
                      maxLines: 4,
                      validator: (value) =>
                          _required(value) ??
                          (value!.length > 500
                              ? 'Objective must be 500 characters or fewer'
                              : null),
                    ),
                    const SizedBox(height: 16),
                    FilledButton.icon(
                      onPressed: state.isBusy ? null : _submit,
                      icon: const Icon(Icons.auto_awesome),
                      label: Text(
                        state.isBusy
                            ? 'Starting AI crop planning...'
                            : 'Start AI crop planning',
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: 12),
          _WorkflowSummaryCard(state: state),
        ],
      ),
    );
  }

  String? _required(String? value) =>
      value == null || value.trim().isEmpty ? 'Required' : null;
  String? _endDate(String? value) {
    if (_required(value) != null) return 'Required';
    final start = DateTime.tryParse(_startController.text);
    final end = DateTime.tryParse(value!);
    if (start != null && end != null && !end.isAfter(start)) {
      return 'End date must be after planting date';
    }
    return null;
  }

  String? _budget(String? value) {
    final parsed = num.tryParse(value ?? '');
    if (parsed == null || parsed <= 0) return 'Enter a positive budget';
    return null;
  }
}

class _WorkflowSummaryCard extends StatelessWidget {
  const _WorkflowSummaryCard({required this.state});

  final AppState state;

  @override
  Widget build(BuildContext context) {
    final start = state.lastWorkflowStart;
    final status = state.lastWorkflowStatus;
    final result = state.lastPlanningResult;

    if (start == null && status == null && result == null) {
      return const Card(
        child: ListTile(
          leading: Icon(Icons.spa_outlined),
          title: Text('No AI workflow started'),
          subtitle: Text(
            'Submit a crop plan request to start coordinator planning.',
          ),
        ),
      );
    }

    final warnings = <String>{
      ...?start?.warnings,
      ...?status?.warnings,
      ...?result?.warnings,
    }.toList();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  Icons.account_tree_outlined,
                  color: Theme.of(context).colorScheme.primary,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Workflow status',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                if (status != null) Chip(label: Text(status.statusLabel)),
              ],
            ),
            if (status != null) ...[
              const SizedBox(height: 8),
              Text(
                'Current step: ${status.currentStep.isEmpty ? 'Pending' : status.currentStep}',
              ),
            ],
            if (result != null) ...[
              const SizedBox(height: 12),
              Text('Planning result: ${result.status}'),
              Text('Reference data: ${result.referenceDataStatus}'),
              if (result.objectiveSummary.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(result.objectiveSummary),
              ],
              if (result.steps.isNotEmpty) ...[
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final step in result.steps)
                      Chip(
                        label: Text('${step.sequence}. ${step.assignedAgent}'),
                      ),
                  ],
                ),
              ],
            ],
            if (warnings.isNotEmpty) ...[
              const SizedBox(height: 12),
              for (final warning in warnings)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(
                        Icons.warning_amber_outlined,
                        size: 18,
                        color: Theme.of(context).colorScheme.error,
                      ),
                      const SizedBox(width: 6),
                      Expanded(child: Text(warning)),
                    ],
                  ),
                ),
            ],
            const SizedBox(height: 12),
            const Text(
              'Final execution status will appear after downstream agents and officer approval.',
            ),
          ],
        ),
      ),
    );
  }
}
