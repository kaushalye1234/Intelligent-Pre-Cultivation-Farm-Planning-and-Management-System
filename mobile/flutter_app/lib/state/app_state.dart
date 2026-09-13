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
  String? lastCropPlanRequestId;
  CropPlanningWorkflowStart? lastWorkflowStart;
  CropPlanningWorkflowStatus? lastWorkflowStatus;
  CropPlanningResult? lastPlanningResult;
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

  Future<void> createAndStartAiCropPlan({
    required String farmId,
    required String? fieldId,
    required String cropTypeId,
    required String startDate,
    required String endDate,
    required num budget,
    required String objective,
  }) async {
    await _guard(() async {
      final requestId = await _apiClient.createPreliminaryCropPlan(
        farmId: farmId,
        fieldId: fieldId,
        cropTypeId: cropTypeId,
        preferredStartDate: startDate,
        preferredEndDate: endDate,
        budget: budget,
        objective: objective,
      );
      lastCropPlanRequestId = requestId;
      lastWorkflowStart = await _apiClient.startCropPlanningWorkflow(requestId);
      await refreshLastCropPlanningWorkflow();
      await refresh();
    });
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
      lastCropPlanRequestId = await _apiClient.createPreliminaryCropPlan(
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

  Future<void> refreshLastCropPlanningWorkflow() async {
    final requestId = lastCropPlanRequestId;
    if (requestId == null) return;
    lastWorkflowStatus = await _apiClient.cropPlanningWorkflowStatus(requestId);
    lastPlanningResult = await _apiClient.cropPlanningResult(requestId);
    notifyListeners();
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
