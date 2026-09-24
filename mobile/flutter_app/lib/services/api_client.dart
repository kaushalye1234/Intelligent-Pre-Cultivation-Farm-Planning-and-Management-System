import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

import '../models/api_models.dart';

class ApiException implements Exception {
  const ApiException(
    this.message, {
    this.code,
    this.statusCode,
    this.retryAfter,
  });

  final String message;
  final String? code;
  final int? statusCode;
  final String? retryAfter;

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({
    http.Client? httpClient,
    FlutterSecureStorage? secureStorage,
    String? baseUrl,
  }) : _httpClient = httpClient ?? http.Client(),
       _secureStorage = secureStorage ?? const FlutterSecureStorage(),
       baseUrl =
           baseUrl ??
           const String.fromEnvironment(
             'AGRIASSIST_API_BASE_URL',
             defaultValue: 'http://10.0.2.2:5087/api',
           );

  final http.Client _httpClient;
  final FlutterSecureStorage _secureStorage;
  final String baseUrl;
  String? _token;

  static const _tokenStorageKey = 'agriassist.token';

  Future<String?> loadToken() async {
    _token = await _secureStorage.read(key: _tokenStorageKey);
    return _token;
  }

  Future<void> clearToken() async {
    _token = null;
    await _secureStorage.delete(key: _tokenStorageKey);
  }

  Future<AuthenticationSession> login(String email, String password) async {
    final response = await _postUnauthenticated('/auth/login', {
      'email': email,
      'password': password,
    });
    final session = AuthenticationSession.fromJson(response);
    await _acceptAuthenticationSession(session);
    return session;
  }

  Future<AuthenticationSession> registerFarmer({
    required String fullName,
    required String email,
    required String password,
  }) async {
    final response = await _postUnauthenticated('/auth/register-farmer', {
      'fullName': fullName,
      'email': email,
      'password': password,
    });
    final session = AuthenticationSession.fromJson(response);
    await _acceptAuthenticationSession(session);
    return session;
  }

  Future<AuthenticationSession> changeTemporaryPassword({
    required String passwordChangeToken,
    required String newPassword,
  }) async {
    final response = await _postWithToken('/auth/change-temporary-password', {
      'newPassword': newPassword,
    }, passwordChangeToken);
    final session = AuthenticationSession.fromJson(response);
    await _acceptAuthenticationSession(session);
    return session;
  }

  Future<UserProfile> profile() async {
    final response = await _get('/auth/profile');
    return UserProfile.fromJson(response);
  }

  Future<FarmerOnboardingStatus> farmerOnboardingStatus() async {
    final response = await _get('/farmer/onboarding-status');
    return FarmerOnboardingStatus.fromJson(response);
  }

  Future<FarmOption> createFarm({
    required String name,
    required String location,
    required num totalArea,
  }) async {
    final response = await _post('/crop-planning/farms', {
      'name': name,
      'location': location,
      'totalArea': totalArea,
      'ownerUserId': null,
    });
    return FarmOption.fromJson(response);
  }

  Future<FieldOption> createField({
    required String farmId,
    required String name,
    required num area,
    required String soilType,
  }) async {
    final response = await _post('/crop-planning/fields', {
      'farmId': farmId,
      'name': name,
      'area': area,
      'soilType': soilType,
      'isActive': true,
    });
    return FieldOption.fromJson(response);
  }

  Future<DashboardSummary> dashboard() async {
    final response = await _get('/dashboard/summary');
    return DashboardSummary.fromJson(response);
  }

  Future<List<FarmOption>> farms() async {
    final items = await _allItems('/crop-planning/farms');
    return items.map(FarmOption.fromJson).toList();
  }

  Future<List<FieldOption>> fields() async {
    final items = await _allItems('/crop-planning/fields');
    return items.map(FieldOption.fromJson).toList();
  }

  Future<List<CropTypeOption>> cropTypes() async {
    final items = await _allItems('/crop-planning/crop-types');
    return items.map(CropTypeOption.fromJson).toList();
  }

  Future<List<CropVarietyOption>> cropVarieties() async {
    final items = await _allItems('/crop-planning/crop-varieties');
    return items.map(CropVarietyOption.fromJson).toList();
  }

  Future<List<FarmTaskRecord>> tasks() async {
    final response = await _get('/task-approval/tasks?pageSize=50');
    return _items(response).map(FarmTaskRecord.fromJson).toList();
  }

  Future<List<IrrigationScheduleRecord>> irrigationSchedules() async {
    final response = await _get('/task-approval/schedules?pageSize=50');
    return _items(response).map(IrrigationScheduleRecord.fromJson).toList();
  }

  Future<List<ApprovalHistoryRecord>> approvalHistory() async {
    final response = await _get('/task-approval/approvals?pageSize=50');
    return _items(response).map(ApprovalHistoryRecord.fromJson).toList();
  }

  Future<String> createPreliminaryCropPlan({
    required String farmId,
    required String? fieldId,
    required String cropTypeId,
    required String preferredStartDate,
    required String preferredEndDate,
    required num budget,
    required String objective,
    String? cropVarietyId,
    int cultivationSeason = 0,
    String? previousCropTypeId,
    List<String> previousKnownProblems = const [],
  }) async {
    final response = await _post('/crop-planning/requests', {
      'farmId': farmId,
      'fieldId': fieldId,
      'cropTypeId': cropTypeId,
      'cropVarietyId': cropVarietyId,
      'cultivationSeason': cultivationSeason,
      'previousCropTypeId': previousCropTypeId,
      'previousKnownProblems': previousKnownProblems,
      'preferredStartDate': preferredStartDate,
      'preferredEndDate': preferredEndDate,
      'budget': budget,
      'objective': objective,
    });
    return response['id'] as String;
  }

  Future<CropPlanningWorkflowStart> startCropPlanningWorkflow(
    String cropPlanRequestId,
  ) async {
    final response = await _post(
      '/crop-plans/$cropPlanRequestId/start-ai-workflow',
      {},
    );
    return CropPlanningWorkflowStart.fromJson(response);
  }

  Future<CropPlanningWorkflowStatus> cropPlanningWorkflowStatus(
    String cropPlanRequestId,
  ) async {
    final response = await _get(
      '/crop-plans/$cropPlanRequestId/workflow-status',
    );
    return CropPlanningWorkflowStatus.fromJson(response);
  }

  Future<CropPlanningResult> cropPlanningResult(
    String cropPlanRequestId,
  ) async {
    final response = await _get(
      '/crop-plans/$cropPlanRequestId/planning-result',
    );
    return CropPlanningResult.fromJson(response);
  }

  Future<Map<String, dynamic>> _get(String path) async {
    final response = await _httpClient.get(
      Uri.parse('$baseUrl$path'),
      headers: _headers(),
    );
    return _decode(response);
  }

  Future<Map<String, dynamic>> _post(
    String path,
    Map<String, dynamic> body,
  ) async {
    final response = await _httpClient.post(
      Uri.parse('$baseUrl$path'),
      headers: _headers(),
      body: jsonEncode(body),
    );
    return _decode(response);
  }

  Future<Map<String, dynamic>> _postUnauthenticated(
    String path,
    Map<String, dynamic> body,
  ) async {
    final response = await _httpClient.post(
      Uri.parse('$baseUrl$path'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode(body),
    );
    return _decode(response);
  }

  Future<Map<String, dynamic>> _postWithToken(
    String path,
    Map<String, dynamic> body,
    String token,
  ) async {
    final response = await _httpClient.post(
      Uri.parse('$baseUrl$path'),
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer $token',
      },
      body: jsonEncode(body),
    );
    return _decode(response);
  }

  Future<void> _acceptAuthenticationSession(
    AuthenticationSession session,
  ) async {
    if (session.requiresPasswordChange) {
      if (session.passwordChangeToken == null ||
          session.passwordChangeToken!.isEmpty) {
        throw const ApiException('Password-change token was not returned.');
      }
      await clearToken();
      return;
    }

    final token = session.accessToken;
    if (session.authenticationStatus != 'authenticated' ||
        token == null ||
        token.isEmpty) {
      throw const ApiException('Access token was not returned.');
    }

    _token = token;
    await _secureStorage.write(key: _tokenStorageKey, value: token);
  }

  Map<String, String> _headers() {
    final headers = {'Content-Type': 'application/json'};
    _applyAuth(headers);
    return headers;
  }

  void _applyAuth(Map<String, String> headers) {
    final token = _token;
    if (token != null && token.isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }
  }

  Map<String, dynamic> _decode(http.Response response) {
    final body = response.body.isEmpty
        ? <String, dynamic>{}
        : jsonDecode(response.body) as Map<String, dynamic>;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      final error = body['error'] as Map<String, dynamic>?;
      throw ApiException(
        (error?['message'] ??
                body['message'] ??
                body['title'] ??
                'Request failed')
            as String,
        code: error?['code'] as String?,
        statusCode: response.statusCode,
        retryAfter: response.headers['retry-after'],
      );
    }
    return body;
  }

  List<Map<String, dynamic>> _items(Map<String, dynamic> response) {
    final items = response['items'] as List<dynamic>? ?? const [];
    return items.cast<Map<String, dynamic>>();
  }

  Future<List<Map<String, dynamic>>> _allItems(String path) async {
    final result = <Map<String, dynamic>>[];
    var page = 1;
    while (true) {
      final separator = path.contains('?') ? '&' : '?';
      final response = await _get('$path${separator}page=$page&pageSize=100');
      result.addAll(_items(response));
      final totalPages = response['totalPages'] as int? ?? 1;
      if (page >= totalPages) return result;
      page++;
    }
  }
}
