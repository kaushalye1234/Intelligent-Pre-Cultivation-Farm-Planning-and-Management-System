import 'package:flutter/material.dart';

import 'agri_theme.dart';

enum JourneyTone { neutral, success, warning, danger }

class JourneyCard extends StatelessWidget {
  const JourneyCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(20),
    this.color,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? color;

  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    padding: padding,
    decoration: BoxDecoration(
      color: color ?? AgriColors.surface,
      borderRadius: BorderRadius.circular(22),
      border: Border.all(color: AgriColors.border),
      boxShadow: const [
        BoxShadow(
          color: Color(0x0B173F32),
          blurRadius: 22,
          offset: Offset(0, 8),
        ),
      ],
    ),
    child: child,
  );
}

class JourneyEyebrow extends StatelessWidget {
  const JourneyEyebrow(
    this.label, {
    super.key,
    this.color = AgriColors.forestLight,
  });

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Text(
    label.toUpperCase(),
    style: TextStyle(
      color: color,
      fontSize: 11,
      fontWeight: FontWeight.w800,
      letterSpacing: 1.5,
    ),
  );
}

class JourneyPageIntro extends StatelessWidget {
  const JourneyPageIntro({
    super.key,
    required this.eyebrow,
    required this.title,
    this.subtitle,
    this.trailing,
  });

  final String eyebrow;
  final String title;
  final String? subtitle;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      JourneyEyebrow(eyebrow),
      const SizedBox(height: 8),
      Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Text(
              title,
              style: Theme.of(context).textTheme.headlineMedium,
            ),
          ),
          ?trailing,
        ],
      ),
      if (subtitle != null) ...[
        const SizedBox(height: 8),
        Text(subtitle!, style: Theme.of(context).textTheme.bodyMedium),
      ],
    ],
  );
}

class JourneySectionHeading extends StatelessWidget {
  const JourneySectionHeading({
    super.key,
    required this.title,
    this.subtitle,
    this.trailing,
  });

  final String title;
  final String? subtitle;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) => Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleLarge),
            if (subtitle != null) ...[
              const SizedBox(height: 3),
              Text(subtitle!, style: Theme.of(context).textTheme.bodyMedium),
            ],
          ],
        ),
      ),
      ?trailing,
    ],
  );
}

class JourneyStatusPill extends StatelessWidget {
  const JourneyStatusPill(
    this.label, {
    super.key,
    this.tone = JourneyTone.neutral,
  });

  final String label;
  final JourneyTone tone;

  @override
  Widget build(BuildContext context) {
    final (foreground, background) = switch (tone) {
      JourneyTone.success => (AgriColors.forest, AgriColors.sage),
      JourneyTone.warning => (AgriColors.amber, AgriColors.amberSoft),
      JourneyTone.danger => (AgriColors.danger, AgriColors.dangerSoft),
      JourneyTone.neutral => (AgriColors.muted, AgriColors.canvas),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(30),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: foreground,
          fontSize: 11,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class JourneyInfoRow extends StatelessWidget {
  const JourneyInfoRow({
    super.key,
    required this.label,
    required this.value,
    this.icon,
  });

  final String label;
  final String value;
  final IconData? icon;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 8),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (icon != null) ...[
          Icon(icon, size: 19, color: AgriColors.forestLight),
          const SizedBox(width: 11),
        ],
        Expanded(
          child: Text(label, style: Theme.of(context).textTheme.bodyMedium),
        ),
        const SizedBox(width: 12),
        Flexible(
          child: Text(
            value,
            textAlign: TextAlign.right,
            style: Theme.of(context).textTheme.titleMedium,
          ),
        ),
      ],
    ),
  );
}

class JourneyEmptyState extends StatelessWidget {
  const JourneyEmptyState({
    super.key,
    required this.icon,
    required this.title,
    required this.message,
    this.action,
  });

  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) => JourneyCard(
    child: Column(
      children: [
        CircleAvatar(
          radius: 29,
          backgroundColor: AgriColors.sage,
          child: Icon(icon, color: AgriColors.forest, size: 27),
        ),
        const SizedBox(height: 16),
        Text(
          title,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 6),
        Text(
          message,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        if (action != null) ...[const SizedBox(height: 18), action!],
      ],
    ),
  );
}

class JourneyNotice extends StatelessWidget {
  const JourneyNotice({
    super.key,
    required this.message,
    this.title,
    this.tone = JourneyTone.warning,
  });

  final String? title;
  final String message;
  final JourneyTone tone;

  @override
  Widget build(BuildContext context) {
    final danger = tone == JourneyTone.danger;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: danger ? AgriColors.dangerSoft : AgriColors.amberSoft,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            danger ? Icons.error_outline_rounded : Icons.info_outline_rounded,
            color: danger ? AgriColors.danger : AgriColors.amber,
            size: 21,
          ),
          const SizedBox(width: 11),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (title != null)
                  Text(title!, style: Theme.of(context).textTheme.titleMedium),
                if (title != null) const SizedBox(height: 3),
                Text(
                  message,
                  style: Theme.of(
                    context,
                  ).textTheme.bodyMedium?.copyWith(color: AgriColors.ink),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class JourneyAuthLayout extends StatelessWidget {
  const JourneyAuthLayout({
    super.key,
    required this.eyebrow,
    required this.title,
    required this.subtitle,
    required this.child,
    this.showBack = false,
    this.actions,
    this.progressLabel,
    this.progress,
  });

  final String eyebrow;
  final String title;
  final String subtitle;
  final Widget child;
  final bool showBack;
  final List<Widget>? actions;
  final String? progressLabel;
  final double? progress;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: showBack || actions != null
        ? AppBar(
            title: const Text('AgriAssist'),
            automaticallyImplyLeading: showBack,
            actions: actions,
          )
        : null,
    body: SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 36),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const _JourneyBrandPanel(),
                const SizedBox(height: 30),
                if (progressLabel != null) ...[
                  JourneyEyebrow(progressLabel!),
                  const SizedBox(height: 12),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(8),
                    child: LinearProgressIndicator(
                      value: progress,
                      minHeight: 5,
                      backgroundColor: AgriColors.border,
                    ),
                  ),
                  const SizedBox(height: 24),
                ] else ...[
                  JourneyEyebrow(eyebrow),
                  const SizedBox(height: 8),
                ],
                Text(title, style: Theme.of(context).textTheme.headlineMedium),
                const SizedBox(height: 8),
                Text(subtitle, style: Theme.of(context).textTheme.bodyMedium),
                const SizedBox(height: 26),
                child,
              ],
            ),
          ),
        ),
      ),
    ),
  );
}

class _JourneyBrandPanel extends StatelessWidget {
  const _JourneyBrandPanel();

  @override
  Widget build(BuildContext context) => Container(
    height: 138,
    decoration: BoxDecoration(
      color: AgriColors.forest,
      borderRadius: BorderRadius.circular(26),
    ),
    clipBehavior: Clip.antiAlias,
    child: Stack(
      children: [
        Positioned.fill(child: CustomPaint(painter: _FieldMotifPainter())),
        const Positioned(
          left: 22,
          top: 20,
          child: Row(
            children: [
              Icon(Icons.spa_rounded, color: Color(0xFFD9E9D6), size: 27),
              SizedBox(width: 9),
              Text(
                'AgriAssist',
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 20,
                  fontWeight: FontWeight.w800,
                  letterSpacing: -0.4,
                ),
              ),
            ],
          ),
        ),
        const Positioned(
          left: 22,
          bottom: 18,
          child: Text(
            'A clearer way to grow.',
            style: TextStyle(
              color: Color(0xFFE4EEE5),
              fontSize: 14,
              fontWeight: FontWeight.w500,
            ),
          ),
        ),
      ],
    ),
  );
}

class _FieldMotifPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = const Color(0x50C7DFCB)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2;
    final sun = Paint()..color = const Color(0xD9D4AA62);
    canvas.drawCircle(Offset(size.width - 75, 48), 17, sun);
    for (var i = 0; i < 5; i++) {
      final y = 88.0 + i * 13;
      final path = Path()
        ..moveTo(size.width * 0.43, size.height + 13)
        ..quadraticBezierTo(size.width * 0.68, y - 30, size.width + 10, y);
      canvas.drawPath(path, paint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
