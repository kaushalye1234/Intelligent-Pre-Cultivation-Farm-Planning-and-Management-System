import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../ui/journey_widgets.dart';

class FieldOnboardingScreen extends StatefulWidget {
  const FieldOnboardingScreen({super.key});

  @override
  State<FieldOnboardingScreen> createState() => _FieldOnboardingScreenState();
}

class _FieldOnboardingScreenState extends State<FieldOnboardingScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _areaController = TextEditingController();
  final _soilTypeController = TextEditingController();
  String? _farmId;

  @override
  void dispose() {
    _nameController.dispose();
    _areaController.dispose();
    _soilTypeController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    await context.read<AppState>().createOnboardingField(
      farmId: _farmId!,
      name: _nameController.text.trim(),
      area: num.parse(_areaController.text.trim()),
      soilType: _soilTypeController.text.trim(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final selectedFarm = state.farms
        .where((farm) => farm.id == _farmId)
        .firstOrNull;
    return PopScope(
      canPop: false,
      child: JourneyAuthLayout(
        eyebrow: 'FIELD SETUP',
        progressLabel: 'Step 2 of 2',
        progress: 1,
        title: 'Add your first active field',
        subtitle:
            'Fields help keep each crop plan and task tied to the right place.',
        actions: [
          IconButton(
            tooltip: 'Sign out',
            onPressed: state.isBusy ? null : state.logout,
            icon: const Icon(Icons.logout_rounded),
          ),
        ],
        child: JourneyCard(
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Field details',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 18),
                DropdownButtonFormField<String>(
                  initialValue: _farmId,
                  decoration: const InputDecoration(
                    labelText: 'Farm',
                    prefixIcon: Icon(Icons.landscape_outlined),
                  ),
                  items: state.farms
                      .map(
                        (farm) => DropdownMenuItem(
                          value: farm.id,
                          child: Text(farm.name),
                        ),
                      )
                      .toList(),
                  onChanged: state.isBusy
                      ? null
                      : (value) => setState(() => _farmId = value),
                  validator: (value) => value == null ? 'Select a farm' : null,
                ),
                if (selectedFarm != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    'Available farm area: ${selectedFarm.totalArea}',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                ],
                const SizedBox(height: 14),
                TextFormField(
                  controller: _nameController,
                  decoration: const InputDecoration(
                    labelText: 'Field name',
                    prefixIcon: Icon(Icons.grass_outlined),
                  ),
                  textInputAction: TextInputAction.next,
                  validator: (value) {
                    final name = value?.trim() ?? '';
                    if (name.isEmpty) return 'Field name is required';
                    if (name.length > 120) return 'Use 120 characters or fewer';
                    return null;
                  },
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _areaController,
                  decoration: const InputDecoration(
                    labelText: 'Field area',
                    helperText: 'Use the same unit entered for the farm.',
                    prefixIcon: Icon(Icons.square_foot_outlined),
                  ),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  textInputAction: TextInputAction.next,
                  validator: (value) {
                    final area = num.tryParse(value?.trim() ?? '');
                    if (area == null || area <= 0) {
                      return 'Enter a positive field area';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _soilTypeController,
                  decoration: const InputDecoration(
                    labelText: 'Soil type (optional)',
                    prefixIcon: Icon(Icons.terrain_outlined),
                  ),
                  onFieldSubmitted: (_) => state.isBusy ? null : _submit(),
                  validator: (value) => (value?.length ?? 0) > 120
                      ? 'Use 120 characters or fewer'
                      : null,
                ),
                if (state.farms.isEmpty) ...[
                  const SizedBox(height: 16),
                  const JourneyNotice(
                    message:
                        'No owned active farm is available. Sign out and try again.',
                    tone: JourneyTone.danger,
                  ),
                ],
                if (state.error != null) ...[
                  const SizedBox(height: 16),
                  JourneyNotice(
                    message: state.error!,
                    tone: JourneyTone.danger,
                  ),
                ],
                const SizedBox(height: 22),
                FilledButton.icon(
                  onPressed: state.isBusy || state.farms.isEmpty
                      ? null
                      : _submit,
                  icon: state.isBusy
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.check_circle_outline_rounded),
                  label: Text(
                    state.isBusy
                        ? 'Saving field...'
                        : 'Save field and open dashboard',
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
