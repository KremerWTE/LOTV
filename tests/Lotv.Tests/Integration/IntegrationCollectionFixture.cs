namespace Lotv.Tests.Integration;

/// <summary>
/// Marks the "Integration" collection so xUnit serializes integration tests
/// within the same collection — avoids SQLite file locking between test classes
/// that share a LotvApiFactory instance.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<LotvApiFactory> { }
