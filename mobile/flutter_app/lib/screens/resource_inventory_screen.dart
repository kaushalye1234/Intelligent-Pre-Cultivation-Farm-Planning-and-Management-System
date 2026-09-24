import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/api_models.dart';
import '../state/resource_state.dart';
import '../ui/agri_theme.dart';
import '../ui/journey_date.dart';
import '../ui/journey_widgets.dart';

String formatQuantity(num value) =>
    value == value.roundToDouble() ? value.toInt().toString() : value.toStringAsFixed(2);

class ResourceInventoryScreen extends StatefulWidget {
  const ResourceInventoryScreen({super.key});

  @override
  State<ResourceInventoryScreen> createState() =>
      _ResourceInventoryScreenState();
}

class _ResourceInventoryScreenState extends State<ResourceInventoryScreen> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _search(ResourceState state) {
    state.setSearch(_searchController.text);
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<ResourceState>();
    return RefreshIndicator(
      onRefresh: state.loadStocks,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
        children: [
          const JourneyPageIntro(
            eyebrow: 'INVENTORY',
            title: 'Resource stock',
            subtitle: 'Look up what is available and what is running low.',
          ),
          const SizedBox(height: 18),
          TextField(
            controller: _searchController,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              labelText: 'Search resources',
              prefixIcon: const Icon(Icons.search_rounded),
              suffixIcon: state.search.isEmpty && _searchController.text.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'Clear search',
                      icon: const Icon(Icons.close_rounded),
                      onPressed: () {
                        _searchController.clear();
                        state.setSearch('');
                      },
                    ),
            ),
            onSubmitted: (_) => _search(state),
          ),
          const SizedBox(height: 10),
          Align(
            alignment: Alignment.centerLeft,
            child: FilterChip(
              label: const Text('Low stock only'),
              selected: state.lowStockOnly,
              onSelected: state.setLowStockOnly,
            ),
          ),
          const SizedBox(height: 14),
          if (state.stocksError != null && state.stocks.isEmpty)
            JourneyEmptyState(
              icon: Icons.cloud_off_outlined,
              title: 'Inventory unavailable',
              message: state.stocksError!,
              action: FilledButton.icon(
                onPressed: state.loadStocks,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('Try again'),
              ),
            )
          else if (state.stocksLoading && state.stocks.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (state.stocks.isEmpty)
            JourneyEmptyState(
              icon: Icons.inventory_2_outlined,
              title: state.search.isNotEmpty || state.lowStockOnly
                  ? 'No matching resources'
                  : 'No stock records yet',
              message: state.search.isNotEmpty || state.lowStockOnly
                  ? 'Try a different search or clear the filters.'
                  : 'Stock will appear here once resources have been stocked.',
            )
          else ...[
            for (final stock in state.stocks) ...[
              _StockCard(stock: stock),
              const SizedBox(height: 10),
            ],
            if (state.stocksError != null)
              JourneyNotice(
                message: state.stocksError!,
                tone: JourneyTone.danger,
              ),
            if (state.stocksHasMore)
              Center(
                child: state.stocksLoading
                    ? const Padding(
                        padding: EdgeInsets.all(12),
                        child: CircularProgressIndicator(),
                      )
                    : OutlinedButton(
                        onPressed: state.loadMoreStocks,
                        child: const Text('Load more'),
                      ),
              ),
          ],
        ],
      ),
    );
  }
}

class _StockCard extends StatelessWidget {
  const _StockCard({required this.stock});

  final StockRecord stock;

  @override
  Widget build(BuildContext context) {
    final (label, tone) = stock.isOutOfStock
        ? ('Out of stock', JourneyTone.danger)
        : stock.isLowStock
        ? ('Low stock', JourneyTone.warning)
        : ('In stock', JourneyTone.success);
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: () => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        showDragHandle: true,
        builder: (sheetContext) => ChangeNotifierProvider<ResourceState>.value(
          value: context.read<ResourceState>(),
          child: StockDetailSheet(stock: stock),
        ),
      ),
      child: JourneyCard(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    stock.resourceName,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${formatQuantity(stock.availableQuantity)} ${stock.unit} available',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'On hand ${formatQuantity(stock.quantityOnHand)} · Reserved ${formatQuantity(stock.reservedQuantity)}',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ],
              ),
            ),
            const SizedBox(width: 10),
            JourneyStatusPill(label, tone: tone),
          ],
        ),
      ),
    );
  }
}

class StockDetailSheet extends StatefulWidget {
  const StockDetailSheet({super.key, required this.stock});

  final StockRecord stock;

  @override
  State<StockDetailSheet> createState() => _StockDetailSheetState();
}

class _StockDetailSheetState extends State<StockDetailSheet> {
  late Future<List<StockTransactionRecord>> _history;

  @override
  void initState() {
    super.initState();
    _history = context.read<ResourceState>().stockHistory(widget.stock.id);
  }

  void _retry() {
    setState(() {
      _history = context.read<ResourceState>().stockHistory(widget.stock.id);
    });
  }

  @override
  Widget build(BuildContext context) {
    final stock = widget.stock;
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              stock.resourceName,
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            JourneyInfoRow(
              label: 'Available',
              value: '${formatQuantity(stock.availableQuantity)} ${stock.unit}',
              icon: Icons.inventory_2_outlined,
            ),
            JourneyInfoRow(
              label: 'On hand',
              value: '${formatQuantity(stock.quantityOnHand)} ${stock.unit}',
            ),
            JourneyInfoRow(
              label: 'Reserved',
              value: '${formatQuantity(stock.reservedQuantity)} ${stock.unit}',
            ),
            JourneyInfoRow(
              label: 'Low-stock threshold',
              value: '${formatQuantity(stock.lowStockThreshold)} ${stock.unit}',
            ),
            if (stock.isOutOfStock || stock.isLowStock) ...[
              const SizedBox(height: 8),
              JourneyNotice(
                title: stock.isOutOfStock ? 'Out of stock' : 'Low stock',
                message: stock.isOutOfStock
                    ? 'Nothing is available to reserve.'
                    : 'Available quantity is at or below the low-stock threshold.',
                tone: stock.isOutOfStock
                    ? JourneyTone.danger
                    : JourneyTone.warning,
              ),
            ],
            const SizedBox(height: 18),
            Text(
              'Recent stock activity',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            FutureBuilder<List<StockTransactionRecord>>(
              future: _history,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                if (snapshot.hasError) {
                  return JourneyEmptyState(
                    icon: Icons.cloud_off_outlined,
                    title: 'History unavailable',
                    message: snapshot.error.toString(),
                    action: OutlinedButton(
                      onPressed: _retry,
                      child: const Text('Try again'),
                    ),
                  );
                }
                final history = snapshot.data ?? const [];
                if (history.isEmpty) {
                  return const JourneyEmptyState(
                    icon: Icons.history_rounded,
                    title: 'No stock activity',
                    message: 'No changes have been recorded for this stock.',
                  );
                }
                return Column(
                  children: [
                    for (final entry in history)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 7),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    '${entry.typeLabel} · ${formatQuantity(entry.quantity)} ${stock.unit}',
                                    style: Theme.of(context).textTheme.titleSmall,
                                  ),
                                  if (entry.note.isNotEmpty)
                                    Text(
                                      entry.note,
                                      style: Theme.of(context).textTheme.bodySmall,
                                    ),
                                ],
                              ),
                            ),
                            Text(
                              journeyDate(entry.createdAt, includeTime: true),
                              style: Theme.of(context).textTheme.bodySmall
                                  ?.copyWith(color: AgriColors.muted),
                            ),
                          ],
                        ),
                      ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
