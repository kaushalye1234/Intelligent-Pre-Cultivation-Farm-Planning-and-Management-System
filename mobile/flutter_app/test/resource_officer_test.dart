import 'dart:async';
import 'dart:convert';

import 'package:agriassist_mobile/main.dart';
import 'package:agriassist_mobile/models/api_models.dart';
import 'package:agriassist_mobile/screens/resource_inventory_screen.dart';
import 'package:agriassist_mobile/screens/resource_reservations_screen.dart';
import 'package:agriassist_mobile/services/api_client.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:agriassist_mobile/state/resource_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

const _officer = UserProfile(
  id: 'officer-1',
  fullName: 'Rita Resource',
  email: 'rita@example.test',
  role: 3,
  isActive: true,
);

Map<String, dynamic> _stock(
  String id,
  String name, {
  num onHand = 10,
  num reserved = 4,
  num threshold = 2,
}) => {
  'id': id,
  'resourceId': 'res-$id',
  'resourceName': name,
  'unit': 'kg',
  'quantityOnHand': onHand,
  'reservedQuantity': reserved,
  'availableQuantity': onHand - reserved,
  'lowStockThreshold': threshold,
};

Map<String, dynamic> _reservation(String id, {int status = 1}) => {
  'id': id,
  'inventoryStockId': 'stock-1',
  'requestedByUserId': 'farmer-user-1234',
  'quantity': 4,
  'status': status,
  'purpose': 'Rice sowing',
  'resourceName': 'Paddy Seed',
  'unit': 'kg',
  'createdAt': '2026-09-01T08:00:00Z',
  'releasedAt': status == 1 ? null : '2026-09-02T08:00:00Z',
};

String _page(List<Map<String, dynamic>> items, {int page = 1, int totalPages = 1}) =>
    jsonEncode({
      'items': items,
      'page': page,
      'pageSize': 20,
      'totalCount': items.length,
      'totalPages': totalPages,
    });

class _Backend {
  final requests = <http.Request>[];
  List<Map<String, dynamic>> stocks = [
    _stock('stock-1', 'Paddy Seed'),
    _stock('stock-2', 'Urea', onHand: 5, reserved: 4, threshold: 2),
    _stock('stock-3', 'Sprayer Nozzle', onHand: 3, reserved: 3),
  ];
  List<Map<String, dynamic>> reservations = [_reservation('rsv-1')];
  int stocksStatus = 200;
  Completer<void>? stocksGate;
  int historyStatus = 200;
  List<Map<String, dynamic>> history = [
    {
      'id': 't1',
      'inventoryStockId': 'stock-1',
      'type': 1,
      'quantity': 10,
      'note': 'Stock level updated.',
      'createdAt': '2026-09-01T08:00:00Z',
    },
    {
      'id': 't2',
      'inventoryStockId': 'stock-1',
      'type': 3,
      'quantity': 4,
      'note': 'Rice sowing',
      'createdAt': '2026-09-02T09:30:00Z',
    },
  ];

  Future<http.Response> handle(http.Request request) async {
    requests.add(request);
    final path = request.url.path;
    if (path == '/api/resources/stocks') {
      await stocksGate?.future;
      if (stocksStatus != 200) {
        return http.Response(
          jsonEncode({
            'error': {'message': 'Inventory service is down.'},
          }),
          stocksStatus,
        );
      }
      final search = request.url.queryParameters['search']?.toLowerCase();
      final low = request.url.queryParameters['lowStockOnly'] == 'true';
      final filtered = stocks.where((stock) {
        final matchesSearch =
            search == null ||
            (stock['resourceName'] as String).toLowerCase().contains(search);
        final matchesLow =
            !low ||
            (stock['availableQuantity'] as num) <=
                (stock['lowStockThreshold'] as num);
        return matchesSearch && matchesLow;
      }).toList();
      return http.Response(_page(filtered), 200);
    }
    if (path == '/api/resources/reservations') {
      final status = request.url.queryParameters['status'];
      final filtered = reservations
          .where((item) => status == null || '${item['status']}' == status)
          .toList();
      return http.Response(_page(filtered), 200);
    }
    if (path.endsWith('/history')) {
      if (historyStatus != 200) {
        return http.Response(
          jsonEncode({
            'error': {'message': 'History unavailable.'},
          }),
          historyStatus,
        );
      }
      return http.Response(jsonEncode(history), 200);
    }
    if (request.method == 'POST' && path.endsWith('/release')) {
      reservations = [_reservation('rsv-1', status: 2)];
      return http.Response(jsonEncode(reservations.first), 200);
    }
    if (request.method == 'POST' && path.endsWith('/cancel')) {
      reservations = [_reservation('rsv-1', status: 3)];
      return http.Response(jsonEncode(reservations.first), 200);
    }
    return http.Response('{}', 404);
  }
}

ApiClient _client(_Backend backend) => ApiClient(
  httpClient: MockClient(backend.handle),
  baseUrl: 'https://api.example.test/api',
);

Widget _harness(_Backend backend, Widget child, {bool canManage = true}) {
  return ChangeNotifierProvider<ResourceState>(
    create: (_) =>
        ResourceState(apiClient: _client(backend), canManage: canManage)
          ..loadStocks()
          ..loadReservations(),
    child: MaterialApp(home: Scaffold(body: child)),
  );
}

void main() {
  group('ApiClient resource calls', () {
    test('builds paged search queries and parses stock', () async {
      final backend = _Backend();
      final page = await _client(
        backend,
      ).resourceStocks(search: 'urea', lowStockOnly: true, page: 2);

      final query = backend.requests.single.url.queryParameters;
      expect(query['search'], 'urea');
      expect(query['lowStockOnly'], 'true');
      expect(query['page'], '2');
      expect(query['sortBy'], 'resourceName');
      expect(page.items.first.resourceName, 'Urea');
      expect(page.items.first.availableQuantity, 1);
      expect(page.items.first.isLowStock, isTrue);
    });

    test('history is a list response shown newest first', () async {
      final backend = _Backend();
      final history = await _client(backend).stockHistory('stock-1');

      expect(history.map((entry) => entry.typeLabel), ['Reserved', 'Stock added']);
    });

    test('a failed history call surfaces the API message', () async {
      final backend = _Backend()..historyStatus = 404;
      expect(
        _client(backend).stockHistory('stock-1'),
        throwsA(
          isA<ApiException>().having((e) => e.message, 'message', 'History unavailable.'),
        ),
      );
    });

    test('release and cancel post to the reservation actions', () async {
      final backend = _Backend();
      final client = _client(backend);
      final released = await client.releaseReservation('rsv-1');
      final cancelled = await client.cancelReservation('rsv-1');

      expect(backend.requests[0].url.path, '/api/resources/reservations/rsv-1/release');
      expect(backend.requests[1].url.path, '/api/resources/reservations/rsv-1/cancel');
      expect(released.statusLabel, 'Released');
      expect(cancelled.statusLabel, 'Cancelled');
    });
  });

  group('role gating', () {
    test('Resource Officer is a mobile role; other staff roles are not', () {
      expect(AppState.isMobileRole(1), isTrue);
      expect(AppState.isMobileRole(3), isTrue);
      expect(AppState.isMobileRole(2), isFalse);
      expect(AppState.isMobileRole(4), isFalse);
      expect(AppState.isMobileRole(5), isFalse);
    });

    testWidgets('Resource Officer lands on inventory with two tabs', (tester) async {
      final backend = _Backend();
      final state = AppState(apiClient: _client(backend))..user = _officer;

      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: state,
          child: const MaterialApp(home: AuthGate()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Inventory'), findsWidgets);
      expect(find.text('Reservations'), findsWidgets);
      expect(find.text('Paddy Seed'), findsOneWidget);
      expect(find.text('Staff portal required'), findsNothing);
      expect(find.text('Plans'), findsNothing);
    });

    testWidgets('Field Officer is still sent to the staff portal', (tester) async {
      final state = AppState(apiClient: _client(_Backend()))
        ..user = const UserProfile(
          id: 'officer-2',
          fullName: 'Field Officer',
          email: 'field@example.test',
          role: 2,
          isActive: true,
        );

      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: state,
          child: const MaterialApp(home: AuthGate()),
        ),
      );

      expect(find.text('Staff portal required'), findsOneWidget);
    });
  });

  group('inventory screen', () {
    testWidgets('shows availability and low-stock indication', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(_harness(backend, const ResourceInventoryScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Paddy Seed'), findsOneWidget);
      expect(find.text('6 kg available'), findsOneWidget);
      expect(find.text('In stock'), findsOneWidget);
      expect(find.text('Low stock'), findsOneWidget); // Urea: 1 available <= 2
      expect(find.text('Out of stock'), findsOneWidget); // Sprayer Nozzle: 0
    });

    testWidgets('search and the low-stock filter reach the API', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(_harness(backend, const ResourceInventoryScreen()));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'urea');
      await tester.testTextInput.receiveAction(TextInputAction.search);
      await tester.pumpAndSettle();

      expect(find.text('Urea'), findsOneWidget);
      expect(find.text('Paddy Seed'), findsNothing);
      expect(backend.requests.last.url.queryParameters['search'], 'urea');

      await tester.tap(find.byTooltip('Clear search'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Low stock only'));
      await tester.pumpAndSettle();

      expect(backend.requests.last.url.queryParameters['lowStockOnly'], 'true');
      expect(find.text('Paddy Seed'), findsNothing);
      expect(find.text('Urea'), findsOneWidget);
    });

    testWidgets('shows an empty state for no matches', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(_harness(backend, const ResourceInventoryScreen()));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'zzz');
      await tester.testTextInput.receiveAction(TextInputAction.search);
      await tester.pumpAndSettle();

      expect(find.text('No matching resources'), findsOneWidget);
    });

    testWidgets('shows a loading indicator and then an error with retry', (tester) async {
      final gate = Completer<void>();
      final backend = _Backend()
        ..stocksStatus = 500
        ..stocksGate = gate;
      await tester.pumpWidget(_harness(backend, const ResourceInventoryScreen()));
      await tester.pump();
      expect(find.byType(CircularProgressIndicator), findsWidgets);

      gate.complete();
      await tester.pumpAndSettle();
      expect(find.text('Inventory unavailable'), findsOneWidget);
      expect(find.text('Inventory service is down.'), findsOneWidget);

      backend
        ..stocksStatus = 200
        ..stocksGate = null;
      await tester.tap(find.text('Try again'));
      await tester.pumpAndSettle();
      expect(find.text('Paddy Seed'), findsOneWidget);
    });

    testWidgets('stock detail shows quantities and recent activity', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(_harness(backend, const ResourceInventoryScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Paddy Seed'));
      await tester.pumpAndSettle();

      expect(find.text('Low-stock threshold'), findsOneWidget);
      expect(find.text('Recent stock activity'), findsOneWidget);
      expect(find.textContaining('Reserved · 4 kg'), findsOneWidget);
      expect(find.textContaining('Stock added · 10 kg'), findsOneWidget);
    });

    testWidgets('stock detail shows an error when history fails', (tester) async {
      final backend = _Backend()..historyStatus = 500;
      await tester.pumpWidget(_harness(backend, const ResourceInventoryScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Paddy Seed'));
      await tester.pumpAndSettle();

      expect(find.text('History unavailable'), findsOneWidget);
    });
  });

  group('reservations screen', () {
    testWidgets('lists reservations with status and details', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(_harness(backend, const ResourceReservationsScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Paddy Seed'), findsOneWidget);
      expect(find.text('Reserved'), findsWidgets);

      await tester.tap(find.text('Paddy Seed'));
      await tester.pumpAndSettle();
      expect(find.text('Rice sowing'), findsWidgets);
      expect(find.text('Requested by'), findsOneWidget);
      expect(find.text('farmer-u'), findsOneWidget);
    });

    testWidgets('status filter is sent to the API and empty view is explained', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(_harness(backend, const ResourceReservationsScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Cancelled'));
      await tester.pumpAndSettle();

      expect(backend.requests.last.url.queryParameters['status'], '3');
      expect(find.text('No reservations'), findsOneWidget);
    });

    testWidgets('Resource Officer can release an active reservation after confirming', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(_harness(backend, const ResourceReservationsScreen()));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Paddy Seed'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(FilledButton, 'Release'));
      await tester.pumpAndSettle();
      expect(find.text('Release reservation?'), findsOneWidget);
      // Nothing is sent until the dialog is confirmed.
      expect(backend.requests.where((r) => r.method == 'POST'), isEmpty);
      await tester.tap(find.widgetWithText(FilledButton, 'Release').last);
      await tester.pumpAndSettle();

      expect(
        backend.requests.where((r) => r.method == 'POST').single.url.path,
        '/api/resources/reservations/rsv-1/release',
      );
      expect(find.text('Reservation released.'), findsOneWidget);
      expect(find.text('Released'), findsWidgets);
    });

    testWidgets('a rejected release keeps the sheet open and shows the error', (tester) async {
      final backend = _Backend();
      final client = ApiClient(
        httpClient: MockClient((request) async {
          if (request.method == 'POST') {
            return http.Response(
              jsonEncode({
                'error': {'message': 'Only active reservations can be released.'},
              }),
              409,
            );
          }
          return backend.handle(request);
        }),
        baseUrl: 'https://api.example.test/api',
      );
      await tester.pumpWidget(
        ChangeNotifierProvider<ResourceState>(
          create: (_) => ResourceState(apiClient: client, canManage: true)..loadReservations(),
          child: const MaterialApp(home: Scaffold(body: ResourceReservationsScreen())),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Paddy Seed'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel reservation'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Cancel reservation'));
      await tester.pumpAndSettle();

      expect(find.text('Only active reservations can be released.'), findsOneWidget);
      expect(find.text('Requested by'), findsOneWidget);
    });

    testWidgets('read-only users see no release or cancel actions', (tester) async {
      final backend = _Backend();
      await tester.pumpWidget(
        _harness(backend, const ResourceReservationsScreen(), canManage: false),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Paddy Seed'));
      await tester.pumpAndSettle();

      expect(find.text('Requested by'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Release'), findsNothing);
      expect(find.text('Cancel reservation'), findsNothing);
    });

    testWidgets('a read-only state refuses to mutate even if called directly', (tester) async {
      final backend = _Backend();
      final state = ResourceState(apiClient: _client(backend), canManage: false);

      final error = await state.releaseReservation('rsv-1');

      expect(error, isNotNull);
      expect(backend.requests.where((r) => r.method == 'POST'), isEmpty);
    });
  });
}
