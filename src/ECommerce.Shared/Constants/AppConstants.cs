namespace ECommerce.Shared.Constants;

public static class AppConstants
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Customer = "Customer";
    }

    public static class Cors
    {
        public const string PolicyName = "AllowedClients";
    }

    public static class RateLimiting
    {
        public const string AuthPolicy = "AuthRateLimit";
        public const string StrictAuthPolicy = "StrictAuthRateLimit";
    }

    public static class FileStorage
    {
        public const string ProductsFolder = "products";
        public const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB
        public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    }
}
