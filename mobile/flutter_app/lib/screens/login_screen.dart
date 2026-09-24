import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../ui/journey_widgets.dart';
import 'register_farmer_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _obscurePassword = true;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    await context.read<AppState>().login(
      _emailController.text.trim(),
      _passwordController.text,
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return JourneyAuthLayout(
      eyebrow: 'WELCOME BACK',
      title: 'Your farm, in focus.',
      subtitle:
          'Sign in to follow your plans, stay ahead of tasks, and grow with confidence.',
      child: JourneyCard(
        child: Form(
          key: _formKey,
          child: AutofillGroup(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('Account access', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 18),
                TextFormField(
                  controller: _emailController,
                  decoration: const InputDecoration(
                    labelText: 'Email',
                    prefixIcon: Icon(Icons.mail_outline_rounded),
                  ),
                  keyboardType: TextInputType.emailAddress,
                  autofillHints: const [AutofillHints.email],
                  textInputAction: TextInputAction.next,
                  validator: (value) {
                    final email = value?.trim() ?? '';
                    if (email.isEmpty) return 'Email is required';
                    if (!email.contains('@')) {
                      return 'Enter a valid email address';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _passwordController,
                  decoration: InputDecoration(
                    labelText: 'Password',
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
                  autofillHints: const [AutofillHints.password],
                  onFieldSubmitted: (_) => state.isBusy ? null : _submit(),
                  validator: (value) => value == null || value.isEmpty
                      ? 'Password is required'
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
                FilledButton(
                  onPressed: state.isBusy ? null : _submit,
                  child: Text(state.isBusy ? 'Signing in...' : 'Sign in'),
                ),
                const SizedBox(height: 10),
                TextButton(
                  onPressed: state.isBusy
                      ? null
                      : () => Navigator.of(context).push(
                          MaterialPageRoute<void>(
                            builder: (_) => const RegisterFarmerScreen(),
                          ),
                        ),
                  child: const Text('Create farmer account'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
