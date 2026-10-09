final _farmerFullNamePattern = RegExp(r"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$");

String? validateFarmerFullName(String? value) {
  final name = value?.trim() ?? '';
  if (name.isEmpty) return 'Full name is required';
  if (name.length > 120) return 'Use 120 characters or fewer';
  if (!_farmerFullNamePattern.hasMatch(name)) {
    return 'Use letters, spaces, apostrophes, and hyphens only';
  }
  return null;
}
