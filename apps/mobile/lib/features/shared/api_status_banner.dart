import 'package:flutter/material.dart';

import '../../core/api/adeni_api_client.dart';

class ApiStatusBanner extends StatefulWidget {
  const ApiStatusBanner({super.key, required this.api});

  final AdeniApiClient api;

  @override
  State<ApiStatusBanner> createState() => _ApiStatusBannerState();
}

class _ApiStatusBannerState extends State<ApiStatusBanner> {
  String? _status;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _refresh();
  }

  Future<void> _refresh() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final health = await widget.api.fetchHealth();
      setState(() {
        _status = health.status;
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    Color bg;
    String label;
    if (_loading) {
      bg = theme.colorScheme.surfaceContainerHighest;
      label = 'Checking API…';
    } else if (_error != null) {
      bg = theme.colorScheme.errorContainer;
      label = 'API unreachable: $_error';
    } else {
      bg = _status == 'healthy'
          ? theme.colorScheme.primaryContainer
          : theme.colorScheme.tertiaryContainer;
      label = 'API status: $_status';
    }

    return Material(
      color: bg,
      child: InkWell(
        onTap: _refresh,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          child: Row(
            children: [
              Expanded(child: Text(label, style: theme.textTheme.bodyMedium)),
              if (_loading)
                const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              else
                Icon(Icons.refresh, size: 20, color: theme.colorScheme.primary),
            ],
          ),
        ),
      ),
    );
  }
}
