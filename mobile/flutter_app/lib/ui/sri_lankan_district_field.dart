import 'package:flutter/material.dart';

import '../constants/sri_lankan_districts.dart';

class SriLankanDistrictField extends StatefulWidget {
  const SriLankanDistrictField({
    required this.onChanged,
    this.value,
    this.enabled = true,
    super.key,
  });

  final String? value;
  final bool enabled;
  final ValueChanged<String?> onChanged;

  @override
  State<SriLankanDistrictField> createState() => _SriLankanDistrictFieldState();
}

class _SriLankanDistrictFieldState extends State<SriLankanDistrictField> {
  late final TextEditingController _controller;
  String? _selectedDistrict;

  @override
  void initState() {
    super.initState();
    _selectedDistrict = canonicalSriLankanDistrict(widget.value);
    _controller = TextEditingController(text: _selectedDistrict ?? '');
  }

  @override
  void didUpdateWidget(covariant SriLankanDistrictField oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.value == widget.value) return;
    _selectedDistrict = canonicalSriLankanDistrict(widget.value);
    _controller.text = _selectedDistrict ?? '';
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FormField<String>(
      initialValue: _selectedDistrict,
      validator: (_) {
        final typedDistrict = canonicalSriLankanDistrict(_controller.text);
        if (_selectedDistrict == null || typedDistrict != _selectedDistrict) {
          return 'Select a Sri Lankan district';
        }
        return null;
      },
      builder: (field) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          LayoutBuilder(
            builder: (context, constraints) => DropdownMenu<String>(
              controller: _controller,
              width: constraints.maxWidth,
              enabled: widget.enabled,
              enableFilter: true,
              requestFocusOnTap: true,
              label: const Text('District'),
              hintText: 'Search district...',
              leadingIcon: const Icon(Icons.map_outlined),
              errorText: field.errorText,
              dropdownMenuEntries: sriLankanDistricts
                  .map(
                    (district) => DropdownMenuEntry<String>(
                      value: district,
                      label: district,
                    ),
                  )
                  .toList(),
              onSelected: (district) {
                setState(() => _selectedDistrict = district);
                field.didChange(district);
                widget.onChanged(district);
              },
            ),
          ),
        ],
      ),
    );
  }
}
