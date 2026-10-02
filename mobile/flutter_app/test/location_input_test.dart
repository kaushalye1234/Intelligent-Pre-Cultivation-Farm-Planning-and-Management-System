import 'package:agriassist_mobile/constants/sri_lankan_districts.dart';
import 'package:agriassist_mobile/models/api_models.dart';
import 'package:agriassist_mobile/ui/sri_lankan_district_field.dart';
import 'package:agriassist_mobile/utils/sri_lankan_phone_validation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Sri Lankan phone inputs normalize to the canonical +94 format', () {
    for (final input in [
      '0771234567',
      '077 123 4567',
      '077-123-4567',
      '+94771234567',
      '+94 77 123 4567',
    ]) {
      expect(normalizeSriLankanPhoneNumber(input), '+94771234567');
      expect(validateSriLankanPhoneNumber(input), isNull);
    }

    expect(validateSriLankanPhoneNumber(''), 'Phone number is required');
    expect(
      validateSriLankanPhoneNumber('12345'),
      'Enter a valid Sri Lankan phone number',
    );
  });

  test('district catalog contains exactly the official 25 values', () {
    expect(sriLankanDistricts, hasLength(25));
    expect(canonicalSriLankanDistrict(' kurunegala '), 'Kurunegala');
    expect(canonicalSriLankanDistrict('Not a district'), isNull);
  });

  testWidgets('district selection is required and filters searchable choices', (
    tester,
  ) async {
    final formKey = GlobalKey<FormState>();
    String? selected;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Form(
            key: formKey,
            child: SriLankanDistrictField(
              onChanged: (value) => selected = value,
            ),
          ),
        ),
      ),
    );

    expect(formKey.currentState!.validate(), isFalse);
    await tester.pump();
    expect(find.text('Select a Sri Lankan district'), findsOneWidget);

    await tester.tap(find.byType(TextField));
    await tester.enterText(find.byType(TextField), 'kuru');
    await tester.pumpAndSettle();
    expect(find.text('Kurunegala'), findsWidgets);
    await tester.tap(find.text('Kurunegala').last);
    await tester.pumpAndSettle();

    expect(selected, 'Kurunegala');
    expect(formKey.currentState!.validate(), isTrue);

    await tester.enterText(find.byType(TextField), 'Made Up District');
    expect(formKey.currentState!.validate(), isFalse);
  });

  testWidgets('persisted district is preselected on reload', (tester) async {
    final formKey = GlobalKey<FormState>();
    String? selected = 'Kurunegala';
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Form(
            key: formKey,
            child: SriLankanDistrictField(
              value: selected,
              onChanged: (value) => selected = value,
            ),
          ),
        ),
      ),
    );

    expect(find.text('Kurunegala'), findsWidgets);
    expect(formKey.currentState!.validate(), isTrue);
  });

  test('farm response retains its persisted district', () {
    final farm = FarmOption.fromJson(const {
      'id': 'farm-1',
      'name': 'North Farm',
      'location': 'Wariyapola',
      'district': 'Kurunegala',
      'totalArea': 10,
    });

    expect(farm.location, 'Wariyapola');
    expect(farm.district, 'Kurunegala');
  });
}
