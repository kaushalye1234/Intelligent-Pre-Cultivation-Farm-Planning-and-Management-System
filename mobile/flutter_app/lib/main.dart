import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'screens/change_temporary_password_screen.dart';
import 'screens/farm_onboarding_screen.dart';
import 'screens/field_onboarding_screen.dart';
import 'screens/home_shell.dart';
import 'screens/login_screen.dart';
import 'state/app_state.dart';
import 'ui/agri_theme.dart';
import 'ui/journey_widgets.dart';

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
        theme: AgriTheme.light(),
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

    if (state.passwordChangeSession?.user.role != null &&
        state.passwordChangeSession?.user.role != 1) {
      return const _StaffPortalRequired();
    }

    if (state.requiresTemporaryPasswordChange) {
      return const ChangeTemporaryPasswordScreen();
    }

    if (!state.isAuthenticated) return const LoginScreen();

    if (state.user?.role != 1) return const _StaffPortalRequired();

    return switch (state.farmerOnboarding?.stage) {
      'farm' => const FarmOnboardingScreen(),
      'field' => const FieldOnboardingScreen(),
      'complete' => const HomeShell(),
      _ => const _OnboardingStatusUnavailable(),
    };
  }
}

class _StaffPortalRequired extends StatelessWidget {
  const _StaffPortalRequired();

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return JourneyAuthLayout(
      eyebrow: 'ACCOUNT TYPE',
      title: 'Staff portal required',
      subtitle: 'This account is for staff. Please use the React Staff Portal.',
      child: JourneyCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Icon(
              Icons.admin_panel_settings_outlined,
              color: AgriColors.forest,
              size: 38,
            ),
            const SizedBox(height: 12),
            Text(
              'Your staff workspace is available in the web portal.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 18),
            OutlinedButton.icon(
              onPressed: state.logout,
              icon: const Icon(Icons.logout_rounded),
              label: const Text('Return to sign in'),
            ),
          ],
        ),
      ),
    );
  }
}

class _OnboardingStatusUnavailable extends StatelessWidget {
  const _OnboardingStatusUnavailable();

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return JourneyAuthLayout(
      eyebrow: 'FARM SETUP',
      title: 'Setup status unavailable',
      subtitle: 'We could not load your setup status.',
      actions: [
        IconButton(
          tooltip: 'Sign out',
          onPressed: state.isBusy ? null : state.logout,
          icon: const Icon(Icons.logout_rounded),
        ),
      ],
      child: JourneyEmptyState(
        icon: Icons.cloud_off_outlined,
        title: 'Your setup is still here',
        message: state.error ?? 'Try again to continue setting up your farm.',
        action: FilledButton.icon(
          onPressed: state.isBusy ? null : state.retryAuthenticatedLanding,
          icon: state.isBusy
              ? const SizedBox.square(
                  dimension: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.refresh_rounded),
          label: const Text('Try again'),
        ),
      ),
    );
  }
}
