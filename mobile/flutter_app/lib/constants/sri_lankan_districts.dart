const sriLankanDistricts = <String>[
  'Ampara',
  'Anuradhapura',
  'Badulla',
  'Batticaloa',
  'Colombo',
  'Galle',
  'Gampaha',
  'Hambantota',
  'Jaffna',
  'Kalutara',
  'Kandy',
  'Kegalle',
  'Kilinochchi',
  'Kurunegala',
  'Mannar',
  'Matale',
  'Matara',
  'Monaragala',
  'Mullaitivu',
  'Nuwara Eliya',
  'Polonnaruwa',
  'Puttalam',
  'Ratnapura',
  'Trincomalee',
  'Vavuniya',
];

String? canonicalSriLankanDistrict(String? value) {
  final candidate = value?.trim();
  if (candidate == null || candidate.isEmpty) return null;

  for (final district in sriLankanDistricts) {
    if (district.toLowerCase() == candidate.toLowerCase()) return district;
  }
  return null;
}
