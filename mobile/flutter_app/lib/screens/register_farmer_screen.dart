import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../ui/journey_widgets.dart';
import '../utils/password_validation.dart';

class RegisterFarmerScreen extends StatefulWidget {
  const RegisterFarmerScreen({super.key});

  @override
  State<RegisterFarmerScreen> createState() => _RegisterFarmerScreenState();
}

class _RegisterFarmerScreenState extends State<RegisterFarmerScreen> {
  final _formKey = GlobalKey<FormState>();
  final _fullNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _obscurePassword = true;

  @override
  void dispose() {
    _fullNameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final state = context.read<AppState>();
    await state.registerFarmer(
      fullName: _fullNameController.text.trim(),
      email: _emailController.text.trim(),
      password: _passwordController.text,
    );
    if (!mounted) return;
    if (state.isAuthenticated) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return JourneyAuthLayout(
      showBack: true,
      eyebrow: 'GET STARTED',
      title: 'Start with your farm',
      subtitle:
          'Create your farmer account. Your farm and first field come next.',
      child: JourneyCard(
        child: AutofillGroup(
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Your details',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 18),
                TextFormField(
                  controller: _fullNameController,
                  decoration: const InputDecoration(
                    labelText: 'Full name',
                    prefixIcon: Icon(Icons.person_outline_rounded),
                  ),
                  autofillHints: const [AutofillHints.name],
                  textInputAction: TextInputAction.next,
                  validator: (value) {
                    final name = value?.trim() ?? '';
                    if (name.isEmpty) return 'Full name is required';
                    if (name.length > 120) return 'Use 120 characters or fewer';
                    return null;
                  },
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _emailController,
                  decoration: const InputDecoration(
                    labelText: 'Email',
                    prefixIcon: Icon(Icons.mail_outline_rounded),
                  ),
                  keyboardType: TextInputType.emailAddress,
                  autofillHints: const [AutofillHints.newUsername],
                  textInputAction: TextInputAction.next,
                  validator: (value) {
                    final email = value?.trim() ?? '';
                    if (email.isEmpty) return 'Email is required';
                    if (!email.contains('@')) {
                      return 'Enter a valid email address';
                    }
                    if (email.length > 180) {
                      return 'Use 180 characters or fewer';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _passwordController,
                  decoration: InputDecoration(
                    labelText: 'Password',
                    helperText:
                        'At least 12 characters; long passphrases are supported.',
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
                    fullName: _fullNameController.text,
                    email: _emailController.text,
                  ),
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _confirmPasswordController,
                  decoration: const InputDecoration(
                    labelText: 'Confirm password',
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
                    state.isBusy
                        ? 'Creating account...'
                        : 'Create farmer account',
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
