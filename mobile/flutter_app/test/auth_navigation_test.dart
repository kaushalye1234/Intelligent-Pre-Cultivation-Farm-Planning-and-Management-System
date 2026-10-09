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

Future<void> _openFarmerRegistration(WidgetTester tester) async {
  await tester.pumpWidget(_authGateHarness(AppState()));
  await tester.ensureVisible(find.text('Create farmer account'));
  await tester.tap(find.text('Create farmer account'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('Farmer shell exposes Home, Plans and Tasks', (tester) async {
    final state = AppState()..user = _farmer;

    await tester.pumpWidget(
      ChangeNotifierProvider.value(
        value: state,
        child: const MaterialApp(home: HomeShell()),
      ),
    );

    expect(find.text('Home'), findsOneWidget);
    expect(find.text('Plans'), findsOneWidget);
    expect(find.text('Tasks'), findsOneWidget);
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

    await tester.ensureVisible(find.text('Create farmer account'));
    await tester.tap(find.text('Create farmer account'));
    await tester.pumpAndSettle();

    expect(find.text('Start with your farm'), findsOneWidget);
    expect(find.text('Full name'), findsOneWidget);
    expect(find.text('Phone number'), findsOneWidget);
    expect(find.text('Contact / home address'), findsOneWidget);
    expect(find.text('Role'), findsNothing);
    expect(find.byType(DropdownButtonFormField<int>), findsNothing);
  });

  testWidgets('new Farmer registration requires phone and contact address', (
    tester,
  ) async {
    await tester.pumpWidget(_authGateHarness(AppState()));
    await tester.ensureVisible(find.text('Create farmer account'));
    await tester.tap(find.text('Create farmer account'));
    await tester.pumpAndSettle();

    final submit = find.widgetWithText(FilledButton, 'Create farmer account');
    await tester.ensureVisible(submit);
    await tester.tap(submit);
    await tester.pump();

    expect(find.text('Phone number is required'), findsOneWidget);
    expect(find.text('Contact / home address is required'), findsOneWidget);
  });

  testWidgets('Farmer registration filters invalid name and phone input', (
    tester,
  ) async {
    await _openFarmerRegistration(tester);
    final nameFinder = find.byKey(const Key('farmerFullNameField'));
    final phoneFinder = find.byKey(const Key('farmerPhoneField'));

    await tester.enterText(nameFinder, 'Nimal2@ Perera');
    await tester.enterText(phoneFinder, '077 123-AB4567');

    expect(
      tester.widget<TextFormField>(nameFinder).controller!.text,
      'Nimal Perera',
    );
    expect(
      tester.widget<TextFormField>(phoneFinder).controller!.text,
      '0771234567',
    );
  });

  testWidgets('Farmer registration validators reject autofill bypass values', (
    tester,
  ) async {
    await _openFarmerRegistration(tester);
    final nameField = tester.widget<TextFormField>(
      find.byKey(const Key('farmerFullNameField')),
    );
    final phoneField = tester.widget<TextFormField>(
      find.byKey(const Key('farmerPhoneField')),
    );
    nameField.controller!.text = 'Nimal2 Perera';
    phoneField.controller!.text = '+94771234567';

    final submit = find.widgetWithText(FilledButton, 'Create farmer account');
    await tester.ensureVisible(submit);
    await tester.tap(submit);
    await tester.pump();

    expect(
      find.text('Use letters, spaces, apostrophes, and hyphens only'),
      findsOneWidget,
    );
    expect(find.text('Enter a valid Sri Lankan phone number'), findsOneWidget);
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
    expect(find.text('STEP 1 OF 2'), findsOneWidget);
    expect(find.text('District'), findsWidgets);
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
    expect(find.text('STEP 2 OF 2'), findsOneWidget);
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
