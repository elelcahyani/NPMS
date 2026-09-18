namespace NPMS.Core.Models
{
    /// <summary>
    /// Centralized constants for product status values.
    /// Use these instead of hardcoded strings throughout the codebase.
    /// </summary>
    public static class ProductStatus
    {
        public const string Draft         = "Draft";
        public const string InDevelopment = "In Development";
        public const string PendingReview = "Pending Review";
        public const string Released      = "Released";
        public const string Obsolete      = "Obsolete";
        public const string Discontinued  = "Discontinued";

        public static readonly string[] All =
        [
            Draft, InDevelopment, PendingReview, Released, Obsolete, Discontinued
        ];
    }
}
