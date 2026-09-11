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

  Future<void> createPreliminaryCropPlan({
    required String farmId,
    required String? fieldId,
    required String cropTypeId,
    required String preferredStartDate,
    required String preferredEndDate,
    required num budget,
    required String objective,
  }) async {
    await _post('/crop-planning/requests/preliminary', {
      'farmId': farmId,
      'fieldId': fieldId,
      'cropTypeId': cropTypeId,
      'preferredStartDate': preferredStartDate,
      'preferredEndDate': preferredEndDate,
      'budget': budget,
      'objective': objective,
    });
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
      throw ApiException((body['message'] ?? body['title'] ?? 'Request failed') as String);
    }
    return body;
  }

  List<Map<String, dynamic>> _items(Map<String, dynamic> response) {
    final items = response['items'] as List<dynamic>? ?? const [];
    return items.cast<Map<String, dynamic>>();
  }
}
