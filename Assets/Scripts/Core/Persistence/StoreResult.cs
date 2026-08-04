// StoreResult.cs
// #355 / #362: success / failure / offline for IGameStore — so a network-backed store can
// report Offline without inventing ad-hoc bools or throwing for expected misses.
//
// Distinct from SaveVersionMismatchException: that type remains for callers that prefer
// exceptions; FileGameStore's async API maps version refusal into StoreStatus.VersionMismatch.

namespace Strategos.Persistence
{
    public enum StoreStatus
    {
        Ok = 0,
        NotFound = 1,
        Offline = 2,
        Failed = 3,
        VersionMismatch = 4,
    }

    /// <summary>Non-generic store outcome (Save, Delete).</summary>
    public readonly struct StoreResult
    {
        public StoreStatus Status { get; }
        public string Message { get; }

        public bool Ok => Status == StoreStatus.Ok;

        public StoreResult(StoreStatus status, string message = null)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public static StoreResult Success() => new(StoreStatus.Ok);
        public static StoreResult NotFound(string message = null) =>
            new(StoreStatus.NotFound, message);
        public static StoreResult Offline(string message = null) =>
            new(StoreStatus.Offline, message);
        public static StoreResult Failed(string message = null) =>
            new(StoreStatus.Failed, message);
        public static StoreResult VersionMismatch(string message = null) =>
            new(StoreStatus.VersionMismatch, message);
    }

    /// <summary>Store outcome that carries a value on success.</summary>
    public readonly struct StoreResult<T>
    {
        public StoreStatus Status { get; }
        public T Value { get; }
        public string Message { get; }

        public bool Ok => Status == StoreStatus.Ok;

        public StoreResult(StoreStatus status, T value = default, string message = null)
        {
            Status = status;
            Value = value;
            Message = message ?? string.Empty;
        }

        public static StoreResult<T> Success(T value) =>
            new(StoreStatus.Ok, value);
        public static StoreResult<T> NotFound(string message = null) =>
            new(StoreStatus.NotFound, default, message);
        public static StoreResult<T> Offline(string message = null) =>
            new(StoreStatus.Offline, default, message);
        public static StoreResult<T> Failed(string message = null) =>
            new(StoreStatus.Failed, default, message);
        public static StoreResult<T> VersionMismatch(string message = null) =>
            new(StoreStatus.VersionMismatch, default, message);
    }
}
