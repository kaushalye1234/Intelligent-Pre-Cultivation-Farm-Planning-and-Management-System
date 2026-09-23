import 'dart:convert';

import 'package:agriassist_mobile/services/api_client.dart';
import 'package:agriassist_mobile/state/app_state.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

const _userJson = {
  'id': 'user-1',
  'fullName': 'Taylor Farmer',
  'email': 'taylor@example.test',
  'role': 1,
  'isActive': true,
  'mustChangePassword': false,
};

String _authResponse({
  required String status,
  String? accessToken,
  String? passwordChangeToken,
}) {
  return jsonEncode({
    'authenticationStatus': status,
    'accessToken': accessToken,
    'accessTokenExpiresAt': null,
    'passwordChangeToken': passwordChangeToken,
    'passwordChangeTokenExpiresAt': null,
    'user': _userJson,
  });
}

void main() {
  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
  });

  test(
    'Farmer registration uses the public endpoint without a role field',
    () async {
      late http.Request captured;
      final httpClient = MockClient((request) async {
        captured = request;
        return http.Response(
          _authResponse(status: 'authenticated', accessToken: 'access-token'),
          201,
          headers: {'content-type': 'application/json'},
        );
      });
      const storage = FlutterSecureStorage();
      final client = ApiClient(
        httpClient: httpClient,
        secureStorage: storage,
        baseUrl: 'https://api.example.test/api',
      );

      final session = await client.registerFarmer(
        fullName: 'Taylor Farmer',
        email: 'taylor@example.test',
        password: 'a long farm passphrase',
      );
      final payload = jsonDecode(captured.body) as Map<String, dynamic>;

      expect(captured.method, 'POST');
      expect(captured.url.path, '/api/auth/register-farmer');
      expect(captured.headers.containsKey('authorization'), isFalse);
      expect(payload, containsPair('fullName', 'Taylor Farmer'));
      expect(payload.containsKey('role'), isFalse);
      expect(session.user.role, 1);
      expect(await storage.read(key: 'agriassist.token'), 'access-token');
    },
  );

  test(
    'temporary password token is never persisted as an app session',
    () async {
      FlutterSecureStorage.setMockInitialValues({
        'agriassist.token': 'older-access-token',
      });
      final httpClient = MockClient((request) async {
        return http.Response(
          _authResponse(
            status: 'passwordChangeRequired',
            passwordChangeToken: 'temporary-token',
          ),
          200,
        );
      });
      const storage = FlutterSecureStorage();
      final client = ApiClient(
        httpClient: httpClient,
        secureStorage: storage,
        baseUrl: 'https://api.example.test/api',
      );

      final session = await client.login(
        'taylor@example.test',
        'temporary password',
      );

      expect(session.requiresPasswordChange, isTrue);
      expect(session.passwordChangeToken, 'temporary-token');
      expect(await storage.read(key: 'agriassist.token'), isNull);
    },
  );

  test('staff login is rejected and its access token is cleared', () async {
    final requests = <http.Request>[];
    final httpClient = MockClient((request) async {
      requests.add(request);
      return http.Response(
        jsonEncode({
          'authenticationStatus': 'authenticated',
          'accessToken': 'staff-access-token',
          'accessTokenExpiresAt': null,
          'passwordChangeToken': null,
          'passwordChangeTokenExpiresAt': null,
          'user': {..._userJson, 'role': 2, 'fullName': 'Field Officer'},
        }),
        200,
      );
    });
    const storage = FlutterSecureStorage();
    final state = AppState(
      apiClient: ApiClient(
        httpClient: httpClient,
        secureStorage: storage,
        baseUrl: 'https://api.example.test/api',
      ),
    );

    await state.login('officer@example.test', 'staff password');

    expect(state.user, isNull);
    expect(state.isAuthenticated, isFalse);
    expect(state.error, AppState.staffPortalMessage);
    expect(await storage.read(key: 'agriassist.token'), isNull);
    expect(requests, hasLength(1));
    expect(requests.single.url.path, '/api/auth/login');
  });

  test(
    'temporary password change uses its token then stores the new access token',
    () async {
      late http.Request captured;
      final httpClient = MockClient((request) async {
        captured = request;
        return http.Response(
          _authResponse(
            status: 'authenticated',
            accessToken: 'new-access-token',
          ),
          200,
        );
      });
      const storage = FlutterSecureStorage();
      final client = ApiClient(
        httpClient: httpClient,
        secureStorage: storage,
        baseUrl: 'https://api.example.test/api',
      );

      final session = await client.changeTemporaryPassword(
        passwordChangeToken: 'temporary-token',
        newPassword: 'a safer replacement passphrase',
      );

      expect(captured.url.path, '/api/auth/change-temporary-password');
      expect(captured.headers['authorization'], 'Bearer temporary-token');
      expect(session.authenticationStatus, 'authenticated');
      expect(await storage.read(key: 'agriassist.token'), 'new-access-token');
    },
  );

  test(
    'Farmer onboarding uses the exact status, farm and field contracts',
    () async {
      FlutterSecureStorage.setMockInitialValues({
        'agriassist.token': 'farmer-access-token',
      });
      final requests = <http.Request>[];
      final httpClient = MockClient((request) async {
        requests.add(request);
        if (request.method == 'GET' &&
            request.url.path == '/api/farmer/onboarding-status') {
          return http.Response(
            jsonEncode({
              'stage': 'farm',
              'activeFarmCount': 0,
              'activeFieldCount': 0,
            }),
            200,
          );
        }
        if (request.url.path == '/api/crop-planning/farms') {
          return http.Response(
            jsonEncode({
              'id': 'farm-1',
              'name': 'North Farm',
              'location': 'North',
              'totalArea': 10,
            }),
            201,
          );
        }
        return http.Response(
          jsonEncode({
            'id': 'field-1',
            'farmId': 'farm-1',
            'name': 'Field One',
            'area': 4,
            'soilType': 'Loam',
            'isActive': true,
          }),
          200,
        );
      });
      final client = ApiClient(
        httpClient: httpClient,
        secureStorage: const FlutterSecureStorage(),
        baseUrl: 'https://api.example.test/api',
      );
      await client.loadToken();

      final status = await client.farmerOnboardingStatus();
      await client.createFarm(
        name: 'North Farm',
        location: 'North',
        totalArea: 10,
      );
      await client.createField(
        farmId: 'farm-1',
        name: 'Field One',
        area: 4,
        soilType: 'Loam',
      );

      expect(status.stage, 'farm');
      expect(requests.map((request) => request.url.path), [
        '/api/farmer/onboarding-status',
        '/api/crop-planning/farms',
        '/api/crop-planning/fields',
      ]);
      expect(
        requests.every(
          (request) =>
              request.headers['authorization'] == 'Bearer farmer-access-token',
        ),
        isTrue,
      );
      final farmPayload = jsonDecode(requests[1].body) as Map<String, dynamic>;
      final fieldPayload = jsonDecode(requests[2].body) as Map<String, dynamic>;
      expect(farmPayload['ownerUserId'], isNull);
      expect(fieldPayload, containsPair('farmId', 'farm-1'));
      expect(fieldPayload, containsPair('isActive', true));
    },
  );
}
