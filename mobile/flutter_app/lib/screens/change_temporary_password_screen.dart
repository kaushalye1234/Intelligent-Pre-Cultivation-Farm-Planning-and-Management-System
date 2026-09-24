import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../ui/journey_widgets.dart';
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
    return JourneyAuthLayout(
      eyebrow: 'ACCOUNT SECURITY',
      title: 'Replace your temporary password',
      subtitle: 'Secure your account before continuing to your farm.',
      child: JourneyCard(
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Set a new password',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 18),
              TextFormField(
                controller: _passwordController,
                decoration: InputDecoration(
                  labelText: 'New password',
                  helperText:
                      'Use at least 12 characters. Passphrases are welcome.',
                  helperMaxLines: 2,
                  prefixIcon: const Icon(Icons.lock_outline_rounded),
                  suffixIcon: IconButton(
                    tooltip: _obscurePassword
                        ? 'Show password'
                        : 'Hide password',
                    onPressed: () =>
                        setState(() => _obscurePassword = !_obscurePassword),
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
              const SizedBox(height: 14),
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
                JourneyNotice(message: state.error!, tone: JourneyTone.danger),
              ],
              const SizedBox(height: 22),
              FilledButton.icon(
                onPressed: state.isBusy ? null : _submit,
                icon: state.isBusy
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.check_circle_outline_rounded),
                label: Text(
                  state.isBusy
                      ? 'Saving password...'
                      : 'Save password and continue',
                ),
              ),
              const SizedBox(height: 10),
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
    );
  }
}
