import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../utils/password_validation.dart';

class ChangeTemporaryPasswordScreen extends StatefulWidget {
  const ChangeTemporaryPasswordScreen({super.key});

  @override
  State<ChangeTemporaryPasswordScreen> createState() =>
      _ChangeTemporaryPasswordScreenState();
}

class _ChangeTemporaryPasswordScreenState
    extends State<ChangeTemporaryPasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _obscurePassword = true;

  @override
  void dispose() {
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    await context.read<AppState>().changeTemporaryPassword(
      _passwordController.text,
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final account = state.passwordChangeSession?.user;
    final colorScheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Secure your account')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Icon(
                      Icons.security_outlined,
                      size: 48,
                      color: colorScheme.primary,
                    ),
                    const SizedBox(height: 16),
                    Text(
                      'Replace your temporary password',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'This one-time step is required before the rest of AgriAssist can be opened.',
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 24),
                    TextFormField(
                      controller: _passwordController,
                      decoration: InputDecoration(
                        labelText: 'New password',
                        helperText:
                            'Use at least 12 characters. Passphrases are welcome.',
                        helperMaxLines: 2,
                        prefixIcon: const Icon(Icons.lock_outline),
                        suffixIcon: IconButton(
                          tooltip: _obscurePassword
                              ? 'Show password'
                              : 'Hide password',
                          onPressed: () => setState(
                            () => _obscurePassword = !_obscurePassword,
                          ),
                          icon: Icon(
                            _obscurePassword
                                ? Icons.visibility_outlined
                                : Icons.visibility_off_outlined,
                          ),
                        ),
                      ),
                      obscureText: _obscurePassword,
                      autofillHints: const [AutofillHints.newPassword],
                      textInputAction: TextInputAction.next,
                      validator: (value) => validateAccountPassword(
                        value,
                        fullName: account?.fullName ?? '',
                        email: account?.email ?? '',
                      ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _confirmPasswordController,
                      decoration: const InputDecoration(
                        labelText: 'Confirm new password',
                        prefixIcon: Icon(Icons.lock_reset_outlined),
                      ),
                      obscureText: _obscurePassword,
                      autofillHints: const [AutofillHints.newPassword],
                      onFieldSubmitted: (_) => state.isBusy ? null : _submit(),
                      validator: (value) => value != _passwordController.text
                          ? 'Passwords do not match'
                          : null,
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
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.check_circle_outline),
                      label: Text(
                        state.isBusy
                            ? 'Saving password...'
                            : 'Save password and continue',
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextButton(
                      onPressed: state.isBusy
                          ? null
                          : state.cancelTemporaryPasswordChange,
                      child: const Text('Return to sign in'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
