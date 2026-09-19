import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

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
    final colorScheme = Theme.of(context).colorScheme;
    final selectedFarm = state.farms
        .where((farm) => farm.id == _farmId)
        .firstOrNull;

    return PopScope(
      canPop: false,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Field setup'),
          actions: [
            IconButton(
              tooltip: 'Sign out',
              onPressed: state.isBusy ? null : state.logout,
              icon: const Icon(Icons.logout),
            ),
          ],
        ),
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 560),
                child: Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const LinearProgressIndicator(value: 1),
                      const SizedBox(height: 12),
                      Text(
                        'Step 2 of 2',
                        style: Theme.of(context).textTheme.labelLarge,
                      ),
                      const SizedBox(height: 24),
                      Icon(
                        Icons.grid_view_outlined,
                        size: 48,
                        color: colorScheme.primary,
                      ),
                      const SizedBox(height: 16),
                      Text(
                        'Add your first active field',
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'The total area of active fields cannot exceed the farm area.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 24),
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
                        validator: (value) =>
                            value == null ? 'Select a farm' : null,
                      ),
                      if (selectedFarm != null) ...[
                        const SizedBox(height: 8),
                        Text(
                          '${selectedFarm.name}: total area ${selectedFarm.totalArea}',
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                      const SizedBox(height: 16),
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
                          if (name.length > 120) {
                            return 'Use 120 characters or fewer';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 16),
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
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _soilTypeController,
                        decoration: const InputDecoration(
                          labelText: 'Soil type (optional)',
                          prefixIcon: Icon(Icons.terrain_outlined),
                        ),
                        onFieldSubmitted: (_) =>
                            state.isBusy ? null : _submit(),
                        validator: (value) => (value?.length ?? 0) > 120
                            ? 'Use 120 characters or fewer'
                            : null,
                      ),
                      if (state.farms.isEmpty) ...[
                        const SizedBox(height: 16),
                        Text(
                          'No owned active farm is available. Sign out and try again.',
                          style: TextStyle(color: colorScheme.error),
                        ),
                      ],
                      if (state.error != null) ...[
                        const SizedBox(height: 16),
                        Text(
                          state.error!,
                          style: TextStyle(color: colorScheme.error),
                        ),
                      ],
                      const SizedBox(height: 24),
                      FilledButton.icon(
                        onPressed: state.isBusy || state.farms.isEmpty
                            ? null
                            : _submit,
                        icon: state.isBusy
                            ? const SizedBox.square(
                                dimension: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Icon(Icons.check_circle_outline),
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
          ),
        ),
      ),
    );
  }
}
