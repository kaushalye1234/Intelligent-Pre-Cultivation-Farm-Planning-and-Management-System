import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'screens/change_temporary_password_screen.dart';
import 'screens/farm_onboarding_screen.dart';
import 'screens/field_onboarding_screen.dart';
import 'screens/home_shell.dart';
import 'screens/login_screen.dart';
import 'state/app_state.dart';

void main() {
  runApp(const AgriAssistMobileApp());
}

class AgriAssistMobileApp extends StatelessWidget {
  const AgriAssistMobileApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => AppState()..restoreSession(),
      child: MaterialApp(
        title: 'AgriAssist Mobile',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF1F7A4A)),
          useMaterial3: true,
          inputDecorationTheme: const InputDecorationTheme(
            border: OutlineInputBorder(),
          ),
          cardTheme: const CardThemeData(
            margin: EdgeInsets.symmetric(vertical: 8),
          ),
        ),
        home: const AuthGate(),
      ),
    );
  }
}

class AuthGate extends StatelessWidget {
  const AuthGate({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    if (state.isBusy &&
        !state.isAuthenticated &&
        !state.requiresTemporaryPasswordChange) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    if (state.requiresTemporaryPasswordChange) {
      return const ChangeTemporaryPasswordScreen();
    }

    if (!state.isAuthenticated) return const LoginScreen();

    if (state.user?.role == 1) {
      return switch (state.farmerOnboarding?.stage) {
        'farm' => const FarmOnboardingScreen(),
        'field' => const FieldOnboardingScreen(),
        'complete' => const HomeShell(),
        _ => const _OnboardingStatusUnavailable(),
      };
    }

    return const HomeShell();
  }
}

class _OnboardingStatusUnavailable extends StatelessWidget {
  const _OnboardingStatusUnavailable();

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Farmer setup'),
        actions: [
          IconButton(
            tooltip: 'Sign out',
            onPressed: state.isBusy ? null : state.logout,
            icon: const Icon(Icons.logout),
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.cloud_off_outlined, size: 48),
                  const SizedBox(height: 16),
                  Text(
                    'We could not load your setup status.',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  if (state.error != null) ...[
                    const SizedBox(height: 8),
                    Text(state.error!, textAlign: TextAlign.center),
                  ],
                  const SizedBox(height: 24),
                  FilledButton.icon(
                    onPressed: state.isBusy
                        ? null
                        : state.retryAuthenticatedLanding,
                    icon: state.isBusy
                        ? const SizedBox.square(
                            dimension: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.refresh),
                    label: const Text('Try again'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
