/// Runtime configuration (override with `--dart-define=ADENI_API_URL=...`).
class AppConfig {
  const AppConfig({required this.apiBaseUrl});

  final String apiBaseUrl;

  static AppConfig fromEnvironment() {
    const fromDefine = String.fromEnvironment('ADENI_API_URL');
    return AppConfig(
      apiBaseUrl: fromDefine.isNotEmpty
          ? fromDefine
          : _defaultBaseUrl(),
    );
  }

  /// Android emulator → host machine; iOS sim / desktop → localhost.
  static String _defaultBaseUrl() {
    // ignore: avoid_print
    const hostOverride = String.fromEnvironment('ADENI_API_HOST');
    if (hostOverride.isNotEmpty) {
      return 'http://$hostOverride:5169';
    }
    return 'http://localhost:5169';
  }
}
