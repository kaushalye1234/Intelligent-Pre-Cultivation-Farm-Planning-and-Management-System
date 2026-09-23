import 'package:agriassist_mobile/main.dart';
import 'package:agriassist_mobile/models/api_models.dart';
import 'package:agriassist_mobile/screens/home_shell.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:agriassist_mobile/utils/password_validation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

const _farmer = UserProfile(
  id: 'farmer-1',
  fullName: 'Taylor Farmer',
  email: 'taylor@example.test',
  role: 1,
  isActive: true,
);

Widget _authGateHarness(AppState state) {
  return ChangeNotifierProvider.value(
    value: state,
    child: const MaterialApp(home: AuthGate()),
  );
}

void main() {
  testWidgets('Farmer shell exposes only Dashboard, Plans and My status', (
    tester,
  ) async {
    final state = AppState()..user = _farmer;

    await tester.pumpWidget(
      ChangeNotifierProvider.value(
        value: state,
        child: const MaterialApp(home: HomeShell()),
      ),
    );

    expect(find.text('Dashboard'), findsOneWidget);
    expect(find.text('Plans'), findsOneWidget);
    expect(find.text('My status'), findsOneWidget);
    expect(find.text('Inspect'), findsNothing);
    expect(find.text('Resources'), findsNothing);
  });

  testWidgets('staff profile cannot enter the Farmer application shell', (
    tester,
  ) async {
    final state = AppState()
      ..user = const UserProfile(
        id: 'officer-1',
        fullName: 'Field Officer',
        email: 'officer@example.test',
        role: 2,
        isActive: true,
      );

    await tester.pumpWidget(_authGateHarness(state));

    expect(
      find.text(
        'This account is for staff. Please use the React Staff Portal.',
      ),
      findsOneWidget,
    );
    expect(find.byType(HomeShell), findsNothing);
  });

  testWidgets('login exposes public Farmer registration with no role input', (
    tester,
  ) async {
    await tester.pumpWidget(_authGateHarness(AppState()));

    await tester.tap(find.text('Create farmer account'));
    await tester.pumpAndSettle();

    expect(find.text('Start with your farm'), findsOneWidget);
    expect(find.text('Full name'), findsOneWidget);
    expect(find.text('Role'), findsNothing);
    expect(find.byType(DropdownButtonFormField<int>), findsNothing);
  });

  testWidgets('staff temporary session is blocked from Farmer screens', (
    tester,
  ) async {
    final state = AppState()
      ..passwordChangeSession = const AuthenticationSession(
        authenticationStatus: 'passwordChangeRequired',
        passwordChangeToken: 'temporary-token',
        user: UserProfile(
          id: 'officer-1',
          fullName: 'Field Officer',
          email: 'officer@example.test',
          role: 2,
          isActive: true,
          mustChangePassword: true,
        ),
      );

    await tester.pumpWidget(_authGateHarness(state));

    expect(
      find.text(
        'This account is for staff. Please use the React Staff Portal.',
      ),
      findsOneWidget,
    );
    expect(find.text('Replace your temporary password'), findsNothing);
    expect(find.text('Dashboard'), findsNothing);
    expect(find.text('Return to sign in'), findsOneWidget);
  });

  testWidgets('Farmer without an owned farm opens farm onboarding', (
    tester,
  ) async {
    final state = AppState()
      ..user = _farmer
      ..farmerOnboarding = const FarmerOnboardingStatus(
        stage: 'farm',
        activeFarmCount: 0,
        activeFieldCount: 0,
      );

    await tester.pumpWidget(_authGateHarness(state));

    expect(find.text('Add your first farm'), findsOneWidget);
    expect(find.text('Step 1 of 2'), findsOneWidget);
  });

  testWidgets('Farmer with a farm but no active field opens field onboarding', (
    tester,
  ) async {
    final state = AppState()
      ..user = _farmer
      ..farmerOnboarding = const FarmerOnboardingStatus(
        stage: 'field',
        activeFarmCount: 1,
        activeFieldCount: 0,
      )
      ..farms = const [
        FarmOption(
          id: 'farm-1',
          name: 'North Farm',
          location: 'North',
          totalArea: 12,
        ),
      ];

    await tester.pumpWidget(_authGateHarness(state));

    expect(find.text('Add your first active field'), findsOneWidget);
    expect(find.text('Step 2 of 2'), findsOneWidget);
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    expect(find.text('North Farm'), findsOneWidget);
  });

  test('password validation enforces length, identifiers and BCrypt bytes', () {
    expect(
      validateAccountPassword(
        'short',
        fullName: 'Taylor Farmer',
        email: 'taylor@example.test',
      ),
      'Use at least 12 characters',
    );
    expect(
      validateAccountPassword(
        'taylor@example.test',
        fullName: 'Taylor Farmer',
        email: 'taylor@example.test',
      ),
      'Password cannot be your email address',
    );
    expect(
      validateAccountPassword(
        'a' * 73,
        fullName: 'Taylor Farmer',
        email: 'taylor@example.test',
      ),
      'Password must be 72 UTF-8 bytes or fewer',
    );
    expect(
      validateAccountPassword(
        'a long farm passphrase',
        fullName: 'Taylor Farmer',
        email: 'taylor@example.test',
      ),
      isNull,
    );
  });
}
