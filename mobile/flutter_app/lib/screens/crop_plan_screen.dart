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
    if (!_formKey.currentState!.validate() || _farmId == null || _cropTypeId == null) {
      return;
    }

    await context.read<AppState>().createAndStartAiCropPlan(
          farmId: _farmId!,
          fieldId: _fieldId,
          cropTypeId: _cropTypeId!,
          startDate: _startController.text,
          endDate: _endController.text,
          budget: num.parse(_budgetController.text),
          objective: _objectiveController.text.trim(),
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
        padding: const EdgeInsets.all(16),
        children: [
          Text('AI crop planning', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Form(
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
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: _fieldId,
                      decoration: const InputDecoration(labelText: 'Field'),
                      items: state.fields.map((field) => DropdownMenuItem(value: field.id, child: Text(field.name))).toList(),
                      onChanged: (value) => setState(() => _fieldId = value),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: _cropTypeId,
                      decoration: const InputDecoration(labelText: 'Crop type'),
                      items: state.cropTypes.map((crop) => DropdownMenuItem(value: crop.id, child: Text(crop.name))).toList(),
                      onChanged: (value) => setState(() => _cropTypeId = value),
                      validator: (value) => value == null ? 'Crop type is required' : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _startController,
                      readOnly: true,
                      decoration: const InputDecoration(labelText: 'Preferred start date', prefixIcon: Icon(Icons.event_outlined)),
                      validator: _required,
                      onTap: () => _pickDate(_startController),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _endController,
                      readOnly: true,
                      decoration: const InputDecoration(labelText: 'Preferred end date', prefixIcon: Icon(Icons.event_available_outlined)),
                      validator: _required,
                      onTap: () => _pickDate(_endController),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _budgetController,
                      decoration: const InputDecoration(labelText: 'Budget', prefixIcon: Icon(Icons.payments_outlined)),
                      keyboardType: TextInputType.number,
                      validator: _budget,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _objectiveController,
                      decoration: const InputDecoration(labelText: 'Objective', prefixIcon: Icon(Icons.flag_outlined)),
                      minLines: 2,
                      maxLines: 4,
                      validator: _required,
                    ),
                    const SizedBox(height: 16),
                    FilledButton.icon(
                      onPressed: state.isBusy ? null : _submit,
                      icon: const Icon(Icons.auto_awesome),
                      label: Text(state.isBusy ? 'Starting AI planning...' : 'Submit and start AI planning'),
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

  String? _required(String? value) => value == null || value.trim().isEmpty ? 'Required' : null;

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
          subtitle: Text('Submit a crop plan request to start coordinator planning.'),
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
                Icon(Icons.account_tree_outlined, color: Theme.of(context).colorScheme.primary),
                const SizedBox(width: 8),
                Expanded(child: Text('Workflow status', style: Theme.of(context).textTheme.titleMedium)),
                if (status != null) Chip(label: Text(status.statusLabel)),
              ],
            ),
            if (status != null) ...[
              const SizedBox(height: 8),
              Text('Current step: ${status.currentStep.isEmpty ? 'Pending' : status.currentStep}'),
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
                      Chip(label: Text('${step.sequence}. ${step.assignedAgent}')),
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
                      Icon(Icons.warning_amber_outlined, size: 18, color: Theme.of(context).colorScheme.error),
                      const SizedBox(width: 6),
                      Expanded(child: Text(warning)),
                    ],
                  ),
                ),
            ],
            const SizedBox(height: 12),
            const Text('Final execution status will appear after downstream agents and officer approval.'),
          ],
        ),
      ),
    );
  }
}
