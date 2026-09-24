import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../ui/journey_widgets.dart';

class FarmOnboardingScreen extends StatefulWidget {
  const FarmOnboardingScreen({super.key});

  @override
  State<FarmOnboardingScreen> createState() => _FarmOnboardingScreenState();
}

class _FarmOnboardingScreenState extends State<FarmOnboardingScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _locationController = TextEditingController();
  final _areaController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _locationController.dispose();
    _areaController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    await context.read<AppState>().createOnboardingFarm(
      name: _nameController.text.trim(),
      location: _locationController.text.trim(),
      totalArea: num.parse(_areaController.text.trim()),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return PopScope(
      canPop: false,
      child: JourneyAuthLayout(
        eyebrow: 'FARM SETUP',
        progressLabel: 'Step 1 of 2',
        progress: 0.5,
        title: 'Add your first farm',
        subtitle:
            'A few details help us shape planning around your land. Your first field comes next.',
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
                  'Farm details',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 18),
                TextFormField(
                  controller: _nameController,
                  decoration: const InputDecoration(
                    labelText: 'Farm name',
                    prefixIcon: Icon(Icons.grass_outlined),
                  ),
                  textInputAction: TextInputAction.next,
                  validator: (value) {
                    final name = value?.trim() ?? '';
                    if (name.isEmpty) return 'Farm name is required';
                    if (name.length > 120) return 'Use 120 characters or fewer';
                    return null;
                  },
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _locationController,
                  decoration: const InputDecoration(
                    labelText: 'Location',
                    prefixIcon: Icon(Icons.location_on_outlined),
                  ),
                  textInputAction: TextInputAction.next,
                  validator: (value) {
                    final location = value?.trim() ?? '';
                    if (location.isEmpty) return 'Location is required';
                    if (location.length > 240) {
                      return 'Use 240 characters or fewer';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _areaController,
                  decoration: const InputDecoration(
                    labelText: 'Total farm area',
                    helperText:
                        'Use the same area unit for the farm and its fields.',
                    prefixIcon: Icon(Icons.square_foot_outlined),
                  ),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  validator: (value) {
                    final area = num.tryParse(value?.trim() ?? '');
                    if (area == null || area <= 0) {
                      return 'Enter a positive farm area';
                    }
                    return null;
                  },
                ),
                if (state.error != null) ...[
                  const SizedBox(height: 16),
                  JourneyNotice(
                    message: state.error!,
                    tone: JourneyTone.danger,
                  ),
                ],
                const SizedBox(height: 22),
                FilledButton.icon(
                  onPressed: state.isBusy ? null : _submit,
                  icon: state.isBusy
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.arrow_forward_rounded),
                  label: Text(
                    state.isBusy ? 'Saving farm...' : 'Save farm and continue',
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
