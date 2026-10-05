import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/app_config.dart';

/// Minimal HTTP client for skeleton screens. Replace with OpenAPI-generated Dart when ready.
class AdeniApiClient {
  AdeniApiClient({AppConfig? config, http.Client? httpClient})
      : _config = config ?? AppConfig.fromEnvironment(),
        _http = httpClient ?? http.Client();

  final AppConfig _config;
  final http.Client _http;

  Uri _uri(String path) {
    final base = _config.apiBaseUrl.replaceAll(RegExp(r'/+$'), '');
    final normalized = path.startsWith('/') ? path : '/$path';
    return Uri.parse('$base$normalized');
  }

  Future<HealthResult> fetchHealth() async {
    final response = await _http.get(_uri('/health'));
    if (response.statusCode != 200) {
      throw AdeniApiException(
        'Health check failed (${response.statusCode})',
      );
    }
    final body = jsonDecode(response.body) as Map<String, dynamic>;
    return HealthResult(
      status: body['status'] as String? ?? 'unknown',
      checks: Map<String, String>.from(
        (body['checks'] as Map<String, dynamic>? ?? {}).map(
          (k, v) => MapEntry(k, v.toString()),
        ),
      ),
    );
  }

  /// Placeholder until Auth0 + tenant headers are wired (see spec).
  Future<List<TenantBookingStub>> fetchTenantBookingsStub() async {
    throw UnimplementedError(
      'Requires business JWT and X-Tenant-Id — use portal or wire Auth0 in phase 2.',
    );
  }

  void dispose() => _http.close();
}

class HealthResult {
  const HealthResult({required this.status, required this.checks});

  final String status;
  final Map<String, String> checks;
}

class TenantBookingStub {
  const TenantBookingStub({
    required this.id,
    required this.serviceName,
    required this.startAt,
    required this.status,
  });

  final String id;
  final String serviceName;
  final DateTime startAt;
  final String status;
}

class AdeniApiException implements Exception {
  AdeniApiException(this.message);

  final String message;

  @override
  String toString() => message;
}
