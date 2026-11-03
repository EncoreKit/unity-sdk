namespace EncoreKit
{
    public enum EntitlementType { FreeTrial, Discount, Credit }

    public enum EntitlementUnit { Months, Days, Percent, Dollars }

    // Which entitlement set to read. On iOS 2.5.0, Verified also counts provisional grants.
    public enum EntitlementScope { All, Verified }

    // The Swift SDK's Entitlement. iOS 2.5.0 matches on the type only and ignores Value and Unit.
    public sealed class Entitlement
    {
        public EntitlementType Type;
        public double? Value;
        public EntitlementUnit? Unit;

        public static Entitlement FreeTrial(double? value = null, EntitlementUnit? unit = null) =>
            new Entitlement { Type = EntitlementType.FreeTrial, Value = value, Unit = unit };

        public static Entitlement Discount(double? value = null, EntitlementUnit? unit = null) =>
            new Entitlement { Type = EntitlementType.Discount, Value = value, Unit = unit };

        public static Entitlement Credit(double? value = null, EntitlementUnit? unit = null) =>
            new Entitlement { Type = EntitlementType.Credit, Value = value, Unit = unit };
    }

    // The result of a call that has no value, such as RevokeEntitlements.
    public sealed class EncoreCallResult
    {
        public bool Success;
        public string ErrorType;
        public string ErrorMessage;
    }
}
