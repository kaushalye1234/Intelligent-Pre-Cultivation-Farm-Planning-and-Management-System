String? normalizeSriLankanPhoneNumber(String? value) {
  final compact = value?.replaceAll(RegExp(r'[\s-]'), '') ?? '';
  final nationalNumber = compact.startsWith('+94')
      ? compact.substring(3)
      : compact.startsWith('0')
      ? compact.substring(1)
      : '';

  if (!RegExp(r'^[1-9][0-9]{8}$').hasMatch(nationalNumber)) return null;
  return '+94$nationalNumber';
}

String? validateSriLankanPhoneNumber(String? value) {
  if ((value ?? '').trim().isEmpty) return 'Phone number is required';
  if (normalizeSriLankanPhoneNumber(value) == null) {
    return 'Enter a valid Sri Lankan phone number';
  }
  return null;
}
