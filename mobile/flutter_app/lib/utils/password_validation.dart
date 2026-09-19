import 'dart:convert';

const _commonPasswords = {
  'password',
  'password123',
  '123456789012',
  'qwertyuiop12',
  'letmein12345',
  'adminpassword',
};

String? validateAccountPassword(
  String? value, {
  required String fullName,
  required String email,
}) {
  final password = value ?? '';
  if (password.isEmpty) return 'Password is required';
  if (password.length < 12) return 'Use at least 12 characters';
  if (utf8.encode(password).length > 72) {
    return 'Password must be 72 UTF-8 bytes or fewer';
  }

  final normalized = password.trim().toLowerCase();
  if (_commonPasswords.contains(normalized)) {
    return 'Choose a less common password';
  }
  if (email.trim().isNotEmpty && normalized == email.trim().toLowerCase()) {
    return 'Password cannot be your email address';
  }
  if (fullName.trim().isNotEmpty &&
      normalized == fullName.trim().toLowerCase()) {
    return 'Password cannot be your full name';
  }

  return null;
}
