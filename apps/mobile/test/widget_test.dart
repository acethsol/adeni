import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/main.dart';

void main() {
  testWidgets('shows mode launcher', (tester) async {
    await tester.pumpWidget(const AdeniBusinessApp());
    expect(find.text('Adeni Business'), findsOneWidget);
    expect(find.text('Owner'), findsOneWidget);
    expect(find.text('Employee'), findsOneWidget);
    expect(find.text('Front desk'), findsOneWidget);
  });
}
