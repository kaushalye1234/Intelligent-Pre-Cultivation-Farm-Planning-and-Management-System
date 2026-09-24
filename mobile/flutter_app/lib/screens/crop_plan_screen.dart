import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/app_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';
import 'planning_progress_screen.dart';

class CropPlanScreen extends StatefulWidget {
  const CropPlanScreen({super.key});

  @override
  State<CropPlanScreen> createState() => _CropPlanScreenState();
}

class _CropPlanScreenState extends State<CropPlanScreen> {
  static const _titles = [
    'Farm & Crop',
    'Season & Dates',
    'History, Budget & Goal',
    'Review & Submit',
  ];
  static const _problemOptions = <String, String>{
    'PreviousFlooding': 'Previous flooding',
    'PreviousWaterShortage': 'Previous water shortage',
    'PreviousPestIssue': 'Previous pest issue',
    'PreviousDiseaseIssue': 'Previous disease issue',
    'PreviousSoilProblem': 'Previous soil problem',
    'Other': 'Other',
    'NoneKnown': 'None known',
  };

  final _formKeys = List.generate(3, (_) => GlobalKey<FormState>());
  final _scrollController = ScrollController();
  final _startController = TextEditingController();
  final _endController = TextEditingController();
  final _budgetController = TextEditingController();
  final _objectiveController = TextEditingController();
  String? _farmId;
  String? _fieldId;
  String? _cropTypeId;
  String? _varietyId;
  String? _previousCropId;
  String? _submittedPlanId;
  int _season = 0;
  int _step = 0;
  final Set<String> _previousProblems = {};

  @override
  void dispose() {
    _scrollController.dispose();
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

  void _showStep(int step) {
    FocusScope.of(context).unfocus();
    setState(() => _step = step);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _scrollController.hasClients) {
        _scrollController.animateTo(
          0,
          duration: const Duration(milliseconds: 260),
          curve: Curves.easeOut,
        );
      }
    });
  }

  void _next() {
    if (_formKeys[_step].currentState?.validate() != true) return;
    _showStep(_step + 1);
  }

  Future<void> _submit() async {
    if (_submittedPlanId != null) {
      _openProgress(_submittedPlanId!);
      return;
    }
    for (var index = 0; index < _formKeys.length; index++) {
      if (_formKeys[index].currentState?.validate() != true) {
        _showStep(index);
        return;
      }
    }
    if (_farmId == null || _fieldId == null || _cropTypeId == null) return;
    final state = context.read<AppState>();
    await state.createAndStartAiCropPlan(
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
    if (!mounted) return;
    if (state.error == null && state.lastCropPlanRequestId != null) {
      _submittedPlanId = state.lastCropPlanRequestId;
      _openProgress(_submittedPlanId!);
    }
  }

  void _openProgress(String id) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => PlanningProgressScreen(planId: id),
      ),
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
    final selectedField = fields
        .where((field) => field.id == _fieldId)
        .firstOrNull;
    final selectedCrop = crops
        .where((crop) => crop.id == _cropTypeId)
        .firstOrNull;
    final selectedVariety = varieties
        .where((variety) => variety.id == _varietyId)
        .firstOrNull;
    final previousCrop = crops
        .where((crop) => crop.id == _previousCropId)
        .firstOrNull;

    return ListView(
      controller: _scrollController,
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
      children: [
        const JourneyPageIntro(
          eyebrow: 'GROW WITH CONFIDENCE',
          title: 'New Crop Plan',
          subtitle:
              'Tell us about your field and growing goals. Each detail helps shape a useful plan.',
        ),
        const SizedBox(height: 22),
        JourneyCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              JourneyEyebrow('STEP ${_step + 1} OF 4'),
              const SizedBox(height: 10),
              Text(
                _titles[_step],
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 15),
              Row(
                children: [
                  for (var index = 0; index < _titles.length; index++) ...[
                    Expanded(
                      child: Container(
                        height: 5,
                        decoration: BoxDecoration(
                          color: index <= _step
                              ? AgriColors.forest
                              : AgriColors.border,
                          borderRadius: BorderRadius.circular(6),
                        ),
                      ),
                    ),
                    if (index < _titles.length - 1) const SizedBox(width: 5),
                  ],
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        JourneyCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Offstage(
                offstage: _step != 0,
                child: Form(
                  key: _formKeys[0],
                  child: _farmCropStep(
                    state,
                    fields,
                    crops,
                    varieties,
                    selectedFarm,
                  ),
                ),
              ),
              Offstage(
                offstage: _step != 1,
                child: Form(key: _formKeys[1], child: _seasonDatesStep()),
              ),
              Offstage(
                offstage: _step != 2,
                child: Form(key: _formKeys[2], child: _historyGoalStep(crops)),
              ),
              if (_step == 3)
                _reviewStep(
                  selectedFarm,
                  selectedField,
                  selectedCrop,
                  selectedVariety,
                  previousCrop,
                ),
              if (state.error != null) ...[
                const SizedBox(height: 18),
                JourneyNotice(message: state.error!, tone: JourneyTone.danger),
              ],
              const SizedBox(height: 24),
              Row(
                children: [
                  if (_step > 0) ...[
                    Expanded(
                      child: OutlinedButton(
                        onPressed: state.isBusy
                            ? null
                            : () => _showStep(_step - 1),
                        child: const Text('Back'),
                      ),
                    ),
                    const SizedBox(width: 10),
                  ],
                  Expanded(
                    child: FilledButton.icon(
                      onPressed: state.isBusy
                          ? null
                          : (_step < 3 ? _next : _submit),
                      icon: state.isBusy
                          ? const SizedBox.square(
                              dimension: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : Icon(
                              _step < 3
                                  ? Icons.arrow_forward_rounded
                                  : Icons.auto_awesome_rounded,
                            ),
                      label: Text(
                        state.isBusy
                            ? 'Starting AI crop planning...'
                            : _step < 3
                            ? 'Continue'
                            : _submittedPlanId == null
                            ? 'Start AI crop planning'
                            : 'View plan progress',
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        const JourneyNotice(
          title: 'Human review is part of the journey',
          message:
              'Final tasks and irrigation schedules only appear after officer approval.',
        ),
      ],
    );
  }

  Widget _farmCropStep(
    AppState state,
    List<FieldOption> fields,
    List<CropTypeOption> crops,
    List<CropVarietyOption> varieties,
    FarmOption? selectedFarm,
  ) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const JourneySectionHeading(
        title: 'Choose your land and crop',
        subtitle: 'Start with the place you plan to grow.',
      ),
      const SizedBox(height: 18),
      DropdownButtonFormField<String>(
        key: const ValueKey('plan-farm'),
        initialValue: _farmId,
        decoration: const InputDecoration(labelText: 'Farm'),
        items: state.farms
            .map(
              (farm) =>
                  DropdownMenuItem(value: farm.id, child: Text(farm.name)),
            )
            .toList(),
        onChanged: (value) => setState(() {
          _farmId = value;
          _fieldId = null;
        }),
        validator: (value) => value == null ? 'Farm is required' : null,
      ),
      const SizedBox(height: 14),
      DropdownButtonFormField<String>(
        key: ValueKey('plan-field-$_farmId'),
        initialValue: _fieldId,
        decoration: const InputDecoration(labelText: 'Field'),
        items: fields
            .map(
              (field) =>
                  DropdownMenuItem(value: field.id, child: Text(field.name)),
            )
            .toList(),
        onChanged: (value) => setState(() => _fieldId = value),
        validator: (value) => value == null ? 'Field is required' : null,
      ),
      const SizedBox(height: 14),
      Container(
        padding: const EdgeInsets.all(15),
        decoration: BoxDecoration(
          color: AgriColors.sage,
          borderRadius: BorderRadius.circular(15),
        ),
        child: Row(
          children: [
            const Icon(Icons.location_on_outlined, color: AgriColors.forest),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Location',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  Text(
                    selectedFarm?.location ?? 'Select a farm',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      const SizedBox(height: 14),
      FormField<String>(
        initialValue: _cropTypeId,
        validator: (value) => value == null ? 'Crop is required' : null,
        builder: (field) => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            LayoutBuilder(
              builder: (context, constraints) => DropdownMenu<String>(
                key: const ValueKey('plan-crop'),
                width: constraints.maxWidth,
                label: const Text('Crop'),
                enableFilter: true,
                requestFocusOnTap: true,
                initialSelection: _cropTypeId,
                dropdownMenuEntries: crops
                    .map(
                      (crop) =>
                          DropdownMenuEntry(value: crop.id, label: crop.name),
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
            ),
            if (field.hasError) ...[
              const SizedBox(height: 5),
              Text(
                field.errorText!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
          ],
        ),
      ),
      const SizedBox(height: 14),
      LayoutBuilder(
        builder: (context, constraints) => DropdownMenu<String>(
          key: ValueKey('plan-variety-$_cropTypeId'),
          width: constraints.maxWidth,
          label: const Text('Crop variety'),
          enableFilter: true,
          requestFocusOnTap: true,
          initialSelection: _varietyId ?? '',
          dropdownMenuEntries: [
            const DropdownMenuEntry(value: '', label: 'Not sure'),
            ...varieties.map(
              (variety) =>
                  DropdownMenuEntry(value: variety.id, label: variety.name),
            ),
          ],
          onSelected: (value) => setState(
            () => _varietyId = value?.isEmpty == true ? null : value,
          ),
        ),
      ),
    ],
  );

  Widget _seasonDatesStep() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const JourneySectionHeading(
        title: 'Shape your season',
        subtitle: 'Choose when you hope to plant and harvest.',
      ),
      const SizedBox(height: 18),
      DropdownButtonFormField<int>(
        initialValue: _season,
        decoration: const InputDecoration(labelText: 'Cultivation season'),
        items: const [
          DropdownMenuItem(value: 1, child: Text('Maha')),
          DropdownMenuItem(value: 2, child: Text('Yala')),
          DropdownMenuItem(value: 3, child: Text('Other / Off-season')),
          DropdownMenuItem(value: 0, child: Text('Not sure')),
        ],
        onChanged: (value) => setState(() => _season = value ?? 0),
      ),
      const SizedBox(height: 14),
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
      const SizedBox(height: 14),
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
    ],
  );

  Widget _historyGoalStep(List<CropTypeOption> crops) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const JourneySectionHeading(
        title: 'Your history and goal',
        subtitle: 'Helpful context for a plan that fits your field.',
      ),
      const SizedBox(height: 18),
      LayoutBuilder(
        builder: (context, constraints) => DropdownMenu<String>(
          key: const ValueKey('plan-previous-crop'),
          width: constraints.maxWidth,
          label: const Text('Previous crop (optional)'),
          enableFilter: true,
          requestFocusOnTap: true,
          initialSelection: _previousCropId ?? 'unknown',
          dropdownMenuEntries: [
            const DropdownMenuEntry(value: 'unknown', label: 'Unknown'),
            const DropdownMenuEntry(value: 'none', label: 'No previous crop'),
            ...crops.map(
              (crop) => DropdownMenuEntry(value: crop.id, label: crop.name),
            ),
          ],
          onSelected: (value) => setState(() => _previousCropId = value),
        ),
      ),
      const SizedBox(height: 18),
      Text(
        'Previous known problems (optional)',
        style: Theme.of(context).textTheme.titleMedium,
      ),
      const SizedBox(height: 9),
      Wrap(
        spacing: 8,
        runSpacing: 7,
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
      const SizedBox(height: 16),
      TextFormField(
        controller: _budgetController,
        decoration: const InputDecoration(
          labelText: 'Budget (LKR)',
          prefixIcon: Icon(Icons.payments_outlined),
        ),
        keyboardType: TextInputType.number,
        validator: _budget,
      ),
      const SizedBox(height: 14),
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
    ],
  );

  Widget _reviewStep(
    FarmOption? farm,
    FieldOption? field,
    CropTypeOption? crop,
    CropVarietyOption? variety,
    CropTypeOption? previousCrop,
  ) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const JourneySectionHeading(
        title: 'Review your plan',
        subtitle: 'Check your details before planning begins.',
      ),
      const SizedBox(height: 12),
      JourneyInfoRow(label: 'Farm', value: farm?.name ?? 'Not selected'),
      JourneyInfoRow(label: 'Field', value: field?.name ?? 'Not selected'),
      JourneyInfoRow(
        label: 'Location',
        value: farm?.location ?? 'Not available',
      ),
      JourneyInfoRow(label: 'Crop', value: crop?.name ?? 'Not selected'),
      JourneyInfoRow(label: 'Variety', value: variety?.name ?? 'Not sure'),
      JourneyInfoRow(
        label: 'Season',
        value: switch (_season) {
          1 => 'Maha',
          2 => 'Yala',
          3 => 'Other / Off-season',
          _ => 'Not sure',
        },
      ),
      JourneyInfoRow(
        label: 'Planting',
        value: journeyDate(_startController.text),
      ),
      JourneyInfoRow(
        label: 'Expected end',
        value: journeyDate(_endController.text),
      ),
      JourneyInfoRow(
        label: 'Previous crop',
        value:
            previousCrop?.name ??
            (_previousCropId == 'none' ? 'No previous crop' : 'Unknown'),
      ),
      JourneyInfoRow(
        label: 'Previous problems',
        value: _previousProblems.isEmpty
            ? 'None selected'
            : _previousProblems
                  .map((value) => _problemOptions[value] ?? value)
                  .join(', '),
      ),
      JourneyInfoRow(label: 'Budget', value: 'LKR ${_budgetController.text}'),
      const SizedBox(height: 12),
      Text('Objective', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 5),
      Text(
        _objectiveController.text.trim(),
        style: Theme.of(context).textTheme.bodyLarge,
      ),
    ],
  );

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
