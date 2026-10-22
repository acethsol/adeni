namespace Adeni.Infrastructure.Reporting;

using QuestPDF.Infrastructure;

internal static class QuestPdfLicenseInitializer
{
    private static readonly object Lock = new();
    private static bool _initialized;
    private static Exception? _initError;

    internal static void EnsureReady()
    {
        if (_initialized)
        {
            if (_initError is not null)
            {
                throw new InvalidOperationException(
                    "PDF generation is unavailable on this runtime.",
                    _initError);
            }

            return;
        }

        lock (Lock)
        {
            if (_initialized)
            {
                if (_initError is not null)
                {
                    throw new InvalidOperationException(
                        "PDF generation is unavailable on this runtime.",
                        _initError);
                }

                return;
            }

            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
            }
            catch (Exception ex)
            {
                _initError = ex;
            }
            finally
            {
                _initialized = true;
            }

            if (_initError is not null)
            {
                throw new InvalidOperationException(
                    "PDF generation is unavailable on this runtime.",
                    _initError);
            }
        }
    }
}
