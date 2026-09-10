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

  @override
  void dispose() {
    _startController.dispose();
    _endController.dispose();
    _budgetController.dispose();
    _objectiveController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate() || _farmId == null || _cropTypeId == null) {
      return;
    }

    await context.read<AppState>().createPreliminaryCropPlan(
          farmId: _farmId!,
          fieldId: _fieldId,
          cropTypeId: _cropTypeId!,
          startDate: _startController.text,
          endDate: _endController.text,
          budget: num.parse(_budgetController.text),
          objective: _objectiveController.text,
        );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Crop plan request', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 12),
        Form(
          key: _formKey,
          child: Column(
            children: [
              DropdownButtonFormField<String>(
                initialValue: _farmId,
                decoration: const InputDecoration(labelText: 'Farm'),
                items: state.farms.map((farm) => DropdownMenuItem(value: farm.id, child: Text(farm.name))).toList(),
                onChanged: (value) => setState(() => _farmId = value),
                validator: (value) => value == null ? 'Farm is required' : null,
              ),
              DropdownButtonFormField<String>(
                initialValue: _fieldId,
                decoration: const InputDecoration(labelText: 'Field'),
                items: state.fields.map((field) => DropdownMenuItem(value: field.id, child: Text(field.name))).toList(),
                onChanged: (value) => setState(() => _fieldId = value),
              ),
              DropdownButtonFormField<String>(
                initialValue: _cropTypeId,
                decoration: const InputDecoration(labelText: 'Crop type'),
                items: state.cropTypes.map((crop) => DropdownMenuItem(value: crop.id, child: Text(crop.name))).toList(),
                onChanged: (value) => setState(() => _cropTypeId = value),
                validator: (value) => value == null ? 'Crop type is required' : null,
              ),
              TextFormField(controller: _startController, decoration: const InputDecoration(labelText: 'Start date YYYY-MM-DD'), validator: _required),
              TextFormField(controller: _endController, decoration: const InputDecoration(labelText: 'End date YYYY-MM-DD'), validator: _required),
              TextFormField(controller: _budgetController, decoration: const InputDecoration(labelText: 'Budget'), keyboardType: TextInputType.number, validator: _required),
              TextFormField(controller: _objectiveController, decoration: const InputDecoration(labelText: 'Objective'), validator: _required),
              const SizedBox(height: 16),
              FilledButton.icon(onPressed: state.isBusy ? null : _submit, icon: const Icon(Icons.auto_awesome), label: const Text('Generate preliminary request')),
            ],
          ),
        ),
      ],
    );
  }

  String? _required(String? value) => value == null || value.trim().isEmpty ? 'Required' : null;
}