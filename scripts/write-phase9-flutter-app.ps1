$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$appRoot = Join-Path $PSScriptRoot "..\mobile\flutter_app"
$libRoot = Join-Path $appRoot "lib"

New-Item -ItemType Directory -Force -Path (Join-Path $libRoot "models") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $libRoot "services") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $libRoot "state") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $libRoot "screens") | Out-Null

function Write-NoBom($Path, $Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

Write-NoBom (Join-Path $libRoot "models\api_models.dart") @'
class UserProfile {
  const UserProfile({
    required this.id,
    required this.fullName,
    required this.email,
    required this.role,
    required this.isActive,
  });

  final String id;
  final String fullName;
  final String email;
  final int role;
  final bool isActive;

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      id: json['id'] as String,
      fullName: json['fullName'] as String,
      email: json['email'] as String,
      role: json['role'] as int,
      isActive: json['isActive'] as bool,
    );
  }
}

class DashboardSummary {
  const DashboardSummary({
    required this.activeFarms,
    required this.activeCropPlans,
    required this.openCropIssues,
    required this.lowStockResources,
    required this.pendingTasks,
    required this.pendingApprovals,
  });

  final int activeFarms;
  final int activeCropPlans;
  final int openCropIssues;
  final int lowStockResources;
  final int pendingTasks;
  final int pendingApprovals;

  factory DashboardSummary.empty() {
    return const DashboardSummary(
      activeFarms: 0,
      activeCropPlans: 0,
      openCropIssues: 0,
      lowStockResources: 0,
      pendingTasks: 0,
      pendingApprovals: 0,
    );
  }

  factory DashboardSummary.fromJson(Map<String, dynamic> json) {
    return DashboardSummary(
      activeFarms: json['activeFarms'] as int? ?? 0,
      activeCropPlans: json['activeCropPlans'] as int? ?? 0,
      openCropIssues: json['openCropIssues'] as int? ?? 0,
      lowStockResources: json['lowStockResources'] as int? ?? 0,
      pendingTasks: json['pendingTasks'] as int? ?? 0,
      pendingApprovals: json['pendingApprovals'] as int? ?? 0,
    );
  }
}

class FarmOption {
  const FarmOption({required this.id, required this.name});

  final String id;
  final String name;

  factory FarmOption.fromJson(Map<String, dynamic> json) {
    return FarmOption(id: json['id'] as String, name: json['name'] as String);
  }
}

class FieldOption {
  const FieldOption({required this.id, required this.name});

  final String id;
  final String name;

  factory FieldOption.fromJson(Map<String, dynamic> json) {
    return FieldOption(id: json['id'] as String, name: json['name'] as String);
  }
}

class CropTypeOption {
  const CropTypeOption({required this.id, required this.name});

  final String id;
  final String name;

  factory CropTypeOption.fromJson(Map<String, dynamic> json) {
    return CropTypeOption(id: json['id'] as String, name: json['name'] as String);
  }
}

class InventoryStock {
  const InventoryStock({
    required this.id,
    required this.resourceId,
    required this.availableQuantity,
    required this.lowStockThreshold,
  });

  final String id;
  final String resourceId;
  final num availableQuantity;
  final num lowStockThreshold;

  factory InventoryStock.fromJson(Map<String, dynamic> json) {
    return InventoryStock(
      id: json['id'] as String,
      resourceId: json['resourceId'] as String,
      availableQuantity: json['availableQuantity'] as num? ?? 0,
      lowStockThreshold: json['lowStockThreshold'] as num? ?? 0,
    );
  }
}
'@

Write-NoBom (Join-Path $libRoot "services\api_client.dart") @'
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
        baseUrl = baseUrl ?? const String.fromEnvironment('AGRIASSIST_API_BASE_URL', defaultValue: 'http://10.0.2.2:5000/api');

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
'@

Write-NoBom (Join-Path $libRoot "state\app_state.dart") @'
import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:image_picker/image_picker.dart';

import '../models/api_models.dart';
import '../services/api_client.dart';

class AppState extends ChangeNotifier {
  AppState({ApiClient? apiClient, ImagePicker? imagePicker})
      : _apiClient = apiClient ?? ApiClient(),
        _imagePicker = imagePicker ?? ImagePicker();

  final ApiClient _apiClient;
  final ImagePicker _imagePicker;

  UserProfile? user;
  DashboardSummary summary = DashboardSummary.empty();
  List<FarmOption> farms = [];
  List<FieldOption> fields = [];
  List<CropTypeOption> cropTypes = [];
  List<InventoryStock> stocks = [];
  String? error;
  bool isBusy = false;
  String? lastPhotoName;
  Position? lastPosition;

  bool get isAuthenticated => user != null;

  Future<void> restoreSession() async {
    isBusy = true;
    notifyListeners();
    try {
      final token = await _apiClient.loadToken();
      if (token != null) {
        user = await _apiClient.profile();
        await refresh();
      }
    } catch (_) {
      await _apiClient.clearToken();
      user = null;
    } finally {
      isBusy = false;
      notifyListeners();
    }
  }

  Future<void> login(String email, String password) async {
    await _guard(() async {
      user = await _apiClient.login(email, password);
      await refresh();
    });
  }

  Future<void> logout() async {
    await _apiClient.clearToken();
    user = null;
    notifyListeners();
  }

  Future<void> refresh() async {
    summary = await _apiClient.dashboard();
    farms = await _apiClient.farms();
    fields = await _apiClient.fields();
    cropTypes = await _apiClient.cropTypes();
    stocks = await _apiClient.stocks();
  }

  Future<void> createPreliminaryCropPlan({
    required String farmId,
    required String? fieldId,
    required String cropTypeId,
    required String startDate,
    required String endDate,
    required num budget,
    required String objective,
  }) async {
    await _guard(() async {
      await _apiClient.createPreliminaryCropPlan(
        farmId: farmId,
        fieldId: fieldId,
        cropTypeId: cropTypeId,
        preferredStartDate: startDate,
        preferredEndDate: endDate,
        budget: budget,
        objective: objective,
      );
      await refresh();
    });
  }

  Future<void> reserveResource({required String stockId, required num quantity, required String purpose}) async {
    await _guard(() async {
      await _apiClient.reserveResource(inventoryStockId: stockId, quantity: quantity, purpose: purpose);
      await refresh();
    });
  }

  Future<void> captureInspectionPhoto() async {
    final photo = await _imagePicker.pickImage(source: ImageSource.camera, imageQuality: 82);
    lastPhotoName = photo?.name;
    notifyListeners();
  }

  Future<void> captureLocation() async {
    final serviceEnabled = await Geolocator.isLocationServiceEnabled();
    if (!serviceEnabled) {
      error = 'Location services are disabled.';
      notifyListeners();
      return;
    }

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }

    if (permission == LocationPermission.denied || permission == LocationPermission.deniedForever) {
      error = 'Location permission was denied.';
      notifyListeners();
      return;
    }

    lastPosition = await Geolocator.getCurrentPosition();
    notifyListeners();
  }

  Future<void> _guard(Future<void> Function() action) async {
    isBusy = true;
    error = null;
    notifyListeners();
    try {
      await action();
    } catch (err) {
      error = err.toString();
    } finally {
      isBusy = false;
      notifyListeners();
    }
  }
}
'@

Write-NoBom (Join-Path $libRoot "screens\login_screen.dart") @'
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController(text: 'farmer@agriassist.local');
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    await context.read<AppState>().login(_emailController.text.trim(), _passwordController.text);
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Form(
                key: _formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Icon(Icons.eco, size: 52, color: Color(0xFF1F7A4A)),
                    const SizedBox(height: 16),
                    Text('AgriAssist Mobile', textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineMedium),
                    const SizedBox(height: 24),
                    TextFormField(
                      controller: _emailController,
                      decoration: const InputDecoration(labelText: 'Email', prefixIcon: Icon(Icons.mail_outline)),
                      keyboardType: TextInputType.emailAddress,
                      validator: (value) => value == null || value.trim().isEmpty ? 'Email is required' : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _passwordController,
                      decoration: const InputDecoration(labelText: 'Password', prefixIcon: Icon(Icons.lock_outline)),
                      obscureText: true,
                      validator: (value) => value == null || value.isEmpty ? 'Password is required' : null,
                    ),
                    if (state.error != null) ...[
                      const SizedBox(height: 12),
                      Text(state.error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                    ],
                    const SizedBox(height: 20),
                    FilledButton(
                      onPressed: state.isBusy ? null : _submit,
                      child: Text(state.isBusy ? 'Signing in...' : 'Sign in'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
'@

Write-NoBom (Join-Path $libRoot "screens\dashboard_screen.dart") @'
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    final summary = state.summary;
    final metrics = [
      ('Farms', summary.activeFarms, Icons.grass),
      ('Crop plans', summary.activeCropPlans, Icons.spa),
      ('Open issues', summary.openCropIssues, Icons.report_problem_outlined),
      ('Low stock', summary.lowStockResources, Icons.inventory_2_outlined),
      ('Pending tasks', summary.pendingTasks, Icons.task_alt),
      ('Approvals', summary.pendingApprovals, Icons.verified_outlined),
    ];

    return RefreshIndicator(
      onRefresh: state.refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Farmer dashboard', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              for (final metric in metrics)
                SizedBox(
                  width: 160,
                  child: Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(metric.$3, color: Theme.of(context).colorScheme.primary),
                          const SizedBox(height: 16),
                          Text(metric.$1),
                          Text('${metric.$2}', style: Theme.of(context).textTheme.headlineMedium),
                        ],
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}
'@

Write-NoBom (Join-Path $libRoot "screens\crop_plan_screen.dart") @'
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class CropPlanScreen extends StatefulWidget {
  const CropPlanScreen({super.key});

  @override
  State<CropPlanScreen> createState() => _CropPlanScreenState();
}

class _CropPlanScreenState extends State<CropPlanScreen> {
  final _formKey = GlobalKey<FormState>();
  final _startController = TextEditingController();
  final _endController = TextEditingController();
  final _budgetController = TextEditingController();
  final _objectiveController = TextEditingController();
  String? _farmId;
  String? _fieldId;
  String? _cropTypeId;

  @override
  void dispose() {
    _startController.dispose();
    _endController.dispose();
    _budgetController.dispose();
    _objectiveController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate() || _farmId == null || _cropTypeId == null) {
      return;
    }

    await context.read<AppState>().createPreliminaryCropPlan(
          farmId: _farmId!,
          fieldId: _fieldId,
          cropTypeId: _cropTypeId!,
          startDate: _startController.text,
          endDate: _endController.text,
          budget: num.parse(_budgetController.text),
          objective: _objectiveController.text,
        );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Crop plan request', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 12),
        Form(
          key: _formKey,
          child: Column(
            children: [
              DropdownButtonFormField<String>(
                value: _farmId,
                decoration: const InputDecoration(labelText: 'Farm'),
                items: state.farms.map((farm) => DropdownMenuItem(value: farm.id, child: Text(farm.name))).toList(),
                onChanged: (value) => setState(() => _farmId = value),
                validator: (value) => value == null ? 'Farm is required' : null,
              ),
              DropdownButtonFormField<String>(
                value: _fieldId,
                decoration: const InputDecoration(labelText: 'Field'),
                items: state.fields.map((field) => DropdownMenuItem(value: field.id, child: Text(field.name))).toList(),
                onChanged: (value) => setState(() => _fieldId = value),
              ),
              DropdownButtonFormField<String>(
                value: _cropTypeId,
                decoration: const InputDecoration(labelText: 'Crop type'),
                items: state.cropTypes.map((crop) => DropdownMenuItem(value: crop.id, child: Text(crop.name))).toList(),
                onChanged: (value) => setState(() => _cropTypeId = value),
                validator: (value) => value == null ? 'Crop type is required' : null,
              ),
              TextFormField(controller: _startController, decoration: const InputDecoration(labelText: 'Start date YYYY-MM-DD'), validator: _required),
              TextFormField(controller: _endController, decoration: const InputDecoration(labelText: 'End date YYYY-MM-DD'), validator: _required),
              TextFormField(controller: _budgetController, decoration: const InputDecoration(labelText: 'Budget'), keyboardType: TextInputType.number, validator: _required),
              TextFormField(controller: _objectiveController, decoration: const InputDecoration(labelText: 'Objective'), validator: _required),
              const SizedBox(height: 16),
              FilledButton.icon(onPressed: state.isBusy ? null : _submit, icon: const Icon(Icons.auto_awesome), label: const Text('Generate preliminary request')),
            ],
          ),
        ),
      ],
    );
  }

  String? _required(String? value) => value == null || value.trim().isEmpty ? 'Required' : null;
}
'@

Write-NoBom (Join-Path $libRoot "screens\inspection_screen.dart") @'
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class InspectionScreen extends StatelessWidget {
  const InspectionScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Inspection tools', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 12),
        Card(
          child: ListTile(
            leading: const Icon(Icons.camera_alt_outlined),
            title: const Text('Capture inspection photo'),
            subtitle: Text(state.lastPhotoName ?? 'No photo selected'),
            trailing: IconButton(
              tooltip: 'Open camera',
              icon: const Icon(Icons.add_a_photo_outlined),
              onPressed: state.captureInspectionPhoto,
            ),
          ),
        ),
        Card(
          child: ListTile(
            leading: const Icon(Icons.location_on_outlined),
            title: const Text('Capture GPS position'),
            subtitle: Text(state.lastPosition == null ? 'No position captured' : '${state.lastPosition!.latitude}, ${state.lastPosition!.longitude}'),
            trailing: IconButton(
              tooltip: 'Get location',
              icon: const Icon(Icons.my_location),
              onPressed: state.captureLocation,
            ),
          ),
        ),
      ],
    );
  }
}
'@

Write-NoBom (Join-Path $libRoot "screens\resources_screen.dart") @'
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class ResourcesScreen extends StatefulWidget {
  const ResourcesScreen({super.key});

  @override
  State<ResourcesScreen> createState() => _ResourcesScreenState();
}

class _ResourcesScreenState extends State<ResourcesScreen> {
  final _formKey = GlobalKey<FormState>();
  final _quantityController = TextEditingController();
  final _purposeController = TextEditingController();
  String? _stockId;

  @override
  void dispose() {
    _quantityController.dispose();
    _purposeController.dispose();
    super.dispose();
  }

  Future<void> _reserve() async {
    if (!_formKey.currentState!.validate() || _stockId == null) {
      return;
    }

    await context.read<AppState>().reserveResource(
          stockId: _stockId!,
          quantity: num.parse(_quantityController.text),
          purpose: _purposeController.text,
        );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Resource reservations', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 12),
        Form(
          key: _formKey,
          child: Column(
            children: [
              DropdownButtonFormField<String>(
                value: _stockId,
                decoration: const InputDecoration(labelText: 'Available stock'),
                items: state.stocks
                    .map((stock) => DropdownMenuItem(value: stock.id, child: Text('${stock.resourceId.substring(0, 8)} - ${stock.availableQuantity} available')))
                    .toList(),
                onChanged: (value) => setState(() => _stockId = value),
                validator: (value) => value == null ? 'Stock is required' : null,
              ),
              TextFormField(controller: _quantityController, decoration: const InputDecoration(labelText: 'Quantity'), keyboardType: TextInputType.number, validator: _required),
              TextFormField(controller: _purposeController, decoration: const InputDecoration(labelText: 'Purpose'), validator: _required),
              const SizedBox(height: 16),
              FilledButton.icon(onPressed: state.isBusy ? null : _reserve, icon: const Icon(Icons.inventory_2_outlined), label: const Text('Reserve stock')),
            ],
          ),
        ),
      ],
    );
  }

  String? _required(String? value) => value == null || value.trim().isEmpty ? 'Required' : null;
}
'@

Write-NoBom (Join-Path $libRoot "screens\home_shell.dart") @'
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import 'crop_plan_screen.dart';
import 'dashboard_screen.dart';
import 'inspection_screen.dart';
import 'resources_screen.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;

  final _screens = const [
    DashboardScreen(),
    CropPlanScreen(),
    InspectionScreen(),
    ResourcesScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return Scaffold(
      appBar: AppBar(
        title: Text(state.user?.fullName ?? 'AgriAssist'),
        actions: [
          IconButton(tooltip: 'Refresh', onPressed: state.isBusy ? null : state.refresh, icon: const Icon(Icons.refresh)),
          IconButton(tooltip: 'Sign out', onPressed: state.logout, icon: const Icon(Icons.logout)),
        ],
      ),
      body: Stack(
        children: [
          _screens[_index],
          if (state.isBusy) const LinearProgressIndicator(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (value) => setState(() => _index = value),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.dashboard_outlined), selectedIcon: Icon(Icons.dashboard), label: 'Dashboard'),
          NavigationDestination(icon: Icon(Icons.spa_outlined), selectedIcon: Icon(Icons.spa), label: 'Plans'),
          NavigationDestination(icon: Icon(Icons.camera_alt_outlined), selectedIcon: Icon(Icons.camera_alt), label: 'Inspect'),
          NavigationDestination(icon: Icon(Icons.inventory_2_outlined), selectedIcon: Icon(Icons.inventory_2), label: 'Resources'),
        ],
      ),
      bottomSheet: state.error == null
          ? null
          : Material(
              color: Theme.of(context).colorScheme.errorContainer,
              child: SafeArea(
                top: false,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(state.error!),
                ),
              ),
            ),
    );
  }
}
'@

Write-NoBom (Join-Path $libRoot "main.dart") @'
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'screens/home_shell.dart';
import 'screens/login_screen.dart';
import 'state/app_state.dart';

void main() {
  runApp(const AgriAssistMobileApp());
}

class AgriAssistMobileApp extends StatelessWidget {
  const AgriAssistMobileApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => AppState()..restoreSession(),
      child: MaterialApp(
        title: 'AgriAssist Mobile',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF1F7A4A)),
          useMaterial3: true,
          inputDecorationTheme: const InputDecorationTheme(border: OutlineInputBorder()),
          cardTheme: const CardThemeData(margin: EdgeInsets.symmetric(vertical: 8)),
        ),
        home: const AuthGate(),
      ),
    );
  }
}

class AuthGate extends StatelessWidget {
  const AuthGate({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    if (state.isBusy && !state.isAuthenticated) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    return state.isAuthenticated ? const HomeShell() : const LoginScreen();
  }
}
'@

Write-NoBom (Join-Path $appRoot "test\widget_test.dart") @'
import 'package:agriassist_mobile/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('shows mobile login form', (tester) async {
    await tester.pumpWidget(const AgriAssistMobileApp());
    await tester.pump();

    expect(find.text('AgriAssist Mobile'), findsOneWidget);
    expect(find.byType(TextFormField), findsNWidgets(2));
  });

  testWidgets('validates empty password', (tester) async {
    await tester.pumpWidget(const AgriAssistMobileApp());
    await tester.pump();

    await tester.tap(find.text('Sign in'));
    await tester.pump();

    expect(find.text('Password is required'), findsOneWidget);
  });
}
'@

$androidManifestPath = Join-Path $appRoot "android\app\src\main\AndroidManifest.xml"
$manifest = Get-Content $androidManifestPath -Raw
if ($manifest -notmatch "android.permission.CAMERA") {
    $manifest = $manifest.Replace("<manifest xmlns:android=`"http://schemas.android.com/apk/res/android`">", "<manifest xmlns:android=`"http://schemas.android.com/apk/res/android`">`r`n    <uses-permission android:name=`"android.permission.CAMERA`" />`r`n    <uses-permission android:name=`"android.permission.ACCESS_FINE_LOCATION`" />`r`n    <uses-permission android:name=`"android.permission.ACCESS_COARSE_LOCATION`" />")
    Write-NoBom $androidManifestPath $manifest
}

$iosPlistPath = Join-Path $appRoot "ios\Runner\Info.plist"
$plist = Get-Content $iosPlistPath -Raw
if ($plist -notmatch "NSCameraUsageDescription") {
    $plist = $plist.Replace("</dict>", "	<key>NSCameraUsageDescription</key>`r`n	<string>Capture inspection photos.</string>`r`n	<key>NSLocationWhenInUseUsageDescription</key>`r`n	<string>Capture field inspection location.</string>`r`n</dict>")
    Write-NoBom $iosPlistPath $plist
}
