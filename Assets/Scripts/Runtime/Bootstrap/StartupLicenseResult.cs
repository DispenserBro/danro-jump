namespace DanroJump.Bootstrap
{
    public readonly struct StartupLicenseResult
    {
        private StartupLicenseResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }
        public string Message { get; }

        public static StartupLicenseResult Valid(string message = "")
        {
            return new StartupLicenseResult(true, message);
        }

        public static StartupLicenseResult Failed(string message)
        {
            return new StartupLicenseResult(false, message);
        }
    }
}
