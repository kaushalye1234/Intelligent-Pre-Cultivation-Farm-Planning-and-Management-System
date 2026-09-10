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
                initialValue: _stockId,
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