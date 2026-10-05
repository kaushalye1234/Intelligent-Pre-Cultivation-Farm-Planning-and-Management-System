import 'package:agriassist_mobile/utils/farmer_registration_validation.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('valid Farmer full name', () {
    for (final name in ['Nimal Perera', 'Anne-Marie Silva', "D'Souza"]) {
      expect(validateFarmerFullName(name), isNull);
    }
  });

  test('invalid Farmer full names', () {
    expect(validateFarmerFullName(''), 'Full name is required');
    for (final name in ['22222', 'Nimal2 Perera', 'Nimal@Perera', '-Nimal']) {
      expect(
        validateFarmerFullName(name),
        'Use letters, spaces, apostrophes, and hyphens only',
      );
    }
  });
}
