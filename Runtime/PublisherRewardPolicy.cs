using System;

namespace EncoreKit
{
    // Limits on how often a PublisherReward can be earned. Set Encore.PublisherRewardPolicy. Encore's server enforces
    // them per identified user: one prize per offer, ever, and DailyLimit (clamped to 1 to 10) per rolling 24 hours.
    public sealed class PublisherRewardPolicy
    {
        public const int DefaultDailyLimit = 3;
        public const int MaxDailyLimit = 10;

        public int DailyLimit { get; }

        public PublisherRewardPolicy(int dailyLimit = DefaultDailyLimit) =>
            DailyLimit = Math.Min(Math.Max(dailyLimit, 1), MaxDailyLimit);
    }
}
