String? normalizeSriLankanPhoneNumber(String? value) {
  final localNumber = value ?? '';
  if (!RegExp(r'^0[1-9][0-9]{8}$').hasMatch(localNumber)) return null;
  return '+94${localNumber.substring(1)}';
}

String? validateSriLankanPhoneNumber(String? value) {
  if ((value ?? '').trim().isEmpty) return 'Phone number is required';
  if (normalizeSriLankanPhoneNumber(value) == null) {
    return 'Enter a valid Sri Lankan phone number';
  }
  return null;
}
