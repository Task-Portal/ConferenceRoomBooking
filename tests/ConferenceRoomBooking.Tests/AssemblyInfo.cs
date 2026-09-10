using Xunit;

// Integration tests in this project each spin up their own in-process ASP.NET Core host
// (WebApplicationFactory) and SQLite database, but they still share the same OS process -
// same thread pool, same native SQLite library. Running many test CLASSES in parallel
// (xUnit's default behavior) under heavy load - e.g. BookingConcurrencyTests firing 10
// simultaneous requests - can starve or delay unrelated tests running at the same moment,
// producing intermittent, hard-to-reproduce failures that have nothing to do with the
// actual code being tested.
//
// Disabling collection parallelization makes every test class run one after another
// instead. For a suite this size (a couple dozen tests), the time cost is negligible
// (seconds), and it removes an entire category of flakiness. This does NOT affect
// ordering of test METHODS within a single class - those were already sequential by
// default; this only stops different classes from overlapping with each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]