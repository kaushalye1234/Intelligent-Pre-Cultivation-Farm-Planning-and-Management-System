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
  AuthenticationSession? passwordChangeSession;
  FarmerOnboardingStatus? farmerOnboarding;
  DashboardSummary summary = DashboardSummary.empty();
  List<FarmOption> farms = [];
  List<FieldOption> fields = [];
  List<CropTypeOption> cropTypes = [];
  List<InventoryStock> stocks = [];
  List<InspectionRecord> inspections = [];
  List<CropIssueRecord> cropIssues = [];
  List<FollowUpRecommendationRecord> followUps = [];
  List<FarmTaskRecord> tasks = [];
  List<IrrigationScheduleRecord> irrigationSchedules = [];
  List<ApprovalHistoryRecord> approvalHistory = [];
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
  bool get requiresTemporaryPasswordChange =>
      passwordChangeSession?.requiresPasswordChange ?? false;
  bool get isFieldOfficer =>
      user?.role == 2 || user?.role == 4 || user?.role == 5;

  Future<void> restoreSession() async {
    isBusy = true;
    notifyListeners();
    try {
      final token = await _apiClient.loadToken();
      if (token != null) {
        user = await _apiClient.profile();
        await _loadAuthenticatedLanding();
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
      final session = await _apiClient.login(email, password);
      if (session.requiresPasswordChange) {
        passwordChangeSession = session;
        user = null;
        farmerOnboarding = null;
        return;
      }

      passwordChangeSession = null;
      user = session.user;
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> registerFarmer({
    required String fullName,
    required String email,
    required String password,
  }) async {
    await _guard(() async {
      final session = await _apiClient.registerFarmer(
        fullName: fullName,
        email: email,
        password: password,
      );
      passwordChangeSession = null;
      user = session.user;
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> changeTemporaryPassword(String newPassword) async {
    final session = passwordChangeSession;
    if (session?.passwordChangeToken == null) {
      error = 'Your password-change session has ended. Sign in again.';
      passwordChangeSession = null;
      notifyListeners();
      return;
    }

    await _guard(() async {
      final authenticatedSession = await _apiClient.changeTemporaryPassword(
        passwordChangeToken: session!.passwordChangeToken!,
        newPassword: newPassword,
      );
      passwordChangeSession = null;
      user = authenticatedSession.user;
      await _loadAuthenticatedLanding();
    });
  }

  void cancelTemporaryPasswordChange() {
    passwordChangeSession = null;
    error = null;
    notifyListeners();
  }

  Future<void> logout() async {
    await _apiClient.clearToken();
    user = null;
    passwordChangeSession = null;
    farmerOnboarding = null;
    notifyListeners();
  }

  Future<void> createOnboardingFarm({
    required String name,
    required String location,
    required num totalArea,
  }) async {
    await _guard(() async {
      await _apiClient.createFarm(
        name: name,
        location: location,
        totalArea: totalArea,
      );
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> createOnboardingField({
    required String farmId,
    required String name,
    required num area,
    required String soilType,
  }) async {
    await _guard(() async {
      await _apiClient.createField(
        farmId: farmId,
        name: name,
        area: area,
        soilType: soilType,
      );
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> retryAuthenticatedLanding() async {
    await _guard(_loadAuthenticatedLanding);
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
    tasks = await _apiClient.tasks();
    irrigationSchedules = await _apiClient.irrigationSchedules();
    approvalHistory = await _apiClient.approvalHistory();
  }

  Future<void> _loadAuthenticatedLanding() async {
    if (user?.role != 1) {
      farmerOnboarding = null;
      await refresh();
      return;
    }

    farmerOnboarding = await _apiClient.farmerOnboardingStatus();
    if (farmerOnboarding?.stage == 'field') {
      farms = await _apiClient.farms();
      return;
    }

    if (farmerOnboarding?.stage == 'complete') {
      await refresh();
    }
  }

  Future<void> startInspection({
    required String fieldId,
    required String summary,
  }) async {
    await _guard(() async {
      final locationText = lastPosition == null
          ? ''
          : '\nGPS: ${lastPosition!.latitude}, ${lastPosition!.longitude}';
      activeInspectionId = await _apiClient.createInspection(
        fieldId: fieldId,
        summary: '$summary$locationText',
      );
      await refreshInspectionHistory();
      await refresh();
    });
  }

  Future<void> addObservation({
    required String observationType,
    required String notes,
  }) async {
    final inspectionId = activeInspectionId;
    if (inspectionId == null) {
      error = 'Start or select an inspection before adding observations.';
      notifyListeners();
      return;
    }
    await _guard(() async {
      await _apiClient.createObservation(
        inspectionId: inspectionId,
        observationType: observationType,
        notes: notes,
      );
      await refreshInspectionHistory();
    });
  }

  Future<void> reportCropIssue({
    required String title,
    required String description,
    required int severity,
  }) async {
    final inspectionId = activeInspectionId;
    if (inspectionId == null) {
      error = 'Start or select an inspection before reporting crop issues.';
      notifyListeners();
      return;
    }
    await _guard(() async {
      await _apiClient.createCropIssue(
        inspectionId: inspectionId,
        title: title,
        description: description,
        severity: severity,
      );
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
        await _apiClient.uploadInspectionImage(
          inspectionId: inspectionId,
          imageFile: File(selectedInspectionPhoto!.path),
        );
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

  Future<void> reserveResource({
    required String stockId,
    required num quantity,
    required String purpose,
  }) async {
    await _guard(() async {
      await _apiClient.reserveResource(
        inventoryStockId: stockId,
        quantity: quantity,
        purpose: purpose,
      );
      await refresh();
    });
  }

  Future<void> captureInspectionPhoto() async {
    final photo = await _imagePicker.pickImage(
      source: ImageSource.camera,
      imageQuality: 82,
    );
    selectedInspectionPhoto = photo;
    lastPhotoName = photo?.name;
    notifyListeners();
  }

  Future<void> pickInspectionPhoto() async {
    final photo = await _imagePicker.pickImage(
      source: ImageSource.gallery,
      imageQuality: 82,
    );
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

    if (permission == LocationPermission.denied ||
        permission == LocationPermission.deniedForever) {
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
