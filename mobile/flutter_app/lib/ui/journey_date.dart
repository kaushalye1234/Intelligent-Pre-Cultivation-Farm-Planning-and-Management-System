String journeyDate(String raw, {bool includeTime = false}) {
  final date = DateTime.tryParse(raw)?.toLocal();
  if (date == null) return raw.isEmpty ? 'Date unavailable' : raw;
  const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];
  final day = date.day;
  final month = months[date.month - 1];
  final year = date.year;
  final formatted = '$day $month $year';
  if (!includeTime) return formatted;
  final hour = date.hour.toString().padLeft(2, '0');
  final minute = date.minute.toString().padLeft(2, '0');
  return '$formatted at $hour:$minute';
}
