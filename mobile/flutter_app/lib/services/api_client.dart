import 'dart:convert';
import 'dart:io';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

import '../models/api_models.dart';

class ApiException implements Exception {
  const ApiException(this.message);

  final String message;

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({
    http.Client? httpClient,
    FlutterSecureStorage? secureStorage,
    String? baseUrl,
  })  : _httpClient = httpClient ?? http.Client(),
        _secureStorage = secureStorage ?? const FlutterSecureStorage(),
        baseUrl = baseUrl ?? const String.fromEnvironment('AGRIASSIST_API_BASE_URL', defaultValue: 'http://10.0.2.2:5087/api');

  final http.Client _httpClient;
  final FlutterSecureStorage _secureStorage;
  final String baseUrl;
  String? _token;

  Future<String?> loadToken() async {
    _token = await _secureStorage.read(key: 'agriassist.token');
    return _token;
  }

  Future<void> clearToken() async {
    _token = null;
    await _secureStorage.delete(key: 'agriassist.token');
  }

  Future<UserProfile> login(String email, String password) async {
    final response = await _post('/auth/login', {'email': email, 'password': password});
    final token = response['accessToken'] as String;
    _token = token;
    await _secureStorage.write(key: 'agriassist.token', value: token);
    return UserProfile.fromJson(response['user'] as Map<String, dynamic>);
  }

  Future<UserProfile> profile() async {
    final response = await _get('/auth/profile');
    return UserProfile.fromJson(response);
  }

  Future<DashboardSummary> dashboard() async {
    final response = await _get('/dashboard/summary');
    return DashboardSummary.fromJson(response);
  }

  Future<List<FarmOption>> farms() async {
    final response = await _get('/crop-planning/farms');
    return _items(response).map(FarmOption.fromJson).toList();
  }

  Future<List<FieldOption>> fields() async {
    final response = await _get('/crop-planning/fields');
    return _items(response).map(FieldOption.fromJson).toList();
  }

  Future<List<CropTypeOption>> cropTypes() async {
    final response = await _get('/crop-planning/crop-types');
    return _items(response).map(CropTypeOption.fromJson).toList();
  }

  Future<List<InventoryStock>> stocks() async {
    final response = await _get('/resources/stocks');
    return _items(response).map(InventoryStock.fromJson).toList();
  }

  Future<List<InspectionRecord>> inspections() async {
    final response = await _get('/inspections?sortBy=scheduledAt&sortDirection=desc&pageSize=50');
    return _items(response).map(InspectionRecord.fromJson).toList();
  }

  Future<List<CropIssueRecord>> cropIssues() async {
    final response = await _get('/inspections/issues?sortBy=createdAt&sortDirection=desc&pageSize=50');
    return _items(response).map(CropIssueRecord.fromJson).toList();
  }

  Future<List<FollowUpRecommendationRecord>> followUps() async {
    final response = await _get('/inspections/recommendations?isCompleted=false&pageSize=50');
    return _items(response).map(FollowUpRecommendationRecord.fromJson).toList();
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

  Future<List<InspectionHistoryEventRecord>> inspectionHistory(String inspectionId) async {
    final response = await _getList('/inspections/$inspectionId/history');
    return response.map(InspectionHistoryEventRecord.fromJson).toList();
  }

  Future<String> createInspection({required String fieldId, required String summary}) async {
    final response = await _post('/inspections', {
      'fieldId': fieldId,
      'scheduledAt': DateTime.now().toUtc().toIso8601String(),
      'status': 2,
      'summary': summary,
    });
    return response['id'] as String;
  }

  Future<void> submitInspection(String inspectionId) async {
    await _post('/inspections/$inspectionId/submit', {});
  }

  Future<void> createObservation({required String inspectionId, required String observationType, required String notes}) async {
    await _post('/inspections/observations', {
      'fieldInspectionId': inspectionId,
      'observationType': observationType,
      'notes': notes,
    });
  }

  Future<void> createCropIssue({required String inspectionId, required String title, required String description, required int severity}) async {
    await _post('/inspections/issues', {
      'fieldInspectionId': inspectionId,
      'title': title,
      'description': description,
      'severity': severity,
      'status': 1,
    });
  }

  Future<String> createPreliminaryCropPlan({
    required String farmId,
    required String? fieldId,
    required String cropTypeId,
    required String preferredStartDate,
    required String preferredEndDate,
    required num budget,
    required String objective,
  }) async {
    final response = await _post('/crop-planning/requests/preliminary', {
      'farmId': farmId,
      'fieldId': fieldId,
      'cropTypeId': cropTypeId,
      'preferredStartDate': preferredStartDate,
      'preferredEndDate': preferredEndDate,
      'budget': budget,
      'objective': objective,
    });
    return response['id'] as String;
  }

  Future<CropPlanningWorkflowStart> startCropPlanningWorkflow(String cropPlanRequestId) async {
    final response = await _post('/crop-plans/$cropPlanRequestId/start-ai-workflow', {});
    return CropPlanningWorkflowStart.fromJson(response);
  }

  Future<CropPlanningWorkflowStatus> cropPlanningWorkflowStatus(String cropPlanRequestId) async {
    final response = await _get('/crop-plans/$cropPlanRequestId/workflow-status');
    return CropPlanningWorkflowStatus.fromJson(response);
  }

  Future<CropPlanningResult> cropPlanningResult(String cropPlanRequestId) async {
    final response = await _get('/crop-plans/$cropPlanRequestId/planning-result');
    return CropPlanningResult.fromJson(response);
  }

  Future<void> reserveResource({required String inventoryStockId, required num quantity, required String purpose}) async {
    await _post('/resources/reservations', {
      'inventoryStockId': inventoryStockId,
      'quantity': quantity,
      'purpose': purpose,
    });
  }

  Future<void> uploadInspectionImage({required String inspectionId, required File imageFile}) async {
    final request = http.MultipartRequest('POST', Uri.parse('$baseUrl/inspections/$inspectionId/images'));
    _applyAuth(request.headers);
    request.files.add(await http.MultipartFile.fromPath('file', imageFile.path));
    final response = await request.send();
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw const ApiException('Image upload failed');
    }
  }

  Future<Map<String, dynamic>> _get(String path) async {
    final response = await _httpClient.get(Uri.parse('$baseUrl$path'), headers: _headers());
    return _decode(response);
  }

  Future<List<Map<String, dynamic>>> _getList(String path) async {
    final response = await _httpClient.get(Uri.parse('$baseUrl$path'), headers: _headers());
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw const ApiException('Request failed');
    }
    final body = response.body.isEmpty ? <dynamic>[] : jsonDecode(response.body) as List<dynamic>;
    return body.cast<Map<String, dynamic>>();
  }

  Future<Map<String, dynamic>> _post(String path, Map<String, dynamic> body) async {
    final response = await _httpClient.post(Uri.parse('$baseUrl$path'), headers: _headers(), body: jsonEncode(body));
    return _decode(response);
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
    final body = response.body.isEmpty ? <String, dynamic>{} : jsonDecode(response.body) as Map<String, dynamic>;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      final error = body['error'] as Map<String, dynamic>?;
      throw ApiException((error?['message'] ?? body['message'] ?? body['title'] ?? 'Request failed') as String);
    }
    return body;
  }

  List<Map<String, dynamic>> _items(Map<String, dynamic> response) {
    final items = response['items'] as List<dynamic>? ?? const [];
    return items.cast<Map<String, dynamic>>();
  }
}
