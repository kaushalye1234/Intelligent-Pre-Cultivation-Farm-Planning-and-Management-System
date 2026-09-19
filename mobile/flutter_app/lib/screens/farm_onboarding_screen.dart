import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

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
    final colorScheme = Theme.of(context).colorScheme;

    return PopScope(
      canPop: false,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Farm setup'),
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
                      const LinearProgressIndicator(value: 0.5),
                      const SizedBox(height: 12),
                      Text(
                        'Step 1 of 2',
                        style: Theme.of(context).textTheme.labelLarge,
                      ),
                      const SizedBox(height: 24),
                      Icon(
                        Icons.landscape_outlined,
                        size: 48,
                        color: colorScheme.primary,
                      ),
                      const SizedBox(height: 16),
                      Text(
                        'Add your first farm',
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'Farm ownership is assigned to your signed-in Farmer account. Next, you will add an active field.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 24),
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
                          if (name.length > 120) {
                            return 'Use 120 characters or fewer';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 16),
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
                      const SizedBox(height: 16),
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
                        Text(
                          state.error!,
                          style: TextStyle(color: colorScheme.error),
                        ),
                      ],
                      const SizedBox(height: 24),
                      FilledButton.icon(
                        onPressed: state.isBusy ? null : _submit,
                        icon: state.isBusy
                            ? const SizedBox.square(
                                dimension: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Icon(Icons.arrow_forward),
                        label: Text(
                          state.isBusy
                              ? 'Saving farm...'
                              : 'Save farm and continue',
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
