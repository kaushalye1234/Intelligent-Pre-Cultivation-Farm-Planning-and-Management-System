import 'dart:io';

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
  List<InspectionRecord> inspections = [];
  List<CropIssueRecord> cropIssues = [];
  List<FollowUpRecommendationRecord> followUps = [];
  List<InspectionHistoryEventRecord> activeInspectionHistory = [];
  String? activeInspectionId;
  String? lastCropPlanRequestId;
  CropPlanningWorkflowStart? lastWorkflowStart;
  CropPlanningWorkflowStatus? lastWorkflowStatus;
  CropPlanningResult? lastPlanningResult;
  String? error;
  bool isBusy = false;
  String? lastPhotoName;
  XFile? selectedInspectionPhoto;
  Position? lastPosition;

  bool get isAuthenticated => user != null;
  bool get isFieldOfficer => user?.role == 2 || user?.role == 4 || user?.role == 5;

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
    inspections = await _apiClient.inspections();
    cropIssues = await _apiClient.cropIssues();
    followUps = await _apiClient.followUps();
  }

  Future<void> startInspection({required String fieldId, required String summary}) async {
    await _guard(() async {
      final locationText = lastPosition == null ? '' : '\nGPS: ${lastPosition!.latitude}, ${lastPosition!.longitude}';
      activeInspectionId = await _apiClient.createInspection(fieldId: fieldId, summary: '$summary$locationText');
      await refreshInspectionHistory();
      await refresh();
    });
  }

  Future<void> addObservation({required String observationType, required String notes}) async {
    final inspectionId = activeInspectionId;
    if (inspectionId == null) {
      error = 'Start or select an inspection before adding observations.';
      notifyListeners();
      return;
    }
    await _guard(() async {
      await _apiClient.createObservation(inspectionId: inspectionId, observationType: observationType, notes: notes);
      await refreshInspectionHistory();
    });
  }

  Future<void> reportCropIssue({required String title, required String description, required int severity}) async {
    final inspectionId = activeInspectionId;
    if (inspectionId == null) {
      error = 'Start or select an inspection before reporting crop issues.';
      notifyListeners();
      return;
    }
    await _guard(() async {
      await _apiClient.createCropIssue(inspectionId: inspectionId, title: title, description: description, severity: severity);
      await refresh();
      await refreshInspectionHistory();
    });
  }

  Future<void> submitActiveInspection() async {
    final inspectionId = activeInspectionId;
    if (inspectionId == null) {
      error = 'No active inspection is selected.';
      notifyListeners();
      return;
    }
    await _guard(() async {
      if (selectedInspectionPhoto != null) {
        await _apiClient.uploadInspectionImage(inspectionId: inspectionId, imageFile: File(selectedInspectionPhoto!.path));
      }
      await _apiClient.submitInspection(inspectionId);
      await refresh();
      await refreshInspectionHistory();
    });
  }

  Future<void> selectInspection(String inspectionId) async {
    activeInspectionId = inspectionId;
    await refreshInspectionHistory();
    notifyListeners();
  }

  Future<void> refreshInspectionHistory() async {
    final inspectionId = activeInspectionId;
    if (inspectionId == null) return;
    activeInspectionHistory = await _apiClient.inspectionHistory(inspectionId);
    notifyListeners();
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
    selectedInspectionPhoto = photo;
    lastPhotoName = photo?.name;
    notifyListeners();
  }

  Future<void> pickInspectionPhoto() async {
    final photo = await _imagePicker.pickImage(source: ImageSource.gallery, imageQuality: 82);
    selectedInspectionPhoto = photo;
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
