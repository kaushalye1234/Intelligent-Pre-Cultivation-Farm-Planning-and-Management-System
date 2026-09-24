import 'package:flutter/foundation.dart';

import '../models/api_models.dart';
import '../services/api_client.dart';

/// Inventory and reservation data for the Resource Officer screens.
///
/// [canManage] mirrors the backend rule (ResourceOfficer and Admin may release or cancel any
/// reservation). The backend still enforces it; this only keeps unauthorized actions out of the UI.
class ResourceState extends ChangeNotifier {
  ResourceState({required this.apiClient, required this.canManage});

  static const _pageSize = 20;

  final ApiClient apiClient;
  final bool canManage;

  List<StockRecord> stocks = [];
  bool stocksLoading = false;
  String? stocksError;
  bool stocksHasMore = false;
  int _stocksPage = 0;
  int _stocksRequest = 0;
  String search = '';
  bool lowStockOnly = false;

  List<ReservationRecord> reservations = [];
  bool reservationsLoading = false;
  String? reservationsError;
  bool reservationsHasMore = false;
  int _reservationsPage = 0;
  int _reservationsRequest = 0;
  int? reservationStatus;

  Future<void> loadStocks() => _loadStocks(reset: true);

  Future<void> loadMoreStocks() {
    if (stocksLoading || !stocksHasMore) return Future.value();
    return _loadStocks(reset: false);
  }

  Future<void> setSearch(String value) {
    search = value.trim();
    return loadStocks();
  }

  Future<void> setLowStockOnly(bool value) {
    lowStockOnly = value;
    return loadStocks();
  }

  Future<void> _loadStocks({required bool reset}) async {
    final request = ++_stocksRequest;
    stocksLoading = true;
    stocksError = null;
    if (reset) {
      stocks = [];
      stocksHasMore = false;
      _stocksPage = 0;
    }
    notifyListeners();
    try {
      final result = await apiClient.resourceStocks(
        search: search,
        lowStockOnly: lowStockOnly,
        page: _stocksPage + 1,
        pageSize: _pageSize,
      );
      if (request != _stocksRequest) return; // a newer search replaced this one
      _stocksPage = result.page;
      stocksHasMore = result.hasMore;
      stocks = [...stocks, ...result.items];
    } catch (error) {
      if (request != _stocksRequest) return;
      stocksError = error.toString();
    } finally {
      if (request == _stocksRequest) {
        stocksLoading = false;
        notifyListeners();
      }
    }
  }

  Future<void> loadReservations() => _loadReservations(reset: true);

  Future<void> loadMoreReservations() {
    if (reservationsLoading || !reservationsHasMore) return Future.value();
    return _loadReservations(reset: false);
  }

  Future<void> setReservationStatus(int? status) {
    reservationStatus = status;
    return loadReservations();
  }

  Future<void> _loadReservations({required bool reset}) async {
    final request = ++_reservationsRequest;
    reservationsLoading = true;
    reservationsError = null;
    if (reset) {
      reservations = [];
      reservationsHasMore = false;
      _reservationsPage = 0;
    }
    notifyListeners();
    try {
      final result = await apiClient.resourceReservations(
        status: reservationStatus,
        page: _reservationsPage + 1,
        pageSize: _pageSize,
      );
      if (request != _reservationsRequest) return;
      _reservationsPage = result.page;
      reservationsHasMore = result.hasMore;
      reservations = [...reservations, ...result.items];
    } catch (error) {
      if (request != _reservationsRequest) return;
      reservationsError = error.toString();
    } finally {
      if (request == _reservationsRequest) {
        reservationsLoading = false;
        notifyListeners();
      }
    }
  }

  Future<List<StockTransactionRecord>> stockHistory(String stockId) =>
      apiClient.stockHistory(stockId);

  /// Returns null on success, otherwise a message safe to show the officer.
  Future<String?> releaseReservation(String reservationId) =>
      _closeReservation(reservationId, apiClient.releaseReservation);

  Future<String?> cancelReservation(String reservationId) =>
      _closeReservation(reservationId, apiClient.cancelReservation);

  Future<String?> _closeReservation(
    String reservationId,
    Future<ReservationRecord> Function(String) action,
  ) async {
    if (!canManage) return 'Your account cannot change reservations.';
    try {
      await action(reservationId);
    } catch (error) {
      return error.toString();
    }
    // Stock quantities changed, so both lists are refreshed.
    await Future.wait([loadReservations(), loadStocks()]);
    return null;
  }
}
