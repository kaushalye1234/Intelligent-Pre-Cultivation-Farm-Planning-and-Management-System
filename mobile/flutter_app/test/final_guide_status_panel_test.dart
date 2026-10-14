import 'package:agriassist_mobile/screens/approved_crop_plan_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final status in ['Ready', 'Pending', 'Unavailable']) {
    testWidgets('$status guide displays its status and refresh', (tester) async {
      var refreshed = false;
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(
          body: FinalGuideStatusPanel(
            status: status,
            generatedAt: status == 'Ready' ? '2026-10-19T09:00:00Z' : null,
            onRefresh: () => refreshed = true,
          ),
        ),
      ));

      expect(find.text('Cultivation guide: $status'), findsOneWidget);
      if (status == 'Ready') {
        expect(find.textContaining('generated 19'), findsOneWidget);
      } else {
        expect(find.textContaining('approved work is available'), findsOneWidget);
      }
      await tester.tap(find.text('Refresh guide'));
      expect(refreshed, isTrue);
    });
  }
}
